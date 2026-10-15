using DBTool.Domain;
using DBTool.Drawing;

namespace DBTool.Application;

public sealed class CalculateManholeUseCase(IWorkbookImporter importer,ICatalogReader catalogReader,
    IManholeValidator validator,NumericPolicy numericPolicy,CalculationPipeline pipeline)
{
    public Task<OperationResult<CalculationPackage>> CalculateAsync(WorkbookReadRequest workbook,CalculationRequest request,
        PresentationRequest presentation,CancellationToken cancellationToken)
    {
        OperationResult<CalculationPackage> Run()
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var check = request.PolicyId == "S1-Analytic-1"
                    ? request.CheckS1()
                    : request.PolicyId == "S2Rebar-1" && request.Scope is (CalculationScope.FullRebar or CalculationScope.Full)
                        ? request.CheckS2RebarInput()
                        : request.CheckS2QuantityInput();
                if(!check.Success)return new(false,null,check.Diagnostics);
                var policy=CalculationPolicy.Select(request.PolicyId,request.AllowExperimental).Value!;
                var raw=importer.Read(workbook); if(!raw.Success)return new(false,null,raw.Diagnostics);
                cancellationToken.ThrowIfCancellationRequested();
                var globals=raw.Value!.Diagnostics.Where(d=>d.Scope==DiagnosticScope.Global&&d.Severity==DiagnosticSeverity.Block).ToArray();
                if(globals.Length>0)return new(false,null,globals);
                var catalogs=catalogReader.Read(raw.Value,numericPolicy); if(!catalogs.Success)return new(false,null,catalogs.Diagnostics);
                cancellationToken.ThrowIfCancellationRequested();
                var validated=validator.Validate(raw.Value,catalogs.Value!,request,policy);
                if(!validated.Success)return new(false,null,validated.Diagnostics);
                if(!string.Equals(validated.Value!.SourceWorkbookHash,raw.Value.WorkbookHash,StringComparison.OrdinalIgnoreCase))
                    return UseCaseErrors.Fail<CalculationPackage>("SOURCE_SNAPSHOT_MISMATCH","Validated provenance must match imported bytes.");
                var merged=new ValidatedProjectInput(validated.Value.SelectedManholes,validated.Value.InputHash,validated.Value.RuleVersion,
                    raw.Diagnostics.Concat(catalogs.Diagnostics).Concat(validated.Diagnostics).Distinct(),validated.Value.SourceWorkbookHash);
                cancellationToken.ThrowIfCancellationRequested();
                return pipeline.Calculate(merged,policy,request,presentation,cancellationToken);
            }
            catch(OperationCanceledException){return UseCaseErrors.Fail<CalculationPackage>("OPERATION_CANCELLED","Calculation cancelled before completion.");}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {return UseCaseErrors.Fail<CalculationPackage>("CALCULATION_FAILED",ex.Message);}
        }
        return Task.FromResult(Run());
    }
}
