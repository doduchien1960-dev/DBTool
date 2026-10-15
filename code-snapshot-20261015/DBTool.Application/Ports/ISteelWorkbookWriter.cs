using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface ISteelWorkbookWriter
{
    OperationResult<ExportArtifact> Write(CalculationPackage package, WorkbookWriteRequest request);
}

