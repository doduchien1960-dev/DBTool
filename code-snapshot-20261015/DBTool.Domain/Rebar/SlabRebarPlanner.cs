using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>S1 uniform base mesh; every occurrence is one physical 0M bar.</summary>
public static class SlabRebarPlanner
{
    public static OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry,
        MeshZoneSet zones, SlabLayerPolicy layerPolicy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(layerPolicy);
        var diagnostics = new List<Diagnostic>();
        var output = new List<BarOccurrence>();
        var input = spec.Input;

        if (layerPolicy.IncludeBase)
        {
            var baseComponent = geometry.Components.SingleOrDefault(x => x.Kind == ComponentKind.Base);
            var baseZones = zones.Zones.Where(x => x.ComponentId == baseComponent?.Id).ToArray();
            if (baseComponent is null || baseZones.Length == 0)
                return Block("BASE_ZONES_REQUIRED", "Base planning requires typed base mesh zones.", input, spec.Sources.FirstOrDefault());
            var baseLayout = PlanBase(spec, geometry, new MeshZonePlan(baseZones),
                CalculationPolicy.Select("S1-Analytic-1", true).Value!);
            if (!baseLayout.Success) return new(false, null, baseLayout.Diagnostics);
            output.AddRange(baseLayout.Value!.Occurrences);
            diagnostics.AddRange(baseLayout.Value.Diagnostics);
        }

        if (layerPolicy.IncludeRoof)
        {
            var roof = geometry.Components.SingleOrDefault(x => x.Id == "Roof" && x.Kind == ComponentKind.Roof);
            if (roof is null) return Block("ROOF_COMPONENT_REQUIRED", "Roof planning requires a roof component.", input, spec.Sources.FirstOrDefault());
            var roofResult = PlanRoof(spec, roof, layerPolicy);
            if (!roofResult.Success) return new(false, null, roofResult.Diagnostics);
            output.AddRange(roofResult.Value!.Occurrences);
            diagnostics.AddRange(roofResult.Value.Diagnostics);
        }

