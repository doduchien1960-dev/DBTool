using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Parametric haunch reinforcement planner: corner reinforcement bars anchored into base and wall.</summary>
public static class HaunchRebarPlanner
{
    public const string RuleId = "HAUNCH-REBAR-001";
    public const string FamilyId = "Haunch";
    public const double DefaultAnchorMultiple = 30.0;

    public static OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);

        var results = new[] { PlanMember(spec, HaunchOwner.Base), PlanMember(spec, HaunchOwner.Roof) };
        var diagnostics = results.SelectMany(r => r.Diagnostics).ToArray();
        if (results.Any(r => !r.Success)) return new(false, null, diagnostics);
        return new(true, new RebarLayout(results.SelectMany(r => r.Value!.Occurrences), diagnostics,
            CalculationScope.FullRebar, results.Any(r => r.Value!.Status == LayoutStatus.Partial) ? LayoutStatus.Partial : LayoutStatus.Complete), diagnostics);
    }

    private static OperationResult<RebarLayout> PlanMember(ValidatedManholeSpec spec, HaunchOwner owner)
    {
        bool roof = owner == HaunchOwner.Roof;
        string member = roof ? "Roof" : "Base";
        var kind = roof ? ComponentKind.Roof : ComponentKind.Base;
        var layer = roof ? Layer.Top : Layer.Bottom;

        var haunchSpec = roof ? spec.Input.TopHaunchSpec : spec.Input.BaseHaunchSpec;
        double legacySize = roof ? spec.Input.TopHaunch : spec.Input.BaseHaunch;
        double w = haunchSpec?.WidthMm ?? legacySize;
        double h = haunchSpec?.HeightMm ?? legacySize;

        if (w <= 0 || h <= 0)
            return new(true, new RebarLayout([], [], CalculationScope.FullRebar, LayoutStatus.Complete), []);

        var bars = new List<BarOccurrence>();
        var diagnostics = new List<Diagnostic>();
        var source = haunchSpec?.Sources.FirstOrDefault() ?? spec.Sources.FirstOrDefault();
        diagnostics.Add(new Diagnostic("HAUNCH_REINFORCEMENT_NEEDS_RULE", DiagnosticSeverity.Review, DiagnosticScope.Manhole,
            spec.Input.Id, null, source, "source-confirmed inclined mesh, distribution diameter and anchorage rule",
            "HAUNCH-PROPOSAL-20261012-v1",
            "Candidate independent detailing is retained for review; proposed diameters, spacing and anchorage are not certified fabrication lengths."));
        bool incomplete = false;
        void MissingSide(string side, double span)
        {
            incomplete = true;
            diagnostics.Add(new Diagnostic("HAUNCH_SIDE_NO_ROOM", DiagnosticSeverity.Review, DiagnosticScope.Manhole,
                spec.Input.Id, null, source, "positive reinforced side span", side + ":" + span.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                "Active haunch side cannot fit its required bars; layout is Partial rather than silently Complete."));
        }

        double L = spec.Input.L;
        double B = spec.Input.B;
        double cover = roof ? spec.Input.MainRebar?.RoofCoverMm ?? spec.Input.CoverMm : spec.Input.CoverMm;
        var detailing = haunchSpec?.Rebar ?? new HaunchRebarSpec();
        int diameter = detailing.InclinedMesh.DiameterMm;
        double spacing = detailing.InclinedMesh.SpacingMm;
        double slabAnchorLength = detailing.SlabAnchorMm;
        double wallAnchorLength = detailing.WallAnchorMm;
        double S = Math.Sqrt(w * w + h * h);
        double dc = cover + diameter / 2.0;
        int distDiameter = detailing.DistributionDiameterMm;

        BarPath MakePath(IEnumerable<PathSegment> segments, HookPattern hooks, MeasurementBasis basis)
        {
            Point3Mm Mirror(Point3Mm p) => new(p.X, p.Y, spec.Input.H - p.Z);
            var actual = roof ? segments.Select(s => (PathSegment)new LineSegment3Mm(Mirror(s.Start), Mirror(s.End))) : segments;
            return new BarPath(actual, false, hooks, 0, false, basis);
        }

        // South: adjacent to Wall/T3, inner face at Y = -B/2
        PlanSideX("South", -B / 2, true, "Wall/T3");

        // North: adjacent to Wall/T1, inner face at Y = +B/2
        PlanSideX("North", B / 2, false, "Wall/T1");

        // East: adjacent to Wall/T2, inner face at X = +L/2
        PlanSideY("East", L / 2, false, "Wall/T2");

        // West: adjacent to Wall/T4, inner face at X = -L/2
        PlanSideY("West", -L / 2, true, "Wall/T4");

        void PlanSideX(string side, double wallInnerY, bool isNegativeY, string wallOwner)
        {
            double deltaY = (dc * S - w * dc) / h;
            double deltaZ = (dc * S - h * dc) / w;

            double yBase = isNegativeY ? wallInnerY + w - deltaY : wallInnerY - w + deltaY;
            double zBase = -dc;
            double yWall = isNegativeY ? wallInnerY - dc : wallInnerY + dc;
            double zWall = h - deltaZ;

            double yAnc = isNegativeY ? yBase + slabAnchorLength : yBase - slabAnchorLength;
            double zAnc = zWall + wallAnchorLength;

            double xMin = -L / 2 + w + cover;
            double xMax = L / 2 - w - cover;
            double span = xMax - xMin;
            if (span <= 0) { MissingSide(side, span); return; }

            int intervals = Math.Max(1, (int)Math.Ceiling(span / spacing));
            int count = intervals + 1;
            double step = span / intervals;

            for (int i = 0; i < count; i++)
            {
                double x = xMin + i * step;
                var pAncBase = new Point3Mm(x, yAnc, zBase);
                var pHaunchBase = new Point3Mm(x, yBase, zBase);
                var pHaunchWall = new Point3Mm(x, yWall, zWall);
                var pAncWall = new Point3Mm(x, yWall, zAnc);

                var segments = new PathSegment[]
                {
                    new LineSegment3Mm(pAncBase, pHaunchBase),
                    new LineSegment3Mm(pHaunchBase, pHaunchWall),
                    new LineSegment3Mm(pHaunchWall, pAncWall)
                };

                double length = BendLengthCalculator.Distance(pAncBase, pHaunchBase)
                              + BendLengthCalculator.Distance(pHaunchBase, pHaunchWall)
                              + BendLengthCalculator.Distance(pHaunchWall, pAncWall);

                var path = MakePath(segments, HookPattern.Hook2M, MeasurementBasis.NominalPolyline);
                string id = $"{spec.Input.Id}/{member}/Haunch/{side}/Inclined/{i + 1:000}";
                bars.Add(new BarOccurrence(id, null, $"{member}/Haunch", kind, layer, Axis.Y,
                    diameter, path, length, null, LengthStatus.NeedsRule, RuleId,
                    source is null ? [] : [source], RebarDirection.Supplementary, wallOwner,
                    RebarSurface.Inner, FamilyId, MeasurementBasisKind.Nominal, null,
                    i, x, member, null, "Inclined", null, length, RuleId, "Haunch", $"{side}:{i}", "Centreline"));
            }

            // Longitudinal distribution bars along this side
            double separation = (diameter + distDiameter) / 2d;
            double yMid = (yBase + yWall) / 2.0 + (isNegativeY ? -1 : 1) * h / S * separation;
            double zMid = (zBase + zWall) / 2.0 - w / S * separation;
            var distSegments = new PathSegment[] { new LineSegment3Mm(new(xMin, yMid, zMid), new(xMax, yMid, zMid)) };
            double distLength = xMax - xMin;
            var distPath = MakePath(distSegments, HookPattern.Hook0M, MeasurementBasis.TangentPath);
            string distId = $"{spec.Input.Id}/{member}/Haunch/{side}/Dist/001";
            bars.Add(new BarOccurrence(distId, null, $"{member}/Haunch", kind, layer, Axis.X,
                distDiameter, distPath, distLength, null, LengthStatus.NeedsRule, RuleId,
                source is null ? [] : [source], RebarDirection.Horizontal, wallOwner,
                RebarSurface.Inner, FamilyId, MeasurementBasisKind.Centerline, null,
                0, (xMin + xMax) / 2, member, null, "Dist", null, distLength, RuleId, "Haunch", $"{side}:Dist", "Centreline"));
        }

        void PlanSideY(string side, double wallInnerX, bool isNegativeX, string wallOwner)
        {
            double deltaX = (dc * S - w * dc) / h;
            double deltaZ = (dc * S - h * dc) / w;

            double xBase = isNegativeX ? wallInnerX + w - deltaX : wallInnerX - w + deltaX;
            double zBase = -dc;
            double xWall = isNegativeX ? wallInnerX - dc : wallInnerX + dc;
            double zWall = h - deltaZ;

            double xAnc = isNegativeX ? xBase + slabAnchorLength : xBase - slabAnchorLength;
            double zAnc = zWall + wallAnchorLength;

            double yMin = -B / 2 + w + cover;
            double yMax = B / 2 - w - cover;
            double span = yMax - yMin;
            if (span <= 0) { MissingSide(side, span); return; }

            int intervals = Math.Max(1, (int)Math.Ceiling(span / spacing));
            int count = intervals + 1;
            double step = span / intervals;

            for (int i = 0; i < count; i++)
            {
                double y = yMin + i * step;
                var pAncBase = new Point3Mm(xAnc, y, zBase);
                var pHaunchBase = new Point3Mm(xBase, y, zBase);
                var pHaunchWall = new Point3Mm(xWall, y, zWall);
                var pAncWall = new Point3Mm(xWall, y, zAnc);

                var segments = new PathSegment[]
                {
                    new LineSegment3Mm(pAncBase, pHaunchBase),
                    new LineSegment3Mm(pHaunchBase, pHaunchWall),
                    new LineSegment3Mm(pHaunchWall, pAncWall)
                };

                double length = BendLengthCalculator.Distance(pAncBase, pHaunchBase)
                              + BendLengthCalculator.Distance(pHaunchBase, pHaunchWall)
                              + BendLengthCalculator.Distance(pHaunchWall, pAncWall);

                var path = MakePath(segments, HookPattern.Hook2M, MeasurementBasis.NominalPolyline);
                string id = $"{spec.Input.Id}/{member}/Haunch/{side}/Inclined/{i + 1:000}";
                bars.Add(new BarOccurrence(id, null, $"{member}/Haunch", kind, layer, Axis.X,
                    diameter, path, length, null, LengthStatus.NeedsRule, RuleId,
                    source is null ? [] : [source], RebarDirection.Supplementary, wallOwner,
                    RebarSurface.Inner, FamilyId, MeasurementBasisKind.Nominal, null,
                    i, y, member, null, "Inclined", null, length, RuleId, "Haunch", $"{side}:{i}", "Centreline"));
            }

            // Longitudinal distribution bar along this side
            double separation = (diameter + distDiameter) / 2d;
            double xMid = (xBase + xWall) / 2.0 + (isNegativeX ? -1 : 1) * h / S * separation;
            double zMid = (zBase + zWall) / 2.0 - w / S * separation;
            var distSegments = new PathSegment[] { new LineSegment3Mm(new(xMid, yMin, zMid), new(xMid, yMax, zMid)) };
            double distLength = yMax - yMin;
            var distPath = MakePath(distSegments, HookPattern.Hook0M, MeasurementBasis.TangentPath);
            string distId = $"{spec.Input.Id}/{member}/Haunch/{side}/Dist/001";
            bars.Add(new BarOccurrence(distId, null, $"{member}/Haunch", kind, layer, Axis.Y,
                distDiameter, distPath, distLength, null, LengthStatus.NeedsRule, RuleId,
                source is null ? [] : [source], RebarDirection.Horizontal, wallOwner,
                RebarSurface.Inner, FamilyId, MeasurementBasisKind.Centerline, null,
                0, (yMin + yMax) / 2, member, null, "Dist", null, distLength, RuleId, "Haunch", $"{side}:Dist", "Centreline"));
        }

        return new(true, new RebarLayout(bars, diagnostics, CalculationScope.FullRebar,
            incomplete || bars.Any(b => b.LengthStatus == LengthStatus.NeedsRule) ? LayoutStatus.Partial : LayoutStatus.Complete), diagnostics);
    }
}
