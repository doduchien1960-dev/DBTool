using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

/// <summary>Authoritative native reproduction of Sheet 4 detailing (Neck Plan, AA, BB, Catch Basin and Schedule Table).</summary>
public static class SheetFourViews
{
    public const string PlanViewId = "Reinforcement/Neck/Plan";
    public const string AAViewId = "Reinforcement/Neck/AA";
    public const string BBViewId = "Reinforcement/Neck/BB";
    public const string ScheduleViewId = "SteelSchedule";

    public const string CatchPlanViewId = "Reinforcement/Catch/Plan";
    public const string CatchSec11ViewId = "Reinforcement/Catch/1-1";
    public const string CatchSec22ViewId = "Reinforcement/Catch/2-2";
}
