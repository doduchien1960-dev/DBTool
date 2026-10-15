using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface IWorkbookImporter
{
    OperationResult<RawProjectInput> Read(WorkbookReadRequest request);
}

