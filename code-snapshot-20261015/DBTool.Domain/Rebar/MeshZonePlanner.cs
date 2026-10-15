using System.Collections.Immutable;
using DBTool.Domain.Geometry;

namespace DBTool.Domain;

/// <summary>Uniform rectangular base fixture only; no layer or junction planning.</summary>
public static class MeshZonePlanner
{
    private const string RuleId = "MESH-UNIFORM-TEST-001";

    public static OperationResult<MeshZonePlan> PlanUniformBase(
        ComponentGeometry baseComponent, MeshSpec mesh, double coverMm, CalculationPolicy policy)
    {
        if (baseComponent is null || mesh is null || policy is null)
            return Block("MESH_INPUT_REQUIRED", "Base, mesh and policy are required.", null);
        SourceRef? source = baseComponent.Sources.FirstOrDefault();
        if (baseComponent.Kind != ComponentKind.Base)
            return Block("MESH_BASE_COMPONENT_REQUIRED", "Expected a Base component.", source);
        if (policy.PolicyId is not ("S1-Analytic-1" or "S2Rebar-1") || policy.MeshJunctionPolicy != MeshJunctionPolicy.UniformBaseTestOnly ||
            !policy.Rules.Any(r => r.RuleId == RuleId))
            return Block("MESH_POLICY_UNSUPPORTED", "Uniform base policy is not selected.", source);
        if (!double.IsFinite(coverMm) || coverMm < 0)
            return Block("MESH_INVALID_COVER", "Cover must be finite and nonnegative.", source);
        if (!baseComponent.Voids.IsEmpty || !baseComponent.Footprint.InnerLoops.IsEmpty)
            return Block("MESH_OPENING_UNSUPPORTED", "Base opening requires a separate mesh policy.",
                baseComponent.Voids.SelectMany(v => v.Sources).FirstOrDefault() ?? source);

        if (baseComponent.Occupied is not ExtrusionSolid solid ||
            solid.Depth != baseComponent.ZRange ||
            solid.Frame.Origin != new Point3Mm(0, 0, 0) ||
            solid.Frame.U != new Vector3(1, 0, 0) ||
            solid.Frame.V != new Vector3(0, 1, 0) ||
            solid.Frame.N != new Vector3(0, 0, 1) ||
            !solid.Footprint.InnerLoops.IsEmpty ||
            !solid.Footprint.Outer.Vertices.ToHashSet().SetEquals(baseComponent.Footprint.Outer.Vertices) ||
            !TryRectangle(baseComponent.Footprint.Outer, out double minX, out double minY,
                out double maxX, out double maxY))
            return Block("MESH_JUNCTION_POLICY_REQUIRED", "Only an axis-aligned flat rectangular base is supported.", source);

        double xRunStart = minX + coverMm, xRunEnd = maxX - coverMm;
        double yRunStart = minY + coverMm, yRunEnd = maxY - coverMm;
        if (!double.IsFinite(xRunStart) || !double.IsFinite(xRunEnd) ||
            !double.IsFinite(yRunStart) || !double.IsFinite(yRunEnd) ||
            xRunEnd <= xRunStart || yRunEnd <= yRunStart)
            return Block("MESH_NO_RUN_SPAN", "Cover leaves no positive run interval.", source);

        var yDistribution = BarDistributor.DistributeSegment(maxY - minY, coverMm,
            mesh.DiameterMm, mesh.SpacingMm, policy.DistributionMode);
        if (!yDistribution.Success) return Forward(yDistribution.Diagnostics, source);
        var xDistribution = BarDistributor.DistributeSegment(maxX - minX, coverMm,
            mesh.DiameterMm, mesh.SpacingMm, policy.DistributionMode);
        if (!xDistribution.Success) return Forward(xDistribution.Diagnostics, source);

        var xZone = new MeshZone($"{baseComponent.Id}/X", baseComponent.Id, Axis.X,
            new Interval1D(minY, maxY), new Interval1D(xRunStart, xRunEnd), RuleId,
            yDistribution.Value!.PositionsMm.Select(p => minY + p), baseComponent.Sources);
        var yZone = new MeshZone($"{baseComponent.Id}/Y", baseComponent.Id, Axis.Y,
            new Interval1D(minX, maxX), new Interval1D(yRunStart, yRunEnd), RuleId,
            xDistribution.Value!.PositionsMm.Select(p => minX + p), baseComponent.Sources);
        return new(true, new MeshZonePlan([xZone, yZone]), Array.Empty<Diagnostic>());
    }

