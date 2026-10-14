using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class DowelPlanner
{
    public static OperationResult<RebarLayout> PlanVhg(DowelSpec spec, ValidatedManholeSpec input,
        GeometryModel geometry, CalculationPolicy policy, double holeDiameterMm, double supportCoverMm)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);

        var source = spec.Sources.FirstOrDefault();
        if (input.Input.Profile is not (Profile.GaThamNapRoi or Profile.GaThamThuNapRoi) || policy.PolicyId != "S2Rebar-1" || input.RuleVersion != policy.Version)
            return Block("DOWEL_PROFILE_UNSUPPORTED", "Verified separate-cover wall/roof joint required.", source);

        if (!double.IsFinite(holeDiameterMm) || holeDiameterMm <= spec.DiameterMm ||
            !double.IsFinite(supportCoverMm) || supportCoverMm < 0 || holeDiameterMm + 2 * supportCoverMm > input.Input.WallThicknessMm)
            return Block("DOWEL_SUPPORT_INVALID", "Dowel hole must fit between both wall cover envelopes.", source);

        // CF.Ga E9 is an entered bar length. Density rounding is not a length rule.
        // The generic historical planner retains its explicitly requested quantization.
        double length = spec.LengthMm;
        var roofs = geometry.Components.Where(c => c.Id == "Roof" && c.Kind == ComponentKind.Roof && c.MaterialId == "Concrete").ToArray();
        if (roofs.Length != 1) return Block("DOWEL_ROOF_REQUIRED", "One physical concrete roof required.", source);
        double top = roofs[0].ZRange.End;
        if (length <= 0 || top - length < 0 || top - length >= input.Input.H - input.Input.BeddingMortarMm)
            return Block("DOWEL_LENGTH_INVALID", "Dowel must penetrate the wall below the mortar without crossing its floor.", source);

        var bars = new List<BarOccurrence>();
        foreach (var wallId in Enum.GetValues<WallId>())
        {
            string owner = $"Wall/{wallId}";
            var walls = geometry.Components.Where(c => c.Id == owner && c.Kind == ComponentKind.Wall && c.MaterialId == "Concrete").ToArray();
            if (walls.Length != 1) return Block("DOWEL_WALL_REQUIRED", "Four concrete wall supports required.", source);
            var wall = walls[0];
            var frame = WallFrame.Create(wallId, input.Input.L, input.Input.B);
            double span = wallId is WallId.T1 or WallId.T3 ? input.Input.L : input.Input.B;

            // Native source counts across five cases resolve distribution per clear wall span.
            // Centered cells respect A as a maximum pitch; corner ownership is not duplicated.
            double count = Math.Ceiling(span / spec.StepMm);
            if (!double.IsFinite(count) || count > 100000) return Block("DOWEL_COUNT_INVALID", "Dowel count exceeds supported size.", source);
            var normal = wall.Footprint.Outer.Vertices.Select(p => frame.ToLocal(new(p.X, p.Y, 0)).V).ToArray();
            double v = (normal.Min() + normal.Max()) / 2;
            if (Math.Abs(normal.Min()) > policy.TolerancePolicy.GeometryMm ||
                Math.Abs(normal.Max() - input.Input.WallThicknessMm) > policy.TolerancePolicy.GeometryMm)
                return Block("DOWEL_SUPPORT_INVALID", "Wall footprint differs from its validated frame/thickness.", source);

            for (int i = 0; i < (int)count; i++)
            {
                double u = -span / 2 + (i + .5) * span / count;
                var path = new BarPath([new LineSegment3Mm(frame.ToGlobal(u, v, top), frame.ToGlobal(u, v, top - length))],
                    false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
                var bar = new BarOccurrence($"{input.Input.Id}/{owner}/Dowel/{i:000}", null, owner, ComponentKind.Wall, Layer.Bottom,
                    wallId is WallId.T1 or WallId.T3 ? Axis.X : Axis.Y, spec.DiameterMm, path, length, length, LengthStatus.Known,
                    "DOWEL-VHG-WALL-1", spec.Sources.Concat(wall.Sources), RebarDirection.Supplementary, owner, null, "Dowel",
                    MeasurementBasisKind.Centerline, null, i, u, roofs[0].Id, null, "Uncut", null, length, "DOWEL-VHG-WALL-1",
                    "Dowel", $"{owner}:{i}", "Centerline");

                var wallOpenings = geometry.Openings.Where(o => o.OwnerId == owner).ToArray();
                if (wallOpenings.Length > 0)
                {
                    var clip = OpeningClipper.Clip(bar, wallOpenings, new ClearancePolicy((holeDiameterMm - spec.DiameterMm) / 2), geometry);
                    if (!clip.Success) return new(false, null, clip.Diagnostics);
                    if (clip.Value!.RemovedLengthMm > policy.TolerancePolicy.GeometryMm)
                        return Block("DOWEL_OPENING_COLLISION", "Requested dowel hole intersects a pipe opening; no dowel was omitted.", source);
                }

                var catchOpenings = geometry.Openings.Where(o => o.OwnerId == "CatchBasin").ToArray();
                if (catchOpenings.Length > 0)
                {
                    var clip = OpeningClipper.Clip(bar, catchOpenings, new ClearancePolicy((holeDiameterMm - spec.DiameterMm) / 2), geometry);
                    if (!clip.Success) return new(false, null, clip.Diagnostics);
                    if (clip.Value!.RemovedLengthMm > policy.TolerancePolicy.GeometryMm)
                        return Block("DOWEL_CATCH_COLLISION", $"Requested dowel hole intersects opening at {owner} station {i}; no dowel was omitted.", source);
                }

                bars.Add(bar);
            }
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar, LayoutStatus.Complete), []);
    }

    public static OperationResult<RebarLayout> Plan(DowelSpec spec, ComponentGeometry geometry, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);

        if (spec.Round50Mode is null && Math.Abs(spec.LengthMm / 50d - Math.Round(spec.LengthMm / 50d)) > 1e-9)
            return Block("NEEDS_RULE_DOWEL", "Dowel quantization requires an explicit Round50Mode.", spec.Sources.FirstOrDefault());

        double length = Quantize(spec.LengthMm, spec.Round50Mode);
        var vertices = geometry.Footprint.Outer.Vertices;
        var bars = new List<BarOccurrence>();
        for (int i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Length];
            double edge = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
            if (edge <= 0) continue;
            double station = edge / 2;
            var p = new Point3Mm(a.X + (b.X - a.X) * station / edge, a.Y + (b.Y - a.Y) * station / edge, geometry.ZRange.Start);
            var end = new Point3Mm(p.X, p.Y, p.Z + length);
            var path = new BarPath([new LineSegment3Mm(p, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
            bars.Add(new BarOccurrence($"{geometry.Id}/Dowel/{i:000}", null, geometry.Id, geometry.Kind, Layer.Bottom,
                Axis.X, spec.DiameterMm, path, length, length, LengthStatus.Known, "DOWEL-S2-001",
                geometry.Sources.Concat(spec.Sources), RebarDirection.Supplementary, geometry.Id, null, "Dowel",
                MeasurementBasisKind.Centerline, null, i, station, geometry.Id, null, "Uncut", null, length, "DOWEL-S2-001",
                "Dowel", $"{geometry.Id}:{i}", "Centerline"));
        }
        return new(true, new RebarLayout(bars, [], CalculationScope.FullRebar), []);
    }

    public static OperationResult<RebarLayout> Plan(DowelSpec spec, GeometryModel geometry, CalculationPolicy policy) =>
        Plan(spec, geometry.Components.FirstOrDefault() ?? throw new ArgumentException("Geometry has no component."), policy);

    private static double Quantize(double value, Round50Mode? mode) => mode switch
    {
        null => value,
        Round50Mode.Floor => Math.Floor(value / 50d) * 50d,
        Round50Mode.Ceiling => Math.Ceiling(value / 50d) * 50d,
        _ => Math.Floor(value / 50d + 0.5d) * 50d
    };

    private static OperationResult<RebarLayout> Block(string code, string message, SourceRef? source) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null, source, null, null, message)]);
}
