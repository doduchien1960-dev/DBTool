using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface IBaseRebarStage
{
    OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry, CalculationPolicy policy);
}

