using DBTool.Domain.Geometry;

namespace DBTool.Domain;

public static class CatchBasinRebarPlanner
{
    public static OperationResult<RebarLayout> PlanCatch(CatchBasinGeometry geometry, CatchRebarSpec spec, CalculationPolicy policy,
        WallFrame? frame = null, double centerU = 0, double hostThickness = 150, double? bottomZ = null, double? floorTopZ = null, double? hostTopZ = null)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(policy);

        var bars = new List<BarOccurrence>();
        var x = BarDistributor.DistributeSegment(spec.FloorDepthMm, spec.CoverMm, spec.DiameterMm, spec.SpacingMm, DistributionMode.Equalized);
        var y = BarDistributor.DistributeSegment(spec.FloorWidthMm, spec.CoverMm, spec.DiameterMm, spec.SpacingMm, DistributionMode.Equalized);
        if (!x.Success) return new(false, null, x.Diagnostics);
        if (!y.Success) return new(false, null, y.Diagnostics);

        double z = frame is not null && floorTopZ.HasValue
            ? floorTopZ.Value - spec.CoverMm - spec.DiameterMm / 2d
            : geometry.ChamberBottomZmm + spec.CoverMm + spec.DiameterMm / 2d;

        int index = 0;
        foreach (double offset in x.Value!.PositionsMm)
        {
            if (frame is not null)
            {
                double u0 = centerU - spec.FloorWidthMm / 2 + spec.CoverMm;
                double u1 = centerU + spec.FloorWidthMm / 2 - spec.CoverMm;
                double v = hostThickness + offset;
                var pStart = frame.ToGlobal(u0, v, z);
                var pEnd = frame.ToGlobal(u1, v, z);
                bars.Add(Create($"Catch/Floor/X/{index++:000}", ComponentKind.CatchBasin, Layer.Bottom, Axis.X, spec.DiameterMm,
                    pStart, pEnd, "CatchFloorX", spec.Sources));
            }
            else
            {
                double yy = -spec.FloorDepthMm / 2 + offset;
                bars.Add(Create($"Catch/Floor/X/{index++:000}", ComponentKind.CatchBasin, Layer.Bottom, Axis.X, spec.DiameterMm,
                    new Point3Mm(-spec.FloorWidthMm / 2 + spec.CoverMm, yy, z),
                    new Point3Mm(spec.FloorWidthMm / 2 - spec.CoverMm, yy, z),
                    "CatchFloorX", spec.Sources));
            }
        }

        index = 0;
        foreach (double offset in y.Value!.PositionsMm)
        {
            if (frame is not null)
            {
                double u = centerU - spec.FloorWidthMm / 2 + offset;
                double v0 = hostThickness + spec.CoverMm;
                double v1 = hostThickness + spec.FloorDepthMm - spec.CoverMm;
                var pStart = frame.ToGlobal(u, v0, z);
                var pEnd = frame.ToGlobal(u, v1, z);
                bars.Add(Create($"Catch/Floor/Y/{index++:000}", ComponentKind.CatchBasin, Layer.Bottom, Axis.Y, spec.DiameterMm,
                    pStart, pEnd, "CatchFloorY", spec.Sources));
            }
            else
            {
                double xx = -spec.FloorWidthMm / 2 + offset;
                bars.Add(Create($"Catch/Floor/Y/{index++:000}", ComponentKind.CatchBasin, Layer.Bottom, Axis.Y, spec.DiameterMm,
                    new Point3Mm(xx, -spec.FloorDepthMm / 2 + spec.CoverMm, z),
                    new Point3Mm(xx, spec.FloorDepthMm / 2 - spec.CoverMm, z),
                    "CatchFloorY", spec.Sources));
            }
        }

        double zOpeningStart = geometry.OpeningZRange.Start + spec.CoverMm;
        double zOpeningEnd = geometry.OpeningZRange.End - spec.CoverMm;
        double zSpan = Math.Max(0, zOpeningEnd - zOpeningStart);

        if (spec.SideHorizontal is not null)
        {
            var sh = spec.SideHorizontal;
            if (zSpan > 0 && sh.SpacingMm > 0)
            {
                var distZ = BarDistributor.DistributeSegment(zSpan, spec.CoverMm, sh.DiameterMm, sh.SpacingMm, DistributionMode.Equalized);
                if (distZ.Success && distZ.Value!.PositionsMm.Length > 0)
                {
                    int shIdx = 0;
                    foreach (double zOffset in distZ.Value.PositionsMm)
                    {
                        double zLevel = zOpeningStart + zOffset;
                        if (frame is not null)
                        {
                            double uLeft = centerU - spec.FloorWidthMm / 2 + spec.CoverMm;
                            double uRight = centerU + spec.FloorWidthMm / 2 - spec.CoverMm;
                            double v0 = hostThickness + spec.CoverMm;
                            double v1 = hostThickness + spec.FloorDepthMm - spec.CoverMm;
                            // Left side bar in V
                            var p1 = frame.ToGlobal(uLeft, v0, zLevel);
                            var p2 = frame.ToGlobal(uLeft, v1, zLevel);
                            bars.Add(Create($"Catch/CatchSideHorizontal/L/{shIdx:000}", ComponentKind.CatchBasin, Layer.Bottom, Axis.Y, sh.DiameterMm, p1, p2, "CatchSideHorizontal", spec.Sources));
                            // Right side bar in V
                            var p3 = frame.ToGlobal(uRight, v0, zLevel);
                            var p4 = frame.ToGlobal(uRight, v1, zLevel);
                            bars.Add(Create($"Catch/CatchSideHorizontal/R/{shIdx++:000}", ComponentKind.CatchBasin, Layer.Bottom, Axis.Y, sh.DiameterMm, p3, p4, "CatchSideHorizontal", spec.Sources));
                        }
                        else
                        {
                            AddOptionalBar(bars, sh, "CatchSideHorizontal", spec.FloorWidthMm, zLevel, Layer.Bottom, Axis.X, spec.Sources, shIdx++);
                        }
                    }
                }
                else
                {
                    AddOptionalBar(bars, sh, "CatchSideHorizontal", spec.FloorWidthMm, zOpeningStart, Layer.Bottom, Axis.X, spec.Sources, 0);
                }
            }
            else
            {
                AddOptionalBar(bars, sh, "CatchSideHorizontal", spec.FloorWidthMm, zOpeningStart, Layer.Bottom, Axis.X, spec.Sources, 0);
            }
        }

