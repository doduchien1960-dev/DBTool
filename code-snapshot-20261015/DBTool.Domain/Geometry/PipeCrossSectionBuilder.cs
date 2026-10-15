using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public sealed record RoundPipeCrossSection
{
    internal RoundPipeCrossSection(CircleProfile inner, CircleProfile outer, CircleProfile cutout)
    {
        InnerCircle = inner; PipeOuterCircle = outer; ConcreteCutoutCircle = cutout;
    }
    public CircleProfile InnerCircle { get; }
    public CircleProfile PipeOuterCircle { get; }
    public CircleProfile ConcreteCutoutCircle { get; }
    public bool MortarEmpty => PipeOuterCircle.RadiusMm == ConcreteCutoutCircle.RadiusMm;
}

public sealed record DoubleBoxPolicy
{
    public DoubleBoxPolicy(double? middleWallThicknessMm = null, SourceRef? approvedSource = null)
    { MiddleWallThicknessMm = middleWallThicknessMm; ApprovedSource = approvedSource; }
    public double? MiddleWallThicknessMm { get; }
    public SourceRef? ApprovedSource { get; }
}

public sealed record BoxInnerCell
{
    public BoxInnerCell(PolygonProfile profile, Point2Mm center, double areaMm2)
    { Profile = profile; Center = center; AreaMm2 = areaMm2; }
    public PolygonProfile Profile { get; }
    public Point2Mm Center { get; }
    public double AreaMm2 { get; }
}

public sealed record PipeCrossSection
{
    public PipeCrossSection(ImmutableArray<BoxInnerCell> innerCells, RectangleProfile pipeOuter,
        RectangleProfile concreteCutout, bool mortarRegionEmpty, ImmutableArray<SourceRef> sourceRefs)
    { InnerCells = innerCells; PipeOuter = pipeOuter; ConcreteCutout = concreteCutout;
      MortarRegionEmpty = mortarRegionEmpty; SourceRefs = sourceRefs; }
    public ImmutableArray<BoxInnerCell> InnerCells { get; }
    public RectangleProfile PipeOuter { get; }
    public RectangleProfile ConcreteCutout { get; }
    public bool MortarRegionEmpty { get; }
    public ImmutableArray<SourceRef> SourceRefs { get; }
}