    /// <summary>Plans typed mesh zones and resolves seam ownership for the experimental S2 rebar fixture.</summary>
    public static OperationResult<MeshZoneSet> Plan(
        ComponentGeometry component, ConnectionGraph graph, MemberRebarSpec spec, JunctionPolicy junctionPolicy)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(junctionPolicy);
        SourceRef? source = component.Sources.FirstOrDefault();
        OperationResult<MeshZoneSet> Block(string code, string message) => new(false, null,
            [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
                source, "typed fixture junction input", junctionPolicy.PolicyId, message)]);

        if (graph.Zones.IsEmpty) return Block("MESH_ZONES_REQUIRED", "At least one mesh zone is required.");
        if (component.Id != spec.ComponentId)
            return Block("MESH_COMPONENT_MISMATCH", "Member rebar spec and component must identify the same owner.");
        if (!junctionPolicy.ApprovedProduction && junctionPolicy.PolicyId != JunctionPolicy.UniformFixture.PolicyId)
            return Block("U_JUNCTION", "No approved junction policy is available for this production branch.");

        var zones = new List<MeshZone>();
        foreach (var input in graph.Zones.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            if (input.ComponentId != component.Id) return Block("MESH_COMPONENT_MISMATCH", "Zone owner differs from component.");
            var layerSpecs = spec.Layers.Where(x => x.Axis == input.Axis).ToArray();
            if (layerSpecs.Length == 0) return Block("MESH_DIRECTION_REQUIRED", "No typed layer consumes this zone axis.");
            foreach (var layerSpec in layerSpecs)
            {
                var positions = input.PositionsMm;
                if (positions.IsEmpty)
                {
                    var distributed = BarDistributor.DistributeSegment(input.DistributionInterval.Length,
                        spec.CoverMm, layerSpec.DiameterMm, layerSpec.SpacingMm, DistributionMode.Equalized);
                    if (!distributed.Success) return new(false, null, distributed.Diagnostics);
                    positions = distributed.Value!.PositionsMm.Select(x => input.DistributionInterval.Start + x).ToImmutableArray();
                }
                try
                {
                    zones.Add(new MeshZone($"{input.Id}/{layerSpec.Id}", component.Id, input.Axis,
                        input.DistributionInterval, input.RunInterval, "MESH-ZONE-S2-001", positions,
                        component.Sources.Concat(input.Sources)));
                }
                catch (ArgumentException ex)
                {
                    return Block("MESH_ZONE_INVALID", ex.Message);
                }
            }
        }

        var merged = new List<MergedMeshCenter>();
        foreach (var group in graph.Zones.SelectMany(input => spec.Layers.Where(layer => layer.Axis == input.Axis)
                     .Select(layer => (input, layer)))
                 .GroupBy(x => (x.input.PhysicalIdentity, x.input.Axis, x.layer.Layer, x.layer.Direction)))
        {
            var values = group.SelectMany(x =>
            {
                var zone = zones.Single(z => z.Id == $"{x.input.Id}/{x.layer.Id}");
                return zone.PositionsMm.Select(p => (p, zone));
            }).OrderBy(x => x.p).ToArray();
            foreach (var item in values)
            {
                if (merged.Any(x => x.PhysicalIdentity == group.Key.PhysicalIdentity && x.Layer == group.Key.Layer &&
                    x.Axis == group.Key.Axis && x.Direction == group.Key.Direction &&
                    Math.Abs(x.PositionMm - item.p) <= junctionPolicy.ToleranceMm)) continue;
                merged.Add(new MergedMeshCenter(item.zone.Id, group.Key.PhysicalIdentity, group.Key.Layer,
                    group.Key.Axis, group.Key.Direction, item.p, item.zone.Sources));
            }
        }

        return new(true, new MeshZoneSet(zones, merged.OrderBy(x => x.Layer).ThenBy(x => x.Axis).ThenBy(x => x.PositionMm)), []);
    }

    private static bool TryRectangle(Polygon2Mm polygon,
        out double minX, out double minY, out double maxX, out double maxY)
    {
        minX = minY = maxX = maxY = 0;
        var vertices = polygon.Vertices;
        if (vertices.Length != 4) return false;
        minX = vertices.Min(p => p.X); maxX = vertices.Max(p => p.X);
        minY = vertices.Min(p => p.Y); maxY = vertices.Max(p => p.Y);
        if (maxX <= minX || maxY <= minY) return false;
        foreach (var vertex in vertices)
            if ((vertex.X != minX && vertex.X != maxX) ||
                (vertex.Y != minY && vertex.Y != maxY)) return false;
        return true;
    }

    private static OperationResult<MeshZonePlan> Forward(
        IEnumerable<Diagnostic> diagnostics, SourceRef? source) =>
        new(false, null, diagnostics.Select(d => new Diagnostic(d.Code, d.Severity, DiagnosticScope.Global,
            null, null, d.Source ?? source, d.Expected, d.Actual, d.Message)));

    private static OperationResult<MeshZonePlan> Block(string code, string message, SourceRef? source) =>
        new(false, null, new[] { new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, source, null, null, message) });
}