        return new(true, new RebarLayout(output, diagnostics, CalculationScope.FullRebar), diagnostics);
    }

    public static OperationResult<RebarLayout> PlanBase(ValidatedManholeSpec spec, GeometryModel geometry,
        MeshZonePlan zones, CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(policy);
        var input = spec.Input;
        SourceRef? source = spec.Sources.FirstOrDefault();
        OperationResult<RebarLayout> Fail(string code, string message, DiagnosticScope scope = DiagnosticScope.Manhole) =>
            new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, scope, input.Id,
                scope == DiagnosticScope.Component ? "Base" : null, source, null, null, message)]);

        var baseComponent = geometry.Components.SingleOrDefault(c => c.Id == "Base" && c.Kind == ComponentKind.Base);
        if (baseComponent is null || policy.Version != spec.RuleVersion ||
            input.MeshJunctionPolicy != MeshJunctionPolicy.UniformBaseTestOnly ||
            input.LayerStackPolicy != LayerStackPolicy.TwoDirectionsTouchingTestOnly ||
            input.DistributionMode != policy.DistributionMode || input.BaseHaunch > 0)
            return Fail("BASE_POLICY_UNSUPPORTED", "Base or experimental mesh policy does not match.");
        if (!baseComponent.Voids.IsEmpty || !baseComponent.Footprint.InnerLoops.IsEmpty ||
            geometry.Openings.Any(o => o.OwnerId == baseComponent.Id))
            return Fail("BASE_OPENING_UNSUPPORTED", "Base opening needs an explicit clipping policy.", DiagnosticScope.Component);
        if (input.HookBaseBottom != HookPattern.Hook0M || input.HookBaseTop != HookPattern.Hook0M)
            return Fail("BASE_HOOK_UNSUPPORTED", "This base fixture supports straight 0M bars only.");

        double f = baseComponent.ZRange.Length, c = input.CoverMm;
        int db = input.BaseBottomMesh.DiameterMm, dt = input.BaseTopMesh.DiameterMm;
        if (f + policy.TolerancePolicy.GeometryMm < 2 * c + 2 * db + 2 * dt)
            return Fail("BASE_LAYER_COLLISION", "Two touching directions in each layer do not fit within the base.");

        var expectedBottom = MeshZonePlanner.PlanUniformBase(baseComponent, input.BaseBottomMesh, c, policy);
        if (!expectedBottom.Success) return new(false, null, expectedBottom.Diagnostics);
        if (!Equivalent(zones, expectedBottom.Value!))
            return Fail("BASE_ZONE_MISMATCH", "Provided zones do not match base geometry and bottom mesh.");
        var top = MeshZonePlanner.PlanUniformBase(baseComponent, input.BaseTopMesh, c, policy);
        if (!top.Success) return new(false, null, top.Diagnostics);

        double bottomX = baseComponent.ZRange.Start + c + db / 2d;
        double bottomY = bottomX + db;
        double topX = baseComponent.ZRange.End - c - dt / 2d;
        double topY = topX - dt;
        var output = new List<BarOccurrence>();
        foreach (var (layer, plan, mesh, xZ, yZ) in new[] {
            (Layer.Bottom, zones, input.BaseBottomMesh, bottomX, bottomY),
            (Layer.Top, top.Value!, input.BaseTopMesh, topX, topY) })
        {
            var hooks = HookResolver.Resolve(HookPattern.Hook0M, policy.HookPolicy.Mode, f, c, c,
                mesh.DiameterMm, policy.HookPolicy.DiameterMultiple, source);
            if (!hooks.Success) return new(false, null, hooks.Diagnostics);
            foreach (var zone in plan.Zones.OrderBy(z => z.Axis))
            {
                double z = zone.Axis == Axis.X ? xZ : yZ;
                int index = 0;
                foreach (double station in zone.PositionsMm)
                {
                    index++;
                    Point3Mm start = zone.Axis == Axis.X
                        ? new(zone.RunInterval.Start, station, z) : new(station, zone.RunInterval.Start, z);
                    Point3Mm end = zone.Axis == Axis.X
                        ? new(zone.RunInterval.End, station, z) : new(station, zone.RunInterval.End, z);
                    var path = new BarPath([new LineSegment3Mm(start, end)], false,
                        HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
                    var length = BarLengthResolver.Resolve(zone.RunInterval.Length + 2 * c, c, c,
                        hooks.Value!, path, source: source);
                    if (!length.Success) return new(false, null, length.Diagnostics);
                    output.Add(new BarOccurrence($"{input.Id}/Base/{layer}/{zone.Axis}/{index:000}", null,
                        baseComponent.Id, ComponentKind.Base, layer, zone.Axis, mesh.DiameterMm,
                        path, length.Value!.ScheduleLengthMm, length.Value.FabricationLengthMm,
                        length.Value.LengthStatus, "MESH-UNIFORM-TEST-001", baseComponent.Sources));
                }
            }
        }
        return new(true, new RebarLayout(output, [], CalculationScope.BaseRebarOnly), []);
    }

    public static OperationResult<RebarLayout> PlanS2Base(ValidatedManholeSpec spec,GeometryModel geometry,CalculationPolicy policy)
    {
        if(policy.PolicyId!="S2Rebar-1" || policy.Version!=spec.RuleVersion)
            return Block("BASE_POLICY_UNSUPPORTED","S2 policy/version required.",spec.Input,spec.Sources.FirstOrDefault());
        var component=geometry.Components.Single(x=>x.Id=="Base");
        // C5 expands the concrete solid, not the design mesh domain.
        var meshFootprint=new Footprint(S2BaseDesignBoundary(spec.Input),component.Footprint.InnerLoops);
        return PlanRoof(spec,component,new SlabLayerPolicy(spec.Input.BaseBottomMesh,spec.Input.BaseTopMesh,spec.Input.CoverMm,false,true),meshFootprint);
    }

    // Shared by mesh placement and nominal outer-end hook recognition. C5 only
    // expands concrete; comparing bars with that expanded edge suppresses hooks.
    internal static Polygon2Mm S2BaseDesignBoundary(ManholeInput input)
    {
        double x=input.L/2+input.WallThicknessMm+input.BaseExpansionMm;
        double y=input.B/2+input.WallThicknessMm+input.BaseExpansionMm;
        return new Polygon2Mm([new(-x,-y),new(x,-y),new(x,y),new(-x,y)]);
    }

    private static OperationResult<RebarLayout> PlanRoof(ValidatedManholeSpec spec, ComponentGeometry roof,
        SlabLayerPolicy policy, Footprint? meshFootprint = null)
    {
        var input = spec.Input;
        var source = roof.Sources.FirstOrDefault() ?? spec.Sources.FirstOrDefault();
        var points = (meshFootprint ?? roof.Footprint).Outer.Vertices;
        double minX = points.Min(x => x.X), maxX = points.Max(x => x.X);
        double minY = points.Min(x => x.Y), maxY = points.Max(x => x.Y);
        double spanX = maxX - minX, spanY = maxY - minY;
        if (!double.IsFinite(spanX) || !double.IsFinite(spanY) || spanX <= 0 || spanY <= 0)
            return Block("ROOF_RUN_INVALID", "Roof footprint has no positive rectangular run.", input, source);

        double bottomDepth=policy.BottomX.DiameterMm+policy.BottomY.DiameterMm;
        double topDepth=policy.TopX.DiameterMm+policy.TopY.DiameterMm;
        double required=2*policy.CoverMm+(policy.ActiveRoofLayers.Contains(Layer.Bottom)?bottomDepth:0)+(policy.ActiveRoofLayers.Contains(Layer.Top)?topDepth:0);
        if(roof.ZRange.Length<required) return Block("ROOF_LAYER_COLLISION","Physical roof layers cannot fit cover and both directions.",input,source);
        var bars = new List<BarOccurrence>();
        var diagnostics = new List<Diagnostic>();
        foreach (var layer in policy.ActiveRoofLayers)
        {
            var definitions = layer switch
            {
                Layer.Bottom => new[] { (Axis.X, policy.BottomX), (Axis.Y, policy.BottomY) },
                Layer.Top => new[] { (Axis.X, policy.TopX), (Axis.Y, policy.TopY) },
                _ => Array.Empty<(Axis, MeshSpec)>()
            };
            foreach (var (axis, mesh) in definitions)
            {
                if (!policy.ActiveRoofAxes.Contains(axis)) continue;
                double perpendicularSpan = axis == Axis.X ? spanY : spanX;
                var distribution = BarDistributor.DistributeSegment(perpendicularSpan, policy.CoverMm,
                    mesh.DiameterMm, mesh.SpacingMm, spec.Input.DistributionMode);
                if (!distribution.Success) return new(false, null, distribution.Diagnostics);
                double stacking=axis==Axis.Y ? (layer==Layer.Bottom?policy.BottomX.DiameterMm:policy.TopX.DiameterMm) : 0;
                double z = layer == Layer.Bottom
                    ? roof.ZRange.Start + policy.CoverMm + mesh.DiameterMm / 2d + stacking
                    : roof.ZRange.End - policy.CoverMm - mesh.DiameterMm / 2d - stacking;
                int parentIndex = 0;
                foreach (var offset in distribution.Value!.PositionsMm)
                {
                    parentIndex++;
                    double station = (axis == Axis.X ? minY : minX) + offset;
                    Point3Mm start = axis == Axis.X ? new(minX+policy.CoverMm+mesh.DiameterMm/2d, station, z) : new(station, minY+policy.CoverMm+mesh.DiameterMm/2d, z);
                    Point3Mm end = axis == Axis.X ? new(maxX-policy.CoverMm-mesh.DiameterMm/2d, station, z) : new(station, maxY-policy.CoverMm-mesh.DiameterMm/2d, z);
                    string parentId = $"{input.Id}/{roof.Id}/{layer}/{axis}/{parentIndex:000}";
                    var parent = CreateBar(parentId, null, roof, layer, axis, RebarDirection.Horizontal,
                        mesh.DiameterMm, start, end, input.SourceRefs.GetValueOrDefault(roof.Kind==ComponentKind.Base?(layer==Layer.Bottom?"Row.BaseBottomMesh":"Row.BaseTopMesh"):(layer==Layer.Bottom?"Row.RoofBottomMesh":"Row.RoofTopMesh")) ?? source);
                    var clipped = roof.Voids.Length > 1 || roof.Voids.Any(v=>v.ConcreteCutout is not RectangleProfile || v.WallFrame is not null)
                        ? ClipMixed(parent,roof,axis,station,double.IsFinite(policy.OpeningClearanceMm)?policy.OpeningClearanceMm:policy.CoverMm,input.Id,source)
                        : ClipRoof(parent, roof, axis, station, policy.OpeningClearanceMm,input.Id, source);
                    if (!clipped.Success) return new(false, null, clipped.Diagnostics);
                    bars.AddRange(clipped.Value!.Retained);
                    if (clipped.Value.Retained.Length > 1)
                        diagnostics.Add(new Diagnostic("ROOF_OPENING_CLIPPED", DiagnosticSeverity.Info, DiagnosticScope.Global,
                            null, null, source, null, clipped.Value.RemovedLengthMm.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                            "Roof parent bar was split around the registered concrete opening."));
                }
            }
        }
        return new(true, new RebarLayout(bars, diagnostics, CalculationScope.FullRebar), diagnostics);
    }

    private static OperationResult<RebarLayout> Block(string code, string message, ManholeInput input, SourceRef? source) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Manhole, input.Id, null, source,
            null, null, message)]);

    private static BarOccurrence CreateBar(string id, string? parentId, ComponentGeometry component, Layer layer,
        Axis axis, RebarDirection direction, int diameter, Point3Mm start, Point3Mm end, SourceRef? source)
    {
        var path = new BarPath([new LineSegment3Mm(start, end)], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        double length = BendLengthCalculator.Distance(start, end);
        return new BarOccurrence(id, parentId, component.Id, component.Kind, layer, axis, diameter, path, length, length,
            LengthStatus.Known, "SLAB-REBAR-S2-001", component.Sources.Concat(source is null ? [] : new[]{source}).Distinct(), direction,
            $"{component.Id}/{layer}", layer == Layer.Bottom ? RebarSurface.Inner : RebarSurface.Outer, "Slab");
    }

    private static OperationResult<ClipResult> ClipMixed(BarOccurrence parent, ComponentGeometry roof, Axis axis,
        double station,double clearance,string manholeId,SourceRef? source)
    {
        var retained=new List<BarOccurrence>{parent};
        var diagnostics=new List<Diagnostic>();
        foreach(var opening in roof.Voids.OrderBy(v=>v.Id,StringComparer.Ordinal))
        {
            var next=new List<BarOccurrence>();
            foreach(var bar in retained)
            {
                var single=new ComponentGeometry(roof.Id,roof.Kind,roof.MaterialId,roof.Footprint,roof.ZRange,
                    roof.Occupied,[opening],roof.FacePatches,roof.Sources);
                var result=opening.ConcreteCutout is RectangleProfile && opening.WallFrame is null
                    ? ClipRoof(bar,single,axis,station,clearance,manholeId,source)
                    : OpeningClipper.Clip(bar,[opening],new ClearancePolicy(clearance),new GeometryModel([single],[opening]));
                diagnostics.AddRange(result.Diagnostics);
                if(!result.Success) return new(false,null,diagnostics);
                next.AddRange(result.Value!.Retained);
            }
            retained=next;
        }
        return new(true,new ClipResult(retained,parent.ScheduleLengthMm-retained.Sum(b=>b.ScheduleLengthMm),parent.ScheduleLengthMm),diagnostics);
    }

    private static OperationResult<ClipResult> ClipRoof(BarOccurrence parent, ComponentGeometry roof, Axis axis,
        double station, double clearance, string manholeId, SourceRef? source)
    {
        var opening = roof.Voids.FirstOrDefault();
        if (opening is null) return new(true, new ClipResult([parent], 0, parent.ScheduleLengthMm), []);
        if (!double.IsFinite(clearance)) clearance = parent.DiameterMm / 2d;
        if (opening.ConcreteCutout is not RectangleProfile rectangle)
            return new(false, null, [new Diagnostic("ROOF_OPENING_UNSUPPORTED", DiagnosticSeverity.Block,
                DiagnosticScope.Component, manholeId, roof.Id, source, "rectangular opening", opening.Kind.ToString(),
                "Roof clipping requires a rectangular opening in this fixture.")]);
        var hole = rectangle.Rectangle;
        double q = clearance+parent.DiameterMm/2d;
        double a0, a1;
        if (axis == Axis.X)
        {
            if (station < hole.MinY - q || station > hole.MaxY + q)
                return new(true, new ClipResult([parent], 0, parent.ScheduleLengthMm), []);
            a0 = hole.MinX - q; a1 = hole.MaxX + q;
        }
        else
        {
            if (station < hole.MinX - q || station > hole.MaxX + q)
                return new(true, new ClipResult([parent], 0, parent.ScheduleLengthMm), []);
            a0 = hole.MinY - q; a1 = hole.MaxY + q;
        }
        var line = (LineSegment3Mm)parent.Path.Segments[0];
        double start = axis == Axis.X ? line.Start.X : line.Start.Y;
        double end = axis == Axis.X ? line.End.X : line.End.Y;
        var parts = new List<(double Start, double End)>();
        if (a0 > start) parts.Add((start, Math.Min(a0, end)));
        if (a1 < end) parts.Add((Math.Max(a1, start), end));
        var kept = parts.Where(x => x.End - x.Start > 1e-6).ToArray();
        var children = new List<BarOccurrence>();
        foreach (var part in kept)
        {
            Point3Mm p0 = axis == Axis.X ? new(part.Start, station, line.Start.Z) : new(station, part.Start, line.Start.Z);
            Point3Mm p1 = axis == Axis.X ? new(part.End, station, line.Start.Z) : new(station, part.End, line.Start.Z);
            children.Add(CreateBar($"{parent.Id}/clip/{children.Count}", parent.Id,
                new ComponentGeometry(parent.ComponentId, parent.ComponentKind, "Concrete",
                    roof.Footprint, roof.ZRange, roof.Occupied, roof.Voids, roof.FacePatches, roof.Sources),
                parent.Layer, parent.Axis, parent.Direction, parent.DiameterMm, p0, p1, source));
        }
        double removed = parent.ScheduleLengthMm - children.Sum(x => x.ScheduleLengthMm);
        return new(true, new ClipResult(children, Math.Max(0, removed), parent.ScheduleLengthMm), []);
    }

    private static bool Equivalent(MeshZonePlan actual, MeshZonePlan expected)
    {
        if (actual.Zones.Length != expected.Zones.Length) return false;
        foreach (var zone in expected.Zones)
        {
            var other = actual.Zones.SingleOrDefault(z => z.Id == zone.Id);
            if (other is null || other.ComponentId != zone.ComponentId || other.Axis != zone.Axis ||
                other.RunInterval != zone.RunInterval || other.DistributionInterval != zone.DistributionInterval ||
                other.SourceRuleId != zone.SourceRuleId || other.PositionsMm.Length != zone.PositionsMm.Length) return false;
            for (int i = 0; i < zone.PositionsMm.Length; i++)
                if (Math.Abs(other.PositionsMm[i] - zone.PositionsMm[i]) > 1e-6) return false;
        }
        return true;
    }
}
