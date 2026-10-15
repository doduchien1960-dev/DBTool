using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface IGeometryStage
{
    OperationResult<GeometryModel> Build(ValidatedManholeSpec spec, CalculationPolicy policy);
}