        if (spec.SideVertical is not null)
        {
            var sv = spec.SideVertical;
            double xSpan = spec.FloorWidthMm - 2 * spec.CoverMm;
            if (xSpan > 0 && sv.SpacingMm > 0)
            {
                var distX = BarDistributor.DistributeSegment(xSpan, spec.CoverMm, sv.DiameterMm, sv.SpacingMm, DistributionMode.Equalized);
                if (distX.Success && distX.Value!.PositionsMm.Length > 0)
                {
                    int svIdx = 0;
                    double barLen = Math.Max(0, geometry.OpeningZRange.End - geometry.OpeningZRange.Start);
                    foreach (double xOffset in distX.Value.PositionsMm)
                    {
                        double xCoord = -spec.FloorWidthMm / 2 + spec.CoverMm + xOffset;
                        var path = new BarPath([new LineSegment3Mm(new(xCoord, 0, geometry.OpeningZRange.Start), new(xCoord, 0, geometry.OpeningZRange.End))],
                            false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
                        bars.Add(new BarOccurrence($"Catch/CatchSideVertical/{svIdx++:000}", null, "CatchBasin", ComponentKind.CatchBasin,
                            Layer.Bottom, Axis.Y, sv.DiameterMm, path, barLen, barLen, LengthStatus.Known, "CATCH-REBAR-S2-001", spec.Sources,
                            RebarDirection.Vertical, "CatchBasin", null, "CatchSideVertical"));
                    }
                }
                else
                {
                    AddOptionalBar(bars, sv, "CatchSideVertical", zSpan, 0, Layer.Bottom, Axis.Y, spec.Sources, 0);
                }
            }
            else
            {
                AddOptionalBar(bars, sv, "CatchSideVertical", zSpan, 0, Layer.Bottom, Axis.Y, spec.Sources, 0);
            }
        }

        if (spec.Front is not null)
        {
            var front = spec.Front;
            if (frame is not null)
            {
                double u0 = centerU - spec.FloorWidthMm / 2 + spec.CoverMm;
                double u1 = centerU + spec.FloorWidthMm / 2 - spec.CoverMm;
                double vFront = hostThickness + spec.FloorDepthMm - spec.CoverMm;
                var p1 = frame.ToGlobal(u0, vFront, zOpeningStart);
                var p2 = frame.ToGlobal(u1, vFront, zOpeningStart);
                bars.Add(Create($"Catch/CatchFront/000", ComponentKind.CatchBasin, Layer.Bottom, Axis.X, front.DiameterMm, p1, p2, "CatchFront", spec.Sources));
            }
            else
            {
                AddOptionalBar(bars, front, "CatchFront", spec.FloorWidthMm, zOpeningStart, Layer.Bottom, Axis.X, spec.Sources, 0);
            }
        }

        var diagnostics = spec.StructuralEvidenceApproved ? Array.Empty<Diagnostic>() : new[] { new Diagnostic("CATCH_STRUCTURAL_PENDING_EVIDENCE",
            DiagnosticSeverity.Review, DiagnosticScope.Global, null, null, spec.Sources.FirstOrDefault(), "approved detail/source", null,
            "Non-floor catch-basin families remain a pending-evidence fixture.") };
        var status = spec.StructuralEvidenceApproved ? LayoutStatus.Complete : LayoutStatus.Partial;
        return new(true, new RebarLayout(bars, diagnostics, CalculationScope.FullRebar, status), diagnostics);
    }

    private static void AddOptionalBar(List<BarOccurrence> bars, MeshSpec mesh, string family, double length, double z,
        Layer layer, Axis axis, IEnumerable<SourceRef> sources, int ordinal)
    {
        var path = new BarPath([new LineSegment3Mm(new(-length / 2, 0, z), new(length / 2, 0, z))], false,
            HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        bars.Add(new BarOccurrence($"Catch/{family}/{ordinal:000}", null, "CatchBasin", ComponentKind.CatchBasin, layer, axis,
            mesh.DiameterMm, path, length, length, LengthStatus.Known, "CATCH-REBAR-S2-001", sources,
            axis == Axis.X ? RebarDirection.Horizontal : RebarDirection.Vertical, "CatchBasin", null, family));
    }

    private static BarOccurrence Create(string id, ComponentKind kind, Layer layer, Axis axis, int diameter,
        Point3Mm start, Point3Mm end, string family, IEnumerable<SourceRef> sources)
    {
        var path = new BarPath([new LineSegment3Mm(start, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        double length = Math.Sqrt(Math.Pow(end.X - start.X, 2) + Math.Pow(end.Y - start.Y, 2) + Math.Pow(end.Z - start.Z, 2));
        return new BarOccurrence(id, null, "CatchBasin", kind, layer, axis, diameter, path, length, length, LengthStatus.Known,
            "CATCH-REBAR-S2-001", sources, axis == Axis.X ? RebarDirection.Horizontal : RebarDirection.Vertical, "CatchBasin", null, family);
    }
}
