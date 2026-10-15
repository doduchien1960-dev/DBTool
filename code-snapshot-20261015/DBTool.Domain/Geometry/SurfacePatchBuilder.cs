using System.Collections;
using System.Collections.Immutable;

namespace DBTool.Domain.Geometry;

public sealed class SurfacePatchSet : IReadOnlyList<FacePatch>
{
    public SurfacePatchSet(IEnumerable<FacePatch> patches) => Items = ContractGuard.Copy(patches);
    public ImmutableArray<FacePatch> Items { get; }
    public int Count => Items.Length;
    public int Length => Items.Length;
    public FacePatch this[int index] => Items[index];
    public IEnumerator<FacePatch> GetEnumerator() => ((IEnumerable<FacePatch>)Items).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>Builds bounded horizontal patches from a net component footprint without polygonizing it.</summary>
public static class SurfacePatchBuilder
{
    private static readonly Vector3 X = new(1, 0, 0);
    private static readonly Vector3 Y = new(0, 1, 0);
    private static readonly Vector3 Up = new(0, 0, 1);
    private static readonly Vector3 Down = new(0, 0, -1);

    public static OperationResult<SurfacePatchSet> Build(ComponentGeometry component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.FacePatches.Length > 0)
            return new(true, new SurfacePatchSet(component.FacePatches), []);
        if (component.Footprint.InnerLoops.Length > 0)
            return new(false, null, [new Diagnostic("SURFACE_PATCH_TOPOLOGY_UNSUPPORTED", DiagnosticSeverity.Block,
                DiagnosticScope.Component, null, component.Id, component.Sources.FirstOrDefault(),
                "net bounded face patches", "footprint contains inner loops", "Surface patches need explicit net topology.")]);

        var points = component.Footprint.Outer.Vertices;
        double x0 = points.Min(x => x.X), x1 = points.Max(x => x.X);
        double y0 = points.Min(x => x.Y), y1 = points.Max(x => x.Y);
        var rectangle = new Rectangle2Mm(x0, y0, x1, y1);
        double area = (x1 - x0) * (y1 - y0);
        var top = new FacePatch(component.Id + "/Top", component.Id, FaceRole.Top,
            new RectangleProfile(rectangle), new Frame3Mm(new(0, 0, component.ZRange.End), X, Y, Up),
            new AreaMm2(area), Up, null, component.Sources);
        var bottom = new FacePatch(component.Id + "/Bottom", component.Id, FaceRole.Bottom,
            new RectangleProfile(rectangle), new Frame3Mm(new(0, 0, component.ZRange.Start), X, Y, Down),
            new AreaMm2(area), Down, null, component.Sources);
        return new(true, new SurfacePatchSet([top, bottom]), []);
    }
}
