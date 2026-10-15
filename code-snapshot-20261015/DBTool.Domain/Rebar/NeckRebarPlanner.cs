using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class NeckRebarPlanner
{
    public static OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry,
        MeshZoneSet zones, WallOrNeckRebarSpec rebarSpec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(rebarSpec);
        return WallRebarPlanner.PlanNeck(spec, geometry, rebarSpec);
    }

    public static OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry,
        MeshZoneSet zones, WallOrNeckRebarSpec rebarSpec, double? effectiveClearX, double? effectiveClearY)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(rebarSpec);
        return WallRebarPlanner.PlanNeck(spec, geometry, rebarSpec);
    }
}
