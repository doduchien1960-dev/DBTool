using System.Collections.Immutable;
using DBTool.Domain;
using DBTool.Drawing;
namespace DBTool.Application;
public interface ISteelScheduleStage
{
    OperationResult<SteelSchedule> Calculate(RebarLayout layout, CalculationPolicy policy);
}

