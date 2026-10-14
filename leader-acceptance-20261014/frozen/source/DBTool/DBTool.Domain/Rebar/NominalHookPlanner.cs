using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>VHG schedule dimensions. Nominal corners are explicitly not a fabrication centerline.</summary>
public static class NominalHookPlanner
{
    public static OperationResult<RebarLayout> Apply(ValidatedManholeSpec spec, GeometryModel geometry,
        IEnumerable<BarOccurrence> input, MainRebarInput main)
    {
        var bars = new List<BarOccurrence>();
        var diagnostics = new List<Diagnostic>();
        foreach (var bar in input)
        {
            string member = bar.ComponentKind switch
            {
                ComponentKind.Base => bar.Layer == Layer.Bottom ? "BaseBottom" : "BaseTop",
                ComponentKind.Roof => bar.Layer == Layer.Bottom ? "RoofBottom" : "RoofTop",
                ComponentKind.Wall => bar.Surface == RebarSurface.Inner ? "WallInner" : "WallOuter",
                ComponentKind.Neck => bar.Surface == RebarSurface.Inner ? "NeckInner" : "NeckOuter",
                _ => ""
            };
            var pattern = member switch
            {
                "BaseBottom" => spec.Input.HookBaseBottom,
                "BaseTop" => spec.Input.HookBaseTop,
                _ => main.Hooks.GetValueOrDefault(member, HookPattern.Hook0M)
            };
            if (pattern == HookPattern.Hook0M || bar.FamilyId == "Ladder" || bar.FamilyId == HaunchRebarPlanner.FamilyId || bar.FamilyId == "Dowel")
            {
                bars.Add(bar);
                continue;
            }
            var component = geometry.Components.Single(x => x.Id == bar.ComponentId);
            var face = member switch
            {
                "WallInner" => main.WallInner,
                "WallOuter" => main.WallOuter,
                "NeckInner" => main.NeckInner,
                "NeckOuter" => main.NeckOuter,
                _ => null
            };
            double cover = face?.CoverMm ?? (bar.ComponentKind == ComponentKind.Base ? spec.Input.CoverMm : main.RoofCoverMm);
            double thickness = bar.ComponentKind switch
            {
                ComponentKind.Base => spec.Input.BaseThicknessMm,
                ComponentKind.Roof => spec.Input.RoofThicknessMm,
                ComponentKind.Neck => spec.Input.NeckSpec!.ThicknessMm,
                _ => spec.Input.WallThicknessMm
            };
            var lengths = HookResolver.Resolve(pattern, main.HookMode, thickness, cover, cover, bar.DiameterMm, main.HookMultiple, bar.Sources.FirstOrDefault());
            if (!lengths.Success) return new(false, null, lengths.Diagnostics);

            if (thickness - 2 * cover < 0)
            {
                diagnostics.Add(new Diagnostic("HOOK_NO_ROOM", DiagnosticSeverity.Block, DiagnosticScope.Manhole,
                    spec.Input.Id, null, bar.Sources.FirstOrDefault(), ">=0 hook nominal clearance",
                    $"{thickness - 2 * cover}", "Bar cover envelope has no room for nominal hook."));
                return new(false, null, diagnostics);
            }

            var first = bar.Path.Segments[0].Start;
            var last = bar.Path.Segments[^1].End;
            double q = cover + bar.DiameterMm / 2d;
            double min, max, a, b;
            if (bar.Direction == RebarDirection.Vertical)
            {
                min = component.ZRange.Start + q;
                if (spec.Input.MainRebar is not null)
                    min = bar.ComponentKind == ComponentKind.Wall
                        ? -spec.Input.BaseThicknessMm + spec.Input.CoverMm + bar.DiameterMm / 2d
                        : spec.Input.H + main.RoofCoverMm + bar.DiameterMm / 2d;
                max = component.ZRange.End - q;
                if (bar.ComponentKind == ComponentKind.Neck && spec.Input.NeckSeatSpec?.Kind == NeckSeatKind.Step)
                    max = spec.Input.NeckSeatSpec.SeatElevationMm - q;
                a = first.Z;
                b = last.Z;
            }
            else
            {
                var points = (bar.ComponentKind == ComponentKind.Base && bar.RuleId == "SLAB-REBAR-S2-001"
                    ? SlabRebarPlanner.S2BaseDesignBoundary(spec.Input) : component.Footprint.Outer).Vertices;
                min = (bar.Axis == Axis.X ? points.Min(x => x.X) : points.Min(x => x.Y)) + q;
                max = (bar.Axis == Axis.X ? points.Max(x => x.X) : points.Max(x => x.Y)) - q;
                if (bar.ComponentKind == ComponentKind.Neck && bar.Axis == Axis.Y)
                {
                    min += spec.Input.NeckSpec!.ThicknessMm;
                    max -= spec.Input.NeckSpec.ThicknessMm;
                }
                a = bar.Axis == Axis.X ? first.X : first.Y;
                b = bar.Axis == Axis.X ? last.X : last.Y;
            }
            // New clipped endpoints never acquire a hook; only original outer endpoints retain it.
            bool atStart = Math.Abs(a - min) < 1e-6, atEnd = Math.Abs(b - max) < 1e-6;
            double start = atStart ? lengths.Value!.StartMm : 0, end = atEnd ? lengths.Value!.EndMm : 0;
            if (start == 0 && end == 0) { bars.Add(bar); continue; }

            Point3Mm Offset(Point3Mm p, double length)
            {
                if (bar.ComponentKind is ComponentKind.Base or ComponentKind.Roof)
                    return new(p.X, p.Y, p.Z + (bar.Layer == Layer.Bottom ? length : -length));
                bool positive = bar.FrameId?.EndsWith("T1", StringComparison.Ordinal) == true || bar.FrameId?.EndsWith("T2", StringComparison.Ordinal) == true;
                double sign = (bar.Surface == RebarSurface.Inner ? 1 : -1) * (positive ? 1 : -1);
                return bar.FrameId?.EndsWith("T1", StringComparison.Ordinal) == true || bar.FrameId?.EndsWith("T3", StringComparison.Ordinal) == true
                    ? new(p.X, p.Y + sign * length, p.Z) : new(p.X + sign * length, p.Y, p.Z);
            }
            var segments = new List<PathSegment>();
            if (start > 0) segments.Add(new LineSegment3Mm(Offset(first, start), first));
            segments.AddRange(bar.Path.Segments);
            if (end > 0) segments.Add(new LineSegment3Mm(last, Offset(last, end)));
            var actual = start > 0 ? (end > 0 ? HookPattern.Hook2M : HookPattern.HookStartOnly) : HookPattern.Hook1M;
            var path = new BarPath(segments, false, actual, 0, false, MeasurementBasis.NominalPolyline);
            var fabrication = NominalFabricationLength.Calculate(path, bar.DiameterMm);
            bars.Add(new(bar.Id, bar.ParentId, bar.ComponentId, bar.ComponentKind, bar.Layer, bar.Axis, bar.DiameterMm,
                path, bar.ScheduleLengthMm + start + end, fabrication, fabrication.HasValue ? LengthStatus.Known : LengthStatus.NeedsRule,
                "VHG-NOMINAL-HOOK-1B", bar.Sources.Concat(main.Sources.Values).Distinct(),
                bar.Direction, bar.FrameId, bar.Surface, bar.FamilyId, MeasurementBasisKind.Nominal, null,
                bar.StationIndex, bar.StationMm, bar.AssemblyId, bar.PanelInstanceId, bar.RegionRole,
                bar.OpeningIds, bar.StraightRunMm ?? bar.ScheduleLengthMm, "VHG-NOMINAL-HOOK-1B",
                bar.SpecId, bar.StationKey, "NominalPolyline"));
        }
        if (bars.Any(x => x.LengthStatus == LengthStatus.NeedsRule && x.RuleId != "VHG-NOMINAL-HOOK-1B"))
            diagnostics.Add(new("HOOK_INPUT_LENGTH_UNRESOLVED", DiagnosticSeverity.Review, DiagnosticScope.Manhole,
                spec.Input.Id, null, bars.First(x => x.LengthStatus == LengthStatus.NeedsRule).Sources.FirstOrDefault(),
                "confirmed upstream fabrication rule", null,
                "Member hook composition preserves upstream unresolved lengths; it cannot certify supplementary bar detailing."));
        if (bars.Any(x => x.RuleId == "VHG-NOMINAL-HOOK-1B"))
            diagnostics.Add(new("HOOK_FABRICATION_FALLBACK", DiagnosticSeverity.Review, DiagnosticScope.Manhole, spec.Input.Id, null,
                main.Sources.Values.FirstOrDefault(),"bend radius and fabrication basis", null,
                "VHG schedule uses nominal legs. Internal fabrication uses owner-approved ribbed-bar radius fallback; infeasible bends remain unresolved for review."));
        return new(true, new RebarLayout(bars, diagnostics, CalculationScope.FullRebar,
            bars.Any(x => x.LengthStatus == LengthStatus.NeedsRule) ? LayoutStatus.Partial : LayoutStatus.Complete), diagnostics);
    }
}
