using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Drawing;

public sealed class PipeGeometryInfo
{
    public OpeningProfile Opening { get; }
    public bool IsCircle { get; }
    public double XCenter { get; }
    public double ZCenter { get; }
    public double RInner { get; }
    public double ROuter { get; }
    public double RCutout { get; }
    public double HalfWInner { get; }
    public double HalfHInner { get; }
    public double HalfWOuter { get; }
    public double HalfHOuter { get; }
    public double HalfWCutout { get; }
    public double HalfHCutout { get; }
    public double ProfileCenterX { get; }
    public double ProfileCenterY { get; }

    public PipeGeometryInfo(
        OpeningProfile opening,
        bool isCircle,
        double xCenter,
        double zCenter,
        double rInner,
        double rOuter,
        double rCutout,
        double halfWInner,
        double halfHInner,
        double halfWOuter,
        double halfHOuter,
        double halfWCutout,
        double halfHCutout,
        double profileCenterX = 0,
        double profileCenterY = 0)
    {
        Opening = opening;
        IsCircle = isCircle;
        XCenter = xCenter;
        ZCenter = zCenter;
        RInner = rInner;
        ROuter = rOuter;
        RCutout = rCutout;
        HalfWInner = halfWInner;
        HalfHInner = halfHInner;
        HalfWOuter = halfWOuter;
        HalfHOuter = halfHOuter;
        HalfWCutout = halfWCutout;
        HalfHCutout = halfHCutout;
        ProfileCenterX = profileCenterX;
        ProfileCenterY = profileCenterY;
    }
}

/// <summary>Details pipe openings on wall reinforcement views (Sheet 2 and Sheet 3) for both round and box pipes.</summary>
public static class SheetWallPipeDetailer
{
    private static bool MatchesWall(string ownerId, string wallId) =>
        ownerId == wallId || ownerId == $"Wall/{wallId}" || wallId == $"Wall/{ownerId}" ||
        ownerId.EndsWith("/" + wallId, StringComparison.Ordinal) || wallId.EndsWith("/" + ownerId, StringComparison.Ordinal);

    public static bool HasPipe(GeometryModel geometry, string wallId) =>
        geometry.Openings.Any(o => MatchesWall(o.OwnerId, wallId) &&
            (o.ConcreteCutout is CircleProfile || o.ConcreteCutout is RectangleProfile || o.ConcreteCutout is PolygonProfile));

    public static PipeGeometryInfo? GetPipeGeometry(GeometryModel geometry, string wallId)
    {
        var op = geometry.Openings.FirstOrDefault(o => MatchesWall(o.OwnerId, wallId) &&
            (o.ConcreteCutout is CircleProfile || o.ConcreteCutout is RectangleProfile || o.ConcreteCutout is PolygonProfile));
        if (op is null) return null;
        return GetPipeGeometry(op,wallId);
    }

