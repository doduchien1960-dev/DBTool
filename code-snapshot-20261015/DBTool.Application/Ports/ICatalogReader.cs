using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface ICatalogReader
{
    OperationResult<CatalogSet> Read(RawProjectInput input, NumericPolicy policy);
}

