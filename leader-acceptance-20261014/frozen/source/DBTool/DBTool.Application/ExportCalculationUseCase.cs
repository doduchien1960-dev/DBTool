using DBTool.Domain;

namespace DBTool.Application;

/// <summary>Additive port for writers that can honour cancellation immediately before finalizing an artifact.</summary>
public interface ICancellableSteelWorkbookWriter : ISteelWorkbookWriter
{
    OperationResult<ExportArtifact> Write(CalculationPackage package,WorkbookWriteRequest request,CancellationToken token);
}

public sealed class ExportCalculationUseCase(ISteelWorkbookWriter writer, IQuantityWorkbookWriter? quantityWriter = null)
{
    public Task<OperationResult<ExportArtifact>> ExportAsync(CalculationPackage package,WorkbookWriteRequest request,CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(package);
            var result=writer is ICancellableSteelWorkbookWriter cancellable?cancellable.Write(package,request,token):writer.Write(package,request);
            return Task.FromResult(result);
        }
        catch(OperationCanceledException){return Task.FromResult(UseCaseErrors.Fail<ExportArtifact>("OPERATION_CANCELLED","Export cancelled."));}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {return Task.FromResult(UseCaseErrors.Fail<ExportArtifact>("EXPORT_WRITE_FAILED",ex.Message));}
    }

    public Task<OperationResult<ExportArtifact>> ExportQuantityAsync(CalculationPackage package,
        WorkbookWriteRequest request, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(package);
            if (quantityWriter is null)
                return Task.FromResult(UseCaseErrors.Fail<ExportArtifact>("QUANTITY_WRITER_MISSING", "Quantity workbook writer is not registered."));
            if (package.ConcreteStatus == CalculationStatus.NotCalculated && package.FormworkStatus == CalculationStatus.NotCalculated)
                return Task.FromResult(UseCaseErrors.Fail<ExportArtifact>("QUANTITY_NOT_CALCULATED", "Quantity has not been calculated or has no basis."));
            return Task.FromResult(quantityWriter.Write(package, request, token));
        }
        catch(OperationCanceledException){return Task.FromResult(UseCaseErrors.Fail<ExportArtifact>("OPERATION_CANCELLED","Export cancelled."));}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {return Task.FromResult(UseCaseErrors.Fail<ExportArtifact>("EXPORT_WRITE_FAILED",ex.Message));}
    }
}