    private static PipeGeometryInfo? GetPipeGeometry(OpeningProfile op,string wallId)
    {

        bool alongY = wallId.EndsWith("T2", StringComparison.Ordinal) || wallId.EndsWith("T4", StringComparison.Ordinal);
        double xCenter = alongY ? op.Frame.Origin.Y : op.Frame.Origin.X;
        double zCenter = (op.ZRange.Start + op.ZRange.End) / 2.0;

        if (op.ConcreteCutout is CircleProfile cutoutCircle && op.Inner is CircleProfile innerCircle)
        {
            if(op.PipeOuter is not CircleProfile outerCircle || op.WallFrame is null) return null;
            var physical=op.WallFrame.ToGlobal(cutoutCircle.Center.X,0,cutoutCircle.Center.Y);
            xCenter=alongY?physical.Y:physical.X;zCenter=physical.Z;
            return new PipeGeometryInfo(
                op, true, xCenter, zCenter,
                innerCircle.RadiusMm, outerCircle.RadiusMm, cutoutCircle.RadiusMm,
                innerCircle.RadiusMm, innerCircle.RadiusMm,
                outerCircle.RadiusMm, outerCircle.RadiusMm,
                cutoutCircle.RadiusMm, cutoutCircle.RadiusMm,
                cutoutCircle.Center.X, cutoutCircle.Center.Y);
        }

        Rectangle2Mm cutoutRect;
        if (op.ConcreteCutout is RectangleProfile rectCut)
        {
            cutoutRect = rectCut.Rectangle;
        }
        else if (op.ConcreteCutout is PolygonProfile polyCut && polyCut.Polygon.Vertices.Length > 0)
        {
            var vs = polyCut.Polygon.Vertices;
            cutoutRect = new Rectangle2Mm(vs.Min(p => p.X), vs.Min(p => p.Y), vs.Max(p => p.X), vs.Max(p => p.Y));
        }
        else
        {
            return null;
        }

        double hwCut = (cutoutRect.MaxX - cutoutRect.MinX) / 2.0;
        double hhCut = (cutoutRect.MaxY - cutoutRect.MinY) / 2.0;
        double profCenterX = (cutoutRect.MinX + cutoutRect.MaxX) / 2.0;
        double profCenterY = (cutoutRect.MinY + cutoutRect.MaxY) / 2.0;
        if(op.WallFrame is null) return null;
        var cutCenter=op.WallFrame.ToGlobal(profCenterX,0,profCenterY);
        xCenter=alongY?cutCenter.Y:cutCenter.X;zCenter=cutCenter.Z;

        double hwIn = hwCut;
        double hhIn = hhCut;
        if (op.Inner is RectangleProfile innerRect)
        {
            hwIn = (innerRect.Rectangle.MaxX - innerRect.Rectangle.MinX) / 2.0;
            hhIn = (innerRect.Rectangle.MaxY - innerRect.Rectangle.MinY) / 2.0;
        }
        else if (op.Inner is MultiCellProfile multi && multi.Cells.Length > 0)
        {
            var verts = multi.Cells.SelectMany(c => c.Polygon.Vertices).ToArray();
            if (verts.Length > 0)
            {
                hwIn = (verts.Max(p => p.X) - verts.Min(p => p.X)) / 2.0;
                hhIn = (verts.Max(p => p.Y) - verts.Min(p => p.Y)) / 2.0;
            }
        }
        else if (op.Inner is PolygonProfile poly && poly.Polygon.Vertices.Length > 0)
        {
            var verts = poly.Polygon.Vertices;
            hwIn = (verts.Max(p => p.X) - verts.Min(p => p.X)) / 2.0;
            hhIn = (verts.Max(p => p.Y) - verts.Min(p => p.Y)) / 2.0;
        }

        double hwOut = hwIn;
        double hhOut = hhIn;
        if (op.PipeOuter is RectangleProfile outerRect)
        {
            hwOut = (outerRect.Rectangle.MaxX - outerRect.Rectangle.MinX) / 2.0;
            hhOut = (outerRect.Rectangle.MaxY - outerRect.Rectangle.MinY) / 2.0;
        }
        else if (op.PipeOuter is MultiCellProfile multiOut && multiOut.Cells.Length > 0)
        {
            var verts = multiOut.Cells.SelectMany(c => c.Polygon.Vertices).ToArray();
            if (verts.Length > 0)
            {
                hwOut = (verts.Max(p => p.X) - verts.Min(p => p.X)) / 2.0;
                hhOut = (verts.Max(p => p.Y) - verts.Min(p => p.Y)) / 2.0;
            }
        }
        else if (op.PipeOuter is PolygonProfile polyOut && polyOut.Polygon.Vertices.Length > 0)
        {
            var verts = polyOut.Polygon.Vertices;
            hwOut = (verts.Max(p => p.X) - verts.Min(p => p.X)) / 2.0;
            hhOut = (verts.Max(p => p.Y) - verts.Min(p => p.Y)) / 2.0;
        }
        else
        {
            return null;
        }

        return new PipeGeometryInfo(
            op, false, xCenter, zCenter,
            hwIn, hwOut, hwCut,
            hwIn, hhIn,
            hwOut, hhOut,
            hwCut, hhCut,
            profCenterX, profCenterY);
    }

