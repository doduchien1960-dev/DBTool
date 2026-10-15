using DBTool.Domain;

namespace DBTool.Application;

public interface IQuantityWorkbookWriter
{
    OperationResult<ExportArtifact> Write(CalculationPackage package,
        WorkbookWriteRequest request, CancellationToken token);
}