public static class PipeCrossSectionBuilder
{
    public static OperationResult<PipeCrossSection> BuildBox(
        PipeCatalogEntry entry, PipeConnectionSpec pipe, DoubleBoxPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentNullException.ThrowIfNull(policy);
        SourceRef? source = pipe.SourceRefs.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value).FirstOrDefault();
        OperationResult<PipeCrossSection> Fail(string code, string expected, string actual) =>
            new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
                null, null, source, expected, actual, "Box pipe cross-section is unavailable.")]);
        if (entry.Code != pipe.CatalogCode || entry.Kind is not (PipeKind.Box or PipeKind.DoubleBox))
            return Fail("PIPE_SHAPE_UNSUPPORTED", "matching box catalog", entry.Code);
        double? middle = entry.MiddleWallThicknessMm;
        if (entry.Kind == PipeKind.DoubleBox && middle is null)
        {
            if (policy.MiddleWallThicknessMm is null || policy.ApprovedSource is null)
                return Fail("NEEDS_RULE_DOUBLE_WALL", "catalog value or approved explicit binding", "unknown");
            middle = policy.MiddleWallThicknessMm;
        }
        double width = entry.InnerWidthMm!.Value, height = entry.InnerHeightMm!.Value;
        double t = entry.WallThicknessMm, m = pipe.MortarMm, a = entry.ChamferMm;
        double cellWidth = entry.Kind == PipeKind.DoubleBox ? (width - middle!.Value) / 2 : width;
        double centerZ = pipe.InvertMm + height / 2;
        double outerW = width + 2 * t, outerH = height + 2 * t;
        double cutW = outerW + 2 * m, cutH = outerH + 2 * m;
        double area = cellWidth * height - 2 * a * a;
        if (entry.Kind == PipeKind.DoubleBox &&
            (!middle.HasValue || !double.IsFinite(middle.Value) || middle.Value <= 0 || middle.Value >= width) ||
            cellWidth <= 0 || a < 0 || 2 * a > Math.Min(cellWidth, height) ||
            area <= 0 || new[] { cellWidth, centerZ, outerW, outerH, cutW, cutH, area,
                pipe.OffsetMm - cutW / 2, pipe.OffsetMm + cutW / 2,
                centerZ - cutH / 2, centerZ + cutH / 2 }.Any(x => !double.IsFinite(x)))
            return Fail("PIPE_DIMENSION_INVALID", "finite non-overlapping box cells", "invalid dimensions or overflow");
        var cells = ImmutableArray.CreateBuilder<BoxInnerCell>();
        var centers = entry.Kind == PipeKind.DoubleBox
            ? new[] { -(width + middle!.Value) / 4, (width + middle.Value) / 4 }
            : new[] { 0d };
        foreach (double u in centers)
        {
            double x0 = pipe.OffsetMm + u - cellWidth / 2, x1 = x0 + cellWidth;
            double z0 = pipe.InvertMm, z1 = z0 + height;
            var polygon = a == 0
                ? new Polygon2Mm([new(x0,z0),new(x1,z0),new(x1,z1),new(x0,z1)])
                : new Polygon2Mm([
                    new(x0 + a, z0), new(x1 - a, z0), new(x1, z0 + a), new(x1, z1 - a),
                    new(x1 - a, z1), new(x0 + a, z1), new(x0, z1 - a), new(x0, z0 + a)]);
            cells.Add(new BoxInnerCell(new PolygonProfile(polygon), new Point2Mm(pipe.OffsetMm + u, centerZ), area));
        }
        var outer = new RectangleProfile(new Rectangle2Mm(pipe.OffsetMm - outerW / 2, centerZ - outerH / 2,
            pipe.OffsetMm + outerW / 2, centerZ + outerH / 2));
        var cut = m == 0 ? outer : new RectangleProfile(new Rectangle2Mm(pipe.OffsetMm - cutW / 2,
            centerZ - cutH / 2, pipe.OffsetMm + cutW / 2, centerZ + cutH / 2));
        var sources = pipe.SourceRefs.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Value)
            .Concat(policy.ApprovedSource is null ? [] : [policy.ApprovedSource])
            .Distinct().ToImmutableArray();
        return new(true, new PipeCrossSection(cells.ToImmutable(), outer, cut, m == 0, sources), []);
    }

    /// <summary>PIPE-ROUND-001: invert is the internal bottom, all scalar lengths are millimetres.</summary>
    public static OperationResult<RoundPipeCrossSection> BuildRound(double diameterMm, double pipeThicknessMm,
        double mortarMm, double invertMm, double offsetMm, SourceRef? source = null)
    {
        if (!double.IsFinite(diameterMm) || diameterMm <= 0 ||
            !double.IsFinite(pipeThicknessMm) || pipeThicknessMm < 0 ||
            !double.IsFinite(mortarMm) || mortarMm < 0 ||
            !double.IsFinite(invertMm) || invertMm < 0 || !double.IsFinite(offsetMm))
            return Invalid();

        double innerRadius = diameterMm / 2;
        double outerRadius = innerRadius + pipeThicknessMm;
        double cutRadius = outerRadius + mortarMm;
        double centerZ = invertMm + innerRadius;
        // Also guard derived values before constructing finite-only DTOs.
        if (innerRadius <= 0 || new[] { outerRadius, cutRadius, centerZ,
            centerZ - cutRadius, centerZ + cutRadius, offsetMm - cutRadius, offsetMm + cutRadius }
            .Any(v => !double.IsFinite(v))) return Invalid();

        var center = new Point2Mm(offsetMm, centerZ);
        var inner = new CircleProfile(center, innerRadius);
        var outer = new CircleProfile(center, outerRadius);
        // Equality represents an empty mortar annulus; do not generate a zero-thickness loop.
        var cutout = mortarMm == 0 ? outer : new CircleProfile(center, cutRadius);
        return new(true, new RoundPipeCrossSection(inner, outer, cutout), []);

        OperationResult<RoundPipeCrossSection> Invalid() => new(false, null,
            [new Diagnostic("PIPE_DIMENSION_INVALID", DiagnosticSeverity.Block, DiagnosticScope.Global,
                null, null, source, "finite D>0, thickness/mortar/invert>=0 and finite derived bounds",
                FormattableString.Invariant($"D={diameterMm};t={pipeThicknessMm};m={mortarMm};h={invertMm};e={offsetMm}"),
                "Invalid round pipe dimensions or arithmetic overflow.")]);
    }
}