    public static IReadOnlyList<PolylinePrimitive> ClipWallMesh(
        IEnumerable<PolylinePrimitive> bars, GeometryModel geometry, string wallId)
    {
        var result=WallOpeningSymbolClipper.ClipMesh(bars,geometry,wallId);
        if(!result.Success) throw new InvalidOperationException(string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        return result.Value!;
    }
    public static IReadOnlyList<DrawingPrimitive> BuildPipePrimitives(
        GeometryModel geometry, PresentationRequest presentation, string viewId, string wallId, Bounds2Mm wallBounds)
    {
        var openings=geometry.Openings.Where(o=>MatchesWall(o.OwnerId,wallId)).DistinctBy(o=>o.Id).ToArray();
        var result=new List<DrawingPrimitive>();
        foreach(var opening in openings)
        {
            var pipe=GetPipeGeometry(opening,wallId);
            if(pipe is null) throw new InvalidOperationException("PIPE_DETAIL_PROFILE_UNSUPPORTED:"+opening.Id);
            string prefix=openings.Length==1?$"{viewId}/Pipe":$"{viewId}/Pipe/{opening.Id}";
            result.AddRange(BuildSingle(pipe,presentation,viewId,wallId,prefix));
        }
        return result;
    }

    private static IReadOnlyList<DrawingPrimitive> BuildSingle(PipeGeometryInfo pipe,PresentationRequest presentation,
        string viewId,string wallId,string prefix)
    {
        double xc = pipe.XCenter;
        double zc = pipe.ZCenter;
        var center = new Point2Mm(xc, zc);
        var list = new List<DrawingPrimitive>();

        if (pipe.IsCircle)
        {
            double r1 = pipe.RInner;
            double r2 = pipe.ROuter;
            double r3 = pipe.RCutout;


            // Concentric circles
            list.Add(new CirclePrimitive($"{prefix}/Inner", "Geometry/PipeInner", viewId, wallId, pipe.Opening.Id, null, center, r1));
            list.Add(new CirclePrimitive($"{prefix}/Outer", "Geometry/PipeOuter", viewId, wallId, pipe.Opening.Id, null, center, r2));
            list.Add(new CirclePrimitive($"{prefix}/Cutout", "Geometry/ConcreteCutout", viewId, wallId, pipe.Opening.Id, null, center, r3));
            // Centerlines
            double cExt = r3 + 50;
            list.Add(new PolylinePrimitive($"{prefix}/CenterH", "Dimension/Linear", viewId, wallId, null, null,
                [new(xc - cExt, zc), new(xc + cExt, zc)], false));
            list.Add(new PolylinePrimitive($"{prefix}/CenterV", "Dimension/Linear", viewId, wallId, null, null,
                [new(xc, zc - cExt), new(xc, zc + cExt)], false));

            // Invert elevation
            double invertM = (presentation.BaseElevationM?.Value ?? 0) + (zc - r1) / 1000.0;
            list.Add(new BlockPrimitive($"{prefix}/CT", "Elevation/Native", viewId, wallId, "Block:CT",
                new(xc, zc - r1), 0.3, [new BlockAttributeIntent("CT", invertM.ToString("0.00"))]));

            // Diameter dim
            int dia = (int)Math.Round(2 * r1);
            list.Add(new DimensionIntentPrimitive($"{prefix}/DiaDim", "Dimension/Linear", viewId, wallId, null, null,
                new(xc - r1, zc), new(xc + r1, zc), dia, "%%c<>", 0));

        }
        else
        {
            // Box pipe (cống hộp)
            double hw1 = pipe.HalfWInner;
            double hh1 = pipe.HalfHInner;
            double hw2 = pipe.HalfWOuter;
            double hh2 = pipe.HalfHOuter;
            double hw3 = pipe.HalfWCutout;
            double hh3 = pipe.HalfHCutout;



            Point2Mm[] Box(double hw, double hh) => [
                new(xc - hw, zc - hh), new(xc + hw, zc - hh),
                new(xc + hw, zc + hh), new(xc - hw, zc + hh)
            ];

            Point2Mm[] TransformToView(IEnumerable<Point2Mm> vertices) =>
                vertices.Select(p => {
                    var world=pipe.Opening.WallFrame!.ToGlobal(p.X,0,p.Y);
                    bool alongY=wallId.EndsWith("T2",StringComparison.Ordinal)||wallId.EndsWith("T4",StringComparison.Ordinal);
                    return new Point2Mm(alongY?world.Y:world.X,world.Z);
                }).ToArray();

            // Inner:
            if (pipe.Opening.Inner is MultiCellProfile multiInner)
            {
                for (int i = 0; i < multiInner.Cells.Length; i++)
                {
                    var pts = TransformToView(multiInner.Cells[i].Polygon.Vertices);
                    string innerId = multiInner.Cells.Length == 1 ? $"{prefix}/Inner" : $"{prefix}/Inner/{i + 1}";
                    list.Add(new PolylinePrimitive(innerId, "Geometry/PipeInner", viewId, wallId, null, null, pts, true));
                }
            }
            else if (pipe.Opening.Inner is PolygonProfile polyInner)
            {
                var pts = TransformToView(polyInner.Polygon.Vertices);
                list.Add(new PolylinePrimitive($"{prefix}/Inner", "Geometry/PipeInner", viewId, wallId, null, null, pts, true));
            }
            else
            {
                list.Add(new PolylinePrimitive($"{prefix}/Inner", "Geometry/PipeInner", viewId, wallId, pipe.Opening.Id, null, Box(hw1, hh1), true));
            }

            // Outer:
            if (pipe.Opening.PipeOuter is PolygonProfile polyOuter)
            {
                var pts = TransformToView(polyOuter.Polygon.Vertices);
                list.Add(new PolylinePrimitive($"{prefix}/Outer", "Geometry/PipeOuter", viewId, wallId, null, null, pts, true));
            }
            else if (pipe.Opening.PipeOuter is MultiCellProfile multiOuter)
            {
                for (int i = 0; i < multiOuter.Cells.Length; i++)
                {
                    var pts = TransformToView(multiOuter.Cells[i].Polygon.Vertices);
                    string outerId = multiOuter.Cells.Length == 1 ? $"{prefix}/Outer" : $"{prefix}/Outer/{i + 1}";
                    list.Add(new PolylinePrimitive(outerId, "Geometry/PipeOuter", viewId, wallId, null, null, pts, true));
                }
            }
            else
            {
                list.Add(new PolylinePrimitive($"{prefix}/Outer", "Geometry/PipeOuter", viewId, wallId, pipe.Opening.Id, null, Box(hw2, hh2), true));
            }

            // Cutout:
            if (pipe.Opening.ConcreteCutout is PolygonProfile polyCutout)
            {
                var pts = TransformToView(polyCutout.Polygon.Vertices);
                list.Add(new PolylinePrimitive($"{prefix}/Cutout", "Geometry/ConcreteCutout", viewId, wallId, null, null, pts, true));
            }
            else
            {
                list.Add(new PolylinePrimitive($"{prefix}/Cutout", "Geometry/ConcreteCutout", viewId, wallId, pipe.Opening.Id, null, Box(hw3, hh3), true));
            }

            // Centerlines
            double extX = hw3 + 60;
            double extY = hh3 + 60;
            list.Add(new PolylinePrimitive($"{prefix}/CenterH", "Dimension/Linear", viewId, wallId, pipe.Opening.Id, null,
                [new(xc - extX, zc), new(xc + extX, zc)], false));
            list.Add(new PolylinePrimitive($"{prefix}/CenterV", "Dimension/Linear", viewId, wallId, pipe.Opening.Id, null,
                [new(xc, zc - extY), new(xc, zc + extY)], false));

            // Invert elevation
            double invertM = (presentation.BaseElevationM?.Value ?? 0) + (zc - hh1) / 1000.0;
            list.Add(new BlockPrimitive($"{prefix}/CT", "Elevation/Native", viewId, wallId, "Block:CT",
                new(xc, zc - hh1), 0.3, [new BlockAttributeIntent("CT", invertM.ToString("0.00"))], occurrenceId: pipe.Opening.Id));

            // Dimensions
            list.Add(new DimensionIntentPrimitive($"{prefix}/WidthDim", "Dimension/Linear", viewId, wallId, pipe.Opening.Id, null,
                new(xc - hw1, zc), new(xc + hw1, zc), 2 * hw1, "<>", 0));
            list.Add(new DimensionIntentPrimitive($"{prefix}/HeightDim", "Dimension/Linear", viewId, wallId, pipe.Opening.Id, null,
                new(xc, zc - hh1), new(xc, zc + hh1), 2 * hh1, "<>", 90));

        }

        return list;
    }

}
