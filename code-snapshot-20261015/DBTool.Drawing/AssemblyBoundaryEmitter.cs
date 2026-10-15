#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Drawing.PlanarRegions;

namespace DBTool.Drawing;

/// <summary>
/// Emits assembly boundary regions and CSG section/projection outlines for drawing views.
/// Implements H06/H26: section CSG, visible surface regions, owned voids, and material boundaries.
/// Includes exact 2D planar CSG boolean operations for line and circular arc boundary segments.
/// </summary>
public sealed class AssemblyBoundaryEmitter
{
    private readonly IPlanarOccupiedRegionKernel _kernel;
    // Explicit analytic composition: this is a supported provider, never a native-failure fallback.
    public AssemblyBoundaryEmitter() : this(new AnalyticPlanarOccupiedRegionKernel()) { }
    public AssemblyBoundaryEmitter(IPlanarOccupiedRegionKernel kernel) =>
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));

    

    /// <summary>
    /// Emits 2D regions for the provided views based on 3D geometry and openings.
    /// </summary>
    public Region2V2[] Emit(GeometryV2 geometry, OpeningV2[]? manufacturingHoles, ViewV2[] views, RulesV2 rules)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(views);

        var tolerance = Tolerance(rules);
        var components = geometry.components ?? [];
        if(components.Any(component=>component is null))
            throw GeometryFault("CSG_GEOMETRY_UNRESOLVED","components","non-null components",tolerance.SourceIds);
        if(components.Select(c=>c.id).Distinct(StringComparer.Ordinal).Count()!=components.Length)
            throw GeometryFault("CSG_OWNERSHIP_INVALID","Components","unique component IDs", tolerance.SourceIds);
        if(components.Any(component=>component is null))
            throw GeometryFault("CSG_GEOMETRY_UNRESOLVED","components","non-null components",tolerance.SourceIds);
        if(views.Any(view=>view is null || string.IsNullOrWhiteSpace(view.id)) || views.Select(view=>view.id).Distinct(StringComparer.Ordinal).Count()!=views.Length)
            throw GeometryFault("CSG_ID_INVALID","views","non-null views with unique nonempty IDs",tolerance.SourceIds);
        foreach(var component in geometry.components ?? []) {
            if(component.source_ids is null || component.source_ids.Length==0 || component.source_ids.Any(string.IsNullOrWhiteSpace))
                throw GeometryFault("CSG_SOURCE_UNRESOLVED",component.id,"component source IDs", tolerance.SourceIds);
            if(new[]{component.id,component.assembly_id,component.material_id}.Any(string.IsNullOrWhiteSpace))
                throw GeometryFault("CSG_OWNERSHIP_INVALID",component.id,"exact component/assembly/material IDs", component.source_ids);
            ValidateFrame(component.frame, tolerance with {SourceIds=component.source_ids.Concat(tolerance.SourceIds).ToArray()});
            if((component.loops?.Length ?? 0)==0 && (component.solid_parts?.Length ?? 0)==0)
                throw GeometryFault("CSG_GEOMETRY_UNRESOLVED",component.id,"explicit geometry or ordered operation producing Empty",
                    component.source_ids,"loops=0; solid_parts=0","component.geometry");
            if (!double.IsFinite(component.z_min_mm) || !double.IsFinite(component.z_max_mm)
                || component.z_min_mm >= component.z_max_mm)
                throw GeometryFault("CSG_DEPTH_INVALID", component.id, "finite min < max", component.source_ids,
                    "min_mm="+component.z_min_mm+"; max_mm="+component.z_max_mm,"component.depth");
            AnalyticPlanarOccupiedRegionKernel.ValidateLoops(component.loops ?? [], tolerance with {SourceIds=component.source_ids.Concat(tolerance.SourceIds).ToArray()});
            if((component.solid_parts ?? []).Any(part=>part is null || string.IsNullOrWhiteSpace(part.id))
                || (component.solid_parts ?? []).Select(part=>part.id).Distinct(StringComparer.Ordinal).Count()!=(component.solid_parts?.Length ?? 0))
                throw GeometryFault("CSG_ID_INVALID",component.id,"non-null uniquely identified ordered solid parts",component.source_ids);
            foreach(var part in component.solid_parts ?? []) {
                ValidateFrame(part.frame, tolerance with {SourceIds=component.source_ids.Concat(tolerance.SourceIds).ToArray()});
                if (!double.IsFinite(part.depth_min_mm) || !double.IsFinite(part.depth_max_mm)
                    || part.depth_min_mm >= part.depth_max_mm)
                    throw GeometryFault("CSG_DEPTH_INVALID", part.id, "finite min < max", component.source_ids,
                        "min_mm="+part.depth_min_mm+"; max_mm="+part.depth_max_mm,"solid_parts.depth");
                if(part.operation is not ("Union" or "Subtract" or "Intersect"))
                    throw GeometryFault("CSG_OPERATION_INVALID", part.id, "Union/Subtract/Intersect", tolerance.SourceIds);
                AnalyticPlanarOccupiedRegionKernel.ValidateLoops(part.loops ?? [], tolerance with {SourceIds=component.source_ids.Concat(tolerance.SourceIds).ToArray()});
            }
        }
        foreach(var view in views) {
            ValidateFrame(view.frame, tolerance);
            if(view.section_offset_mm is double offset && !double.IsFinite(offset))
                throw GeometryFault("CSG_DEPTH_INVALID",view.id,"finite section offset", tolerance.SourceIds);
        }
        var allOpenings = (geometry.openings ?? [])
            .Concat(manufacturingHoles ?? [])
            .ToArray();

        if(allOpenings.Any(opening=>opening is null || string.IsNullOrWhiteSpace(opening.id))
            || allOpenings.Select(opening=>opening.id).Distinct(StringComparer.Ordinal).Count()!=allOpenings.Length)
            throw GeometryFault("CSG_ID_INVALID","openings","non-null unique opening IDs",tolerance.SourceIds);
        foreach(var opening in allOpenings) {
            if(opening.source_ids is null||opening.source_ids.Length==0||opening.source_ids.Any(string.IsNullOrWhiteSpace))
                throw GeometryFault("CSG_SOURCE_UNRESOLVED",opening.id,"opening source IDs", tolerance.SourceIds);
            ValidateFrame(opening.frame, tolerance with {SourceIds=opening.source_ids.Concat(tolerance.SourceIds).ToArray()});
            if(!double.IsFinite(opening.depth_min_mm)||!double.IsFinite(opening.depth_max_mm)||opening.depth_min_mm>=opening.depth_max_mm)
                throw GeometryFault("CSG_DEPTH_INVALID",opening.id,"finite min < max", opening.source_ids,
                    "min_mm="+opening.depth_min_mm+"; max_mm="+opening.depth_max_mm,"opening.depth");
            if((opening.concrete_cutout?.Length ?? 0)==0 && (opening.inner?.Length ?? 0)==0)
                throw GeometryFault("CSG_GEOMETRY_UNRESOLVED",opening.id,"explicit opening silhouette",opening.source_ids);
            AnalyticPlanarOccupiedRegionKernel.ValidateLoops(opening.concrete_cutout?.Length>0?opening.concrete_cutout:opening.inner ?? [],tolerance with {SourceIds=opening.source_ids.Concat(tolerance.SourceIds).ToArray()});
            if(opening.host_ids is null || opening.host_ids.Length==0
                || opening.host_ids.Any(id=>components.Count(c=>c.id==id)!=1))
                throw GeometryFault("CSG_OWNERSHIP_INVALID",opening.id,"each host resolves exactly once", opening.source_ids);
        }
        var result = new List<Region2V2>();

        foreach (var view in views)
        {
            var viewRegions = RegionsForView(view, geometry, allOpenings, rules);
            result.AddRange(viewRegions);
        }

        return result.ToArray();
    }

    private List<Region2V2> RegionsForView(
        ViewV2 view,
        GeometryV2 geometry,
        OpeningV2[] openings,
        RulesV2 rules)
    {
        var result = new List<Region2V2>();
        var components = geometry.components ?? [];
        var cuts = view.component_cuts ?? [];
        if(cuts.Select(c=>c.component_id).Distinct(StringComparer.Ordinal).Count()!=cuts.Length)
            throw GeometryFault("CSG_OWNERSHIP_INVALID",view.id,"unique component cuts per view",Tolerance(rules).SourceIds,
                string.Join(",",cuts.Select(c=>c.component_id)),"view.component_cuts");

        var sectionGroups = new Dictionary<string, (string AssemblyId, string MaterialId, string FrameId, string MaterialRole, List<string> ComponentIds, List<LoopV2[]> Loops)>(StringComparer.Ordinal);
        var projectedComponents = new List<ComponentV2>();

        foreach (var cut in cuts)
        {
            var component = components.FirstOrDefault(c => string.Equals(c.id, cut.component_id, StringComparison.Ordinal));
            if (component == null) throw GeometryFault("CSG_OWNERSHIP_INVALID",cut.component_id,"unique existing component", Tolerance(rules).SourceIds);
            ValidateFrame(cut.frame,Tolerance(rules));
            if(cut.mode is not ("Projection" or "TrueSection"))
                throw GeometryFault("CSG_CUT_KIND_INVALID",cut.component_id,"Projection or TrueSection",component.source_ids,
                    cut.mode,"component_cuts.mode");

            if (string.Equals(cut.mode, "Projection", StringComparison.OrdinalIgnoreCase))
            {
                projectedComponents.Add(component);
            }
            else
            {
                if(view.section_offset_mm is not double sectionOffset)
                    throw GeometryFault("CSG_SECTION_OFFSET_UNRESOLVED",view.id,"explicit active section offset in mm",component.source_ids,
                        "missing","view.section_offset_mm");
                var loops = SectionCsg(component, openings, cut.frame, sectionOffset, rules, view.id);

                string groupKey = $"{component.assembly_id}|{component.material_id}|{cut.frame.id}";
                if (!sectionGroups.TryGetValue(groupKey, out var entry))
                {
                    entry = (component.assembly_id, component.material_id, cut.frame.id, component.material_role ?? "Concrete", [], []);
                    sectionGroups[groupKey] = entry;
                }

                if (!entry.ComponentIds.Contains(component.id))
                    entry.ComponentIds.Add(component.id);
                entry.Loops.Add(loops);
            }
        }

        // Emit TrueSection regions
        foreach (var (key, data) in sectionGroups)
        {
            var unionedLoops = new List<LoopV2>();
            for(int i=0;i<data.Loops.Count;i++)
                unionedLoops = Boolean(unionedLoops, data.Loops[i], "Union", rules,
                    data.AssemblyId,data.MaterialId,view.id,data.ComponentIds[i],components.Single(c=>c.id==data.ComponentIds[i]).source_ids);
            string regionId = Encode(view.id, data.AssemblyId, data.MaterialId, data.FrameId, "Region");

            result.Add(new Region2V2(
                id: regionId,
                view_id: view.id,
                assembly_id: data.AssemblyId,
                material_id: data.MaterialId,
                component_ids: data.ComponentIds.ToArray(),
                loops: unionedLoops.ToArray(),
                projection_frame_id: data.FrameId,
                surface_depth_mm: null,
                material_role: data.MaterialRole,
                representation_kind: "TrueSection",
                visible_surface_id: null
            ));
        }

        // Emit Projection (Visible) regions for projected components (e.g. plans)
        if (projectedComponents.Count > 0)
        {
            var projectedGroups = new Dictionary<string, (string AssemblyId, string MaterialId, string SurfaceId, string MaterialRole, double? SurfaceDepth, List<string> ComponentIds, List<LoopV2[]> Loops)>(StringComparer.Ordinal);

            foreach (var comp in projectedComponents)
            {
                var compLoops = CollectProjectedLoops(comp, openings, view.frame, rules, view.id);

                string surfaceId = comp.id + "/Exposed";
                string groupKey = $"{comp.assembly_id}|{comp.material_id}|{surfaceId}";

                if (!projectedGroups.TryGetValue(groupKey, out var entry))
                {
                    double? surfaceDepth = Math.Abs(comp.z_max_mm) > 0 ? comp.z_max_mm : null;
                    entry = (comp.assembly_id, comp.material_id, surfaceId, comp.material_role ?? "Concrete", surfaceDepth, [], []);
                    projectedGroups[groupKey] = entry;
                }

                if (!entry.ComponentIds.Contains(comp.id))
                    entry.ComponentIds.Add(comp.id);
                entry.Loops.Add(compLoops.ToArray());
            }

            foreach (var (key, data) in projectedGroups)
            {
                var unionedLoops = new List<LoopV2>();
            for(int i=0;i<data.Loops.Count;i++)
                unionedLoops = Boolean(unionedLoops, data.Loops[i], "Union", rules,
                    data.AssemblyId,data.MaterialId,view.id,data.ComponentIds[i],components.Single(c=>c.id==data.ComponentIds[i]).source_ids);
                string regionId = Encode(view.id, data.AssemblyId, data.MaterialId, data.SurfaceId, "VisibleRegion");

                result.Add(new Region2V2(
                    id: regionId,
                    view_id: view.id,
                    assembly_id: data.AssemblyId,
                    material_id: data.MaterialId,
                    component_ids: data.ComponentIds.ToArray(),
                    loops: unionedLoops.ToArray(),
                    projection_frame_id: view.frame.id,
                    surface_depth_mm: data.SurfaceDepth,
                    material_role: data.MaterialRole,
                    representation_kind: "Projection",
                    visible_surface_id: data.SurfaceId
                ));
            }
        }

        return result;
    }

    private LoopV2[] SectionCsg(ComponentV2 component,OpeningV2[] openings,FrameV2 viewFrame,
        double planeOffset,RulesV2? rules,string viewId)
    {
        var tolerance=Tolerance(rules);
        var ops=new List<PlanarRegionOperation>();
        var parts=component.solid_parts?.Length>0 ? component.solid_parts :
            [new SolidPartV2(component.id,"Union",component.frame,component.loops ?? [],component.z_min_mm,component.z_max_mm)];
        foreach(var part in parts)
            ops.Add(new(ops.Count,part.id,part.operation,SectionPart(part,viewFrame,planeOffset,tolerance),component.source_ids));
        foreach(var opening in openings.Where(o=>o.host_ids.Contains(component.id))) {
            var cutout=OpeningFootprint(opening);
            var part=new SolidPartV2(opening.id,"Subtract",opening.frame,cutout,opening.depth_min_mm,opening.depth_max_mm);
            ops.Add(new(ops.Count,opening.id,"Subtract",SectionPart(part,viewFrame,planeOffset,tolerance),opening.source_ids));
        }
        return _kernel.Execute(new(Encode(viewId,component.id,"Section"),component.assembly_id,component.material_id,viewId,
            [new(component.id,ops.ToArray(),component.source_ids)],tolerance,component.source_ids)).Loops;
    }
    private static LoopV2[] OpeningFootprint(OpeningV2 opening) {
        var loops=opening.concrete_cutout?.Length>0?opening.concrete_cutout:opening.inner ?? [];
        // V2 inner denotes the opening silhouette, rather than a hole of the cutter operand.
        return loops.Select(l=>l with {role="Outer"}).ToArray();
    }

    private LoopV2[] SectionPart(
        SolidPartV2 part,
        FrameV2 viewFrame,
        double planeOffset,
        PlanarRegionTolerance tolerance)
    {
        var n = viewFrame.normal;
        var f = part.frame;

        double a = Dot(n, f.axis_u);
        double b = Dot(n, f.axis_v);
        double c = Dot(n, f.normal);

        var originDiff = new Vector3V2(
            f.origin.x_mm - viewFrame.origin.x_mm,
            f.origin.y_mm - viewFrame.origin.y_mm,
            f.origin.z_mm - viewFrame.origin.z_mm);
        double d = Dot(n, originDiff) - planeOffset;

        // Case 1: Section plane perpendicular to extrusion axis
        if (Math.Abs(a) <= tolerance.AngularRad && Math.Abs(b) <= tolerance.AngularRad)
        {
            if (Math.Abs(c) <= tolerance.AngularRad) return [];
            double w = -d / c;
            if (w < part.depth_min_mm - tolerance.LinearMm || w > part.depth_max_mm + tolerance.LinearMm) return [];

            return TransformLoopsToView(part.loops ?? [], part.frame, viewFrame, w, tolerance);
        }

        // Case 2: Section plane parallel to extrusion axis
        if (Math.Abs(c) <= tolerance.AngularRad)
        {
            double denom = a * a + b * b;
            if (denom <= tolerance.AngularRad) return [];

            var intersections = IntersectLineWithPartFootprint(a, b, d, part.loops ?? [], tolerance);
            if (intersections.Count == 0) return [];

            var result = new List<LoopV2>();
            for (int i = 0; i < intersections.Count - 1; i++)
            {
                var p0 = intersections[i];
                var p1 = intersections[i + 1];
                if (!AnalyticPlanarOccupiedRegionKernel.Contains(part.loops ?? [],
                    (p0.X+p1.X)*.5,(p0.Y+p1.Y)*.5)) continue;

                var w0 = ToWorld(f, p0.X, p0.Y, part.depth_min_mm);
                var w1 = ToWorld(f, p1.X, p1.Y, part.depth_min_mm);
                var w2 = ToWorld(f, p1.X, p1.Y, part.depth_max_mm);
                var w3 = ToWorld(f, p0.X, p0.Y, part.depth_max_mm);

                var v0 = ToLocal2D(viewFrame, w0);
                var v1 = ToLocal2D(viewFrame, w1);
                var v2 = ToLocal2D(viewFrame, w2);
                var v3 = ToLocal2D(viewFrame, w3);

                var curves = new CurveV2[]
                {
                    CreateLineCurve($"{part.id}/Cut/{i}/E0", v0, v1, "Boundary"),
                    CreateLineCurve($"{part.id}/Cut/{i}/E1", v1, v2, "Boundary"),
                    CreateLineCurve($"{part.id}/Cut/{i}/E2", v2, v3, "Boundary"),
                    CreateLineCurve($"{part.id}/Cut/{i}/E3", v3, v0, "Boundary")
                };

                double area = SignedPolygonArea([v0, v1, v2, v3]);
                result.Add(new LoopV2($"{part.id}/Loop/{i}", "Outer", curves, true, area));
            }

            return result.ToArray();
        }

        // Case 3: Oblique plane cutting through prism
        throw GeometryFault("CSG_PROJECTION_UNSUPPORTED",part.id,"axis-aligned prism section; oblique analytic conic section requires an approved provider", tolerance.SourceIds, "a="+a+"; b="+b+"; c="+c+"; plane_offset_mm="+planeOffset, "section_plane");
    }

    private static List<Point2D> IntersectLineWithPartFootprint(double a, double b, double d, LoopV2[] loops, PlanarRegionTolerance tolerance)
    {
        double linearTol=tolerance.LinearMm;
        var hits = new List<Point2D>();

        foreach (var loop in loops)
        {
            foreach (var curve in loop.curves ?? [])
            {
                if (string.Equals(curve.kind, "Arc", StringComparison.OrdinalIgnoreCase))
                {
                    var pStart = new Point2D(curve.start.x_mm, curve.start.y_mm);
                    var pEnd = new Point2D(curve.end.x_mm, curve.end.y_mm);
                    var pCenter = new Point2D(curve.center!.x_mm, curve.center.y_mm);
                    double radius = curve.radius_mm!.Value;
                    double sweep = AnalyticPlanarOccupiedRegionKernel.SignedSweep(curve);

                    double startAngle = Math.Atan2(pStart.Y - pCenter.Y, pStart.X - pCenter.X);

                    double normSq = a * a + b * b;
                    if (normSq <= 1e-12) continue;
                    double norm = Math.Sqrt(normSq);

                    // Signed distance from center to line a*X + b*Y + d = 0
                    double dist = (a * pCenter.X + b * pCenter.Y + d) / norm;
                    if (Math.Abs(dist) <= radius + linearTol)
                    {
                        // Projection of center onto line
                        double projX = pCenter.X - (a * (a * pCenter.X + b * pCenter.Y + d)) / normSq;
                        double projY = pCenter.Y - (b * (a * pCenter.X + b * pCenter.Y + d)) / normSq;

                        double hSq = radius * radius - dist * dist;
                        double h = Math.Sqrt(Math.Max(0.0, hSq));

                        double ux = -b / norm;
                        double uy = a / norm;

                        Point2D[] candidates = h > linearTol
                            ? [new Point2D(projX + h * ux, projY + h * uy), new Point2D(projX - h * ux, projY - h * uy)]
                            : [new Point2D(projX, projY)];

                        foreach (var pt in candidates)
                        {
                            double ang = Math.Atan2(pt.Y - pCenter.Y, pt.X - pCenter.X);
                            // Positional domain tolerance is converted from mm to radians.
                            if (IsAngleOnArc(ang, startAngle, sweep, linearTol/radius))
                            {
                                hits.Add(pt);
                            }
                        }
                    }
                }
                else
                {
                    var p1 = new Point2D(curve.start.x_mm, curve.start.y_mm);
                    var p2 = new Point2D(curve.end.x_mm, curve.end.y_mm);

                    double v1 = a * p1.X + b * p1.Y + d;
                    double v2 = a * p2.X + b * p2.Y + d;

                    if (Math.Abs(v1) <= linearTol && Math.Abs(v2) <= linearTol)
                    {
                        hits.Add(p1);
                        hits.Add(p2);
                    }
                    else if (v1 * v2 <= 0)
                    {
                        double t = v1 / (v1 - v2);
                        hits.Add(new Point2D(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y)));
                    }
                }
            }
        }

        double normDir = Math.Sqrt(a*a+b*b);
        var ordered = hits.OrderBy(p => (-b*p.X+a*p.Y)/normDir).ToList();
        var distinct = new List<Point2D>();
        foreach(var p in ordered)
            if(distinct.Count==0 || Distance(distinct[^1],p)>linearTol) distinct.Add(p);
        return distinct;
    }

    private List<LoopV2> CollectProjectedLoops(ComponentV2 component,OpeningV2[] openings,FrameV2 viewFrame,RulesV2? rules,string viewId)
    {
        var tolerance=Tolerance(rules);
        // Projection currently names the component's declared exposed face.
        // Evaluate ordered occupied geometry at that actual plane before projecting;
        // projecting every prism first loses all depth constraints on Intersect/Subtract.
        double alignment=Dot(component.frame.normal,viewFrame.normal);
        if(Math.Abs(Math.Abs(alignment)-1)>tolerance.AngularRad)
            throw GeometryFault("CSG_PROJECTION_UNSUPPORTED",component.id,
                "parallel declared exposed-face projection",component.source_ids,
                "normal_dot="+alignment,"projection.frame");
        double depth=alignment>0?component.z_max_mm:component.z_min_mm;
        var surface=ToWorld(component.frame,0,0,depth);
        double offset=Dot(new Vector3V2(surface.x_mm-viewFrame.origin.x_mm,
            surface.y_mm-viewFrame.origin.y_mm,surface.z_mm-viewFrame.origin.z_mm),viewFrame.normal);
        var parts=component.solid_parts ?? [];
        // Multiple exposed union levels require distinct visible-surface depth records.
        // Never collapse such levels into a single guessed front face.
        foreach(var part in parts.Where(part=>part.operation=="Union")) {
            double partAlignment=Dot(part.frame.normal,viewFrame.normal);
            if(Math.Abs(Math.Abs(partAlignment)-1)>tolerance.AngularRad)
                throw GeometryFault("CSG_PROJECTION_UNSUPPORTED",part.id,
                    "parallel analytic exposed surface",component.source_ids,"normal_dot="+partAlignment,"projection.solid_parts.frame");
            var partSurface=ToWorld(part.frame,0,0,partAlignment>0?part.depth_max_mm:part.depth_min_mm);
            double partOffset=Dot(new Vector3V2(partSurface.x_mm-viewFrame.origin.x_mm,
                partSurface.y_mm-viewFrame.origin.y_mm,partSurface.z_mm-viewFrame.origin.z_mm),viewFrame.normal);
            if(Math.Abs(partOffset-offset)>tolerance.LinearMm && parts.Count(part=>part.operation=="Union")>1)
                throw GeometryFault("CSG_PROJECTION_LAYER_UNRESOLVED",component.id,
                    "distinct visible-surface layers before merging projection",component.source_ids,
                    "declared_face_offset_mm="+offset+"; union_face_offset_mm="+partOffset,"projection.visible_surface_depth");
        }
        return SectionCsg(component,openings,viewFrame,offset,rules,viewId).ToList();
    }

    /// <summary>
    /// Transforms 3D loops to target view frame while strictly preserving circular arcs without chordizing.
    /// </summary>
    private static LoopV2[] TransformLoopsToView(
        LoopV2[] loops,
        FrameV2 sourceFrame,
        FrameV2 targetFrame,
        double localDepth,
        PlanarRegionTolerance tolerance)
    {
        var result = new List<LoopV2>();

        foreach (var loop in loops)
        {
            var curves = new List<CurveV2>();
            double totalSignedArea = 0.0;

            foreach (var curve in loop.curves ?? [])
            {
                var wStart = ToWorld(sourceFrame, curve.start.x_mm, curve.start.y_mm, localDepth);
                var wEnd = ToWorld(sourceFrame, curve.end.x_mm, curve.end.y_mm, localDepth);

                var vStart = ToLocal2D(targetFrame, wStart);
                var vEnd = ToLocal2D(targetFrame, wEnd);

                if (string.Equals(curve.kind, "Arc", StringComparison.OrdinalIgnoreCase))
                {
                    var wCenter = ToWorld(sourceFrame, curve.center!.x_mm, curve.center.y_mm, localDepth);
                    var vCenter = ToLocal2D(targetFrame, wCenter);
                    double radius = curve.radius_mm!.Value;
                    double sourceSweep = AnalyticPlanarOccupiedRegionKernel.SignedSweep(curve);
                    double det = Dot(sourceFrame.axis_u,targetFrame.axis_u)*Dot(sourceFrame.axis_v,targetFrame.axis_v)
                        - Dot(sourceFrame.axis_u,targetFrame.axis_v)*Dot(sourceFrame.axis_v,targetFrame.axis_u);
                    if(Math.Abs(Math.Abs(det)-1)>tolerance.AngularRad)
                        throw GeometryFault("CSG_PROJECTION_UNSUPPORTED",curve.id,"parallel circular-arc projection; analytic ellipse provider required", tolerance.SourceIds, "source_frame="+sourceFrame+"; target_frame="+targetFrame+"; curve="+curve.id, "curves["+curve.id+"].projection");
                    double sweep = sourceSweep*Math.Sign(det);

                    var arcCurve = new CurveV2(
                        id: curve.id,
                        kind: "Arc",
                        start: new Point3V2(vStart.X, vStart.Y, 0),
                        end: new Point3V2(vEnd.X, vEnd.Y, 0),
                        center: new Point3V2(vCenter.X, vCenter.Y, 0),
                        normal: new Vector3V2(0, 0, sweep >= 0 ? 1 : -1),
                        radius_mm: radius,
                        sweep_rad: Math.Abs(sweep),
                        role: curve.role ?? "Boundary"
                    );

                    curves.Add(arcCurve);

                    double chordArea = (vStart.X * vEnd.Y - vEnd.X * vStart.Y) * 0.5;
                    double segArea = 0.5 * radius * radius * (sweep - Math.Sin(sweep));
                    totalSignedArea += (chordArea + segArea);
                }
                else
                {
                    var lineCurve = CreateLineCurve(curve.id, vStart, vEnd, curve.role ?? "Boundary");
                    curves.Add(lineCurve);
                    totalSignedArea += (vStart.X * vEnd.Y - vEnd.X * vStart.Y) * 0.5;
                }
            }

            result.Add(new LoopV2(
                id: loop.id,
                role: loop.role,
                curves: curves.ToArray(),
                closed: loop.closed,
                signed_area_mm2: totalSignedArea));
        }

        return result.ToArray();
    }

    private static DrawingStageFaultException GeometryFault(string code,string id,string expected,string[] sources,
        string actual="invalid or unsupported",string field="geometry")=>
        new(code,id,[field],expected,actual,sources);
    private static PlanarRegionTolerance Tolerance(RulesV2? rules) {
        var sources=rules?.entries?.Where(e=>e.id=="NR-18" && e.status=="Approved"&&!string.IsNullOrWhiteSpace(e.approver)&&!string.IsNullOrWhiteSpace(e.approved_at_utc))
            .SelectMany(e=>e.source_ids ?? []).Distinct().ToArray() ?? [];
        if(rules?.geometry_tolerance_mm is not double lin || rules.angle_tolerance_rad is not double ang
            || rules.area_tolerance_mm2 is not double area || rules.basis_tolerance is not double basis || !double.IsFinite(basis)||basis<=0 || !double.IsFinite(lin)||lin<=0
            || !double.IsFinite(ang)||ang<=0 || !double.IsFinite(area)||area<=0 || sources.Length==0 || sources.Any(string.IsNullOrWhiteSpace))
            throw GeometryFault("CSG_TOLERANCE_UNRESOLVED","Rules","sourced finite positive linear/angular/area tolerances", sources);
        return new(lin,ang,area,sources,basis);
    }
    private static void ValidateFrame(FrameV2 f,PlanarRegionTolerance tolerance) {
        double Len(Vector3V2 v)=>Math.Sqrt(Dot(v,v));
        bool Finite(Vector3V2 v)=>double.IsFinite(v.x)&&double.IsFinite(v.y)&&double.IsFinite(v.z);
        if(f is null || f.origin is null || f.axis_u is null || f.axis_v is null || f.normal is null || string.IsNullOrWhiteSpace(f.id) || !double.IsFinite(f.origin.x_mm)
            ||!double.IsFinite(f.origin.y_mm)||!double.IsFinite(f.origin.z_mm)
            ||!Finite(f.axis_u)||!Finite(f.axis_v)||!Finite(f.normal)
            ||Math.Abs(Len(f.axis_u)-1)>tolerance.BasisDimensionless||Math.Abs(Len(f.axis_v)-1)>tolerance.BasisDimensionless||Math.Abs(Len(f.normal)-1)>tolerance.BasisDimensionless
            ||Math.Abs(Dot(f.axis_u,f.axis_v))>tolerance.BasisDimensionless||Math.Abs(Dot(f.axis_u,f.normal))>tolerance.BasisDimensionless
            ||Math.Abs(Dot(f.axis_v,f.normal))>tolerance.BasisDimensionless || f.handedness is not (-1 or 1))
            throw GeometryFault("CSG_FRAME_INVALID",f?.id??"frame","finite orthonormal sourced frame", tolerance.SourceIds,
                f?.ToString()??"missing","frame");
        var cross=new Vector3V2(f.axis_u.y*f.axis_v.z-f.axis_u.z*f.axis_v.y,
            f.axis_u.z*f.axis_v.x-f.axis_u.x*f.axis_v.z,f.axis_u.x*f.axis_v.y-f.axis_u.y*f.axis_v.x);
        if(Math.Abs(Dot(cross,f.normal)-f.handedness)>tolerance.BasisDimensionless)
            throw GeometryFault("CSG_FRAME_INVALID",f.id,"consistent handedness", tolerance.SourceIds,
                "basis_dot_normal="+Dot(cross,f.normal)+"; declared_handedness="+f.handedness,"frame.handedness");
    }
    private List<LoopV2> Boolean(List<LoopV2> basis,LoopV2[] modifier,string operation,RulesV2? rules,
        string assemblyId="LegacyAssembly",string materialId="LegacyMaterial",string viewId="LegacyView",
        string componentId="LegacyOperand",string[]? componentSources=null) {
        var tol=Tolerance(rules);var sources=componentSources ?? tol.SourceIds;
        var request=new PlanarOccupiedRegionRequest(Encode(viewId,componentId,"Boolean"),assemblyId,materialId,viewId,
            [new(componentId,[new(0,"Basis","Union",basis.ToArray(),sources),
                new(1,"Modifier",operation=="Difference"?"Subtract":operation,modifier,sources)],sources)],tol,sources);
        return _kernel.Execute(request).Loops.ToList();
    }
    // Legacy helpers remain explicit analytic entry points; production Emit uses the injected port.
    public static List<LoopV2> RegionBoolean(IEnumerable<LoopV2> loops,string operation,RulesV2? rules=null)=>
        new AssemblyBoundaryEmitter().Boolean([],loops.ToArray(),operation,rules);
    public static List<LoopV2> RegionBoolean(List<LoopV2> basis,LoopV2[] modifier,string operation,RulesV2? rules=null)=>
        new AssemblyBoundaryEmitter().Boolean(basis,modifier,operation,rules);

    private static Point3V2 ToWorld(FrameV2 frame, double u, double v, double w)
    {
        return new Point3V2(
            frame.origin.x_mm + u * frame.axis_u.x + v * frame.axis_v.x + w * frame.normal.x,
            frame.origin.y_mm + u * frame.axis_u.y + v * frame.axis_v.y + w * frame.normal.y,
            frame.origin.z_mm + u * frame.axis_u.z + v * frame.axis_v.z + w * frame.normal.z);
    }

    private static Point2D ToLocal2D(FrameV2 frame, Point3V2 p)
    {
        var diff = new Vector3V2(
            p.x_mm - frame.origin.x_mm,
            p.y_mm - frame.origin.y_mm,
            p.z_mm - frame.origin.z_mm);

        return new Point2D(Dot(diff, frame.axis_u), Dot(diff, frame.axis_v));
    }

    private static double Dot(Vector3V2 a, Vector3V2 b) => a.x * b.x + a.y * b.y + a.z * b.z;

    private static double Distance(Point2D a, Point2D b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static CurveV2 CreateLineCurve(string id, Point2D start, Point2D end, string role)
    {
        return new CurveV2(
            id: id,
            kind: "Line",
            start: new Point3V2(start.X, start.Y, 0),
            end: new Point3V2(end.X, end.Y, 0),
            center: null,
            normal: null,
            radius_mm: null,
            sweep_rad: null,
            role: role
        );
    }

    private static double SignedPolygonArea(IReadOnlyList<Point2D> points)
    {
        if (points.Count < 3) return 0.0;
        double sum = 0.0;
        for (int i = 0; i < points.Count; i++)
        {
            var p1 = points[i];
            var p2 = points[(i + 1) % points.Count];
            sum += (p1.X * p2.Y - p2.X * p1.Y);
        }
        return sum * 0.5;
    }

    private static string Encode(params string[] parts)
    {
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            int byteCount = Encoding.UTF8.GetByteCount(p);
            sb.Append(byteCount).Append(':').Append(p);
        }
        return sb.ToString();
    }

    private static bool IsAngleOnArc(double angle,double start,double sweep,double tol) {
        double diff=(angle-start)%(2*Math.PI);
        if(sweep>0&&diff<0)diff+=2*Math.PI;
        if(sweep<0&&diff>0)diff-=2*Math.PI;
        return sweep>0?diff>=-tol&&diff<=sweep+tol:diff<=tol&&diff>=sweep-tol;
    }

    private readonly record struct Point2D(double X, double Y);

}

