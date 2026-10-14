#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;

namespace DBTool.Drawing;

/// <summary>
/// Emits assembly boundary regions and CSG section/projection outlines for drawing views.
/// Implements H06/H26: section CSG, visible surface regions, owned voids, and material boundaries.
/// </summary>
public sealed class AssemblyBoundaryEmitter
{
    private const double BasisTol = 1e-6;
    private const double GeomTol = 1e-4;
    private const double AreaTol = 1e-4;

    /// <summary>
    /// Emits 2D regions for the provided views based on 3D geometry and openings.
    /// </summary>
    public Region2V2[] Emit(GeometryV2 geometry, OpeningV2[]? manufacturingHoles, ViewV2[] views, RulesV2 rules)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(views);

        var allOpenings = (geometry.openings ?? [])
            .Concat(manufacturingHoles ?? [])
            .ToArray();

        var result = new List<Region2V2>();

        foreach (var view in views)
        {
            var viewRegions = RegionsForView(view, geometry, allOpenings, rules);
            result.AddRange(viewRegions);
        }

        return result.ToArray();
    }

    private static List<Region2V2> RegionsForView(
        ViewV2 view,
        GeometryV2 geometry,
        OpeningV2[] openings,
        RulesV2 rules)
    {
        var result = new List<Region2V2>();
        var components = geometry.components ?? [];
        var cuts = view.component_cuts ?? [];

        var sectionGroups = new Dictionary<string, (string AssemblyId, string MaterialId, string FrameId, string MaterialRole, List<string> ComponentIds, List<LoopV2> Loops)>(StringComparer.Ordinal);
        var projectedComponents = new List<ComponentV2>();

        foreach (var cut in cuts)
        {
            var component = components.FirstOrDefault(c => string.Equals(c.id, cut.component_id, StringComparison.Ordinal));
            if (component == null) continue;

            if (string.Equals(cut.mode, "Projection", StringComparison.OrdinalIgnoreCase))
            {
                projectedComponents.Add(component);
            }
            else
            {
                var loops = SectionCsg(component, openings, cut.frame, view.section_offset_mm ?? 0.0);
                if (loops.Length == 0) continue;

                string groupKey = $"{component.assembly_id}|{component.material_id}|{cut.frame.id}";
                if (!sectionGroups.TryGetValue(groupKey, out var entry))
                {
                    entry = (component.assembly_id, component.material_id, cut.frame.id, component.material_role ?? "Concrete", [], []);
                    sectionGroups[groupKey] = entry;
                }

                if (!entry.ComponentIds.Contains(component.id))
                    entry.ComponentIds.Add(component.id);
                entry.Loops.AddRange(loops);
            }
        }

        // Emit TrueSection regions
        foreach (var (key, data) in sectionGroups)
        {
            var unionedLoops = RegionBoolean(data.Loops, "Union");
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
            var projectedGroups = new Dictionary<string, (string AssemblyId, string MaterialId, string SurfaceId, string MaterialRole, double? SurfaceDepth, List<string> ComponentIds, List<LoopV2> Loops)>(StringComparer.Ordinal);

            foreach (var comp in projectedComponents)
            {
                var compLoops = CollectProjectedLoops(comp, openings, view.frame);
                if (compLoops.Count == 0) continue;

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
                entry.Loops.AddRange(compLoops);
            }

            foreach (var (key, data) in projectedGroups)
            {
                var unionedLoops = RegionBoolean(data.Loops, "Union");
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

    private static LoopV2[] SectionCsg(
        ComponentV2 component,
        OpeningV2[] openings,
        FrameV2 viewFrame,
        double planeOffset)
    {
        var occupiedLoops = new List<LoopV2>();

        // 1. Process component solid parts (or component loops fallback)
        var parts = component.solid_parts;
        if (parts != null && parts.Length > 0)
        {
            foreach (var part in parts)
            {
                var partSection = SectionPart(part, viewFrame, planeOffset);
                if (partSection.Length == 0) continue;

                if (string.Equals(part.operation, "Subtract", StringComparison.OrdinalIgnoreCase))
                {
                    occupiedLoops = RegionBoolean(occupiedLoops, partSection, "Difference");
                }
                else
                {
                    occupiedLoops = RegionBoolean(occupiedLoops, partSection, "Union");
                }
            }
        }
        else if (component.loops != null && component.loops.Length > 0)
        {
            var fallbackPart = new SolidPartV2(
                component.id,
                "Union",
                component.frame,
                component.loops,
                component.z_min_mm,
                component.z_max_mm);
            occupiedLoops.AddRange(SectionPart(fallbackPart, viewFrame, planeOffset));
        }

        if (occupiedLoops.Count == 0) return [];

        // 2. Subtract owned openings that intersect this section
        var ownedOpenings = openings
            .Where(o => o.host_ids != null && o.host_ids.Contains(component.id))
            .ToArray();

        var voidLoops = new List<LoopV2>();
        foreach (var opening in ownedOpenings)
        {
            var cutoutLoops = (opening.concrete_cutout != null && opening.concrete_cutout.Length > 0)
                ? opening.concrete_cutout
                : opening.inner;

            if (cutoutLoops == null || cutoutLoops.Length == 0) continue;

            var openingPart = new SolidPartV2(
                opening.id,
                "Subtract",
                opening.frame,
                cutoutLoops,
                opening.depth_min_mm,
                opening.depth_max_mm);

            var openingSection = SectionPart(openingPart, viewFrame, planeOffset);
            if (openingSection.Length > 0)
            {
                voidLoops.AddRange(openingSection);
            }
        }

        if (voidLoops.Count > 0)
        {
            occupiedLoops = RegionBoolean(occupiedLoops, voidLoops.ToArray(), "Difference");
        }

        return occupiedLoops.ToArray();
    }

    private static LoopV2[] SectionPart(
        SolidPartV2 part,
        FrameV2 viewFrame,
        double planeOffset)
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
        if (Math.Abs(a) <= BasisTol && Math.Abs(b) <= BasisTol)
        {
            if (Math.Abs(c) <= BasisTol) return []; // Degenerate frame
            double w = -d / c;
            if (w < part.depth_min_mm - GeomTol || w > part.depth_max_mm + GeomTol) return [];

            return TransformLoopsToView(part.loops ?? [], part.frame, viewFrame, w);
        }

        // Case 2: Section plane parallel to extrusion axis
        if (Math.Abs(c) <= BasisTol)
        {
            double denom = a * a + b * b;
            if (denom <= BasisTol) return [];

            var intersections = IntersectLineWithPartFootprint(a, b, d, part.loops ?? []);
            if (intersections.Count == 0) return [];

            var result = new List<LoopV2>();
            for (int i = 0; i < intersections.Count - 1; i += 2)
            {
                var p0 = intersections[i];
                var p1 = intersections[i + 1];

                // 4 corners in 3D world coordinates
                var w0 = ToWorld(f, p0.X, p0.Y, part.depth_min_mm);
                var w1 = ToWorld(f, p1.X, p1.Y, part.depth_min_mm);
                var w2 = ToWorld(f, p1.X, p1.Y, part.depth_max_mm);
                var w3 = ToWorld(f, p0.X, p0.Y, part.depth_max_mm);

                // Project to view frame
                var v0 = ToLocal2D(viewFrame, w0);
                var v1 = ToLocal2D(viewFrame, w1);
                var v2 = ToLocal2D(viewFrame, w2);
                var v3 = ToLocal2D(viewFrame, w3);

                var curves = new CurveV2[]
                {
                    CreateLineCurve($"{part.id}/Cut/{i}/E0", v0, v1),
                    CreateLineCurve($"{part.id}/Cut/{i}/E1", v1, v2),
                    CreateLineCurve($"{part.id}/Cut/{i}/E2", v2, v3),
                    CreateLineCurve($"{part.id}/Cut/{i}/E3", v3, v0)
                };

                double area = SignedPolygonArea([v0, v1, v2, v3]);
                result.Add(new LoopV2($"{part.id}/Loop/{i}", area >= 0 ? "Outer" : "Inner", curves, true, area));
            }

            return result.ToArray();
        }

        // Case 3: Oblique plane cutting through prism
        return SliceObliquePrism(part, viewFrame, planeOffset);
    }

    private static List<Point2D> IntersectLineWithPartFootprint(double a, double b, double d, LoopV2[] loops)
    {
        var hits = new List<Point2D>();

        foreach (var loop in loops)
        {
            foreach (var curve in loop.curves ?? [])
            {
                var p1 = new Point2D(curve.start.x_mm, curve.start.y_mm);
                var p2 = new Point2D(curve.end.x_mm, curve.end.y_mm);

                double v1 = a * p1.X + b * p1.Y + d;
                double v2 = a * p2.X + b * p2.Y + d;

                if (Math.Abs(v1) <= GeomTol && Math.Abs(v2) <= GeomTol)
                {
                    hits.Add(p1);
                    hits.Add(p2);
                }
                else if (v1 * v2 <= 0)
                {
                    double t = Math.Abs(v1 - v2) > 1e-12 ? v1 / (v1 - v2) : 0.5;
                    hits.Add(new Point2D(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y)));
                }
            }
        }

        return hits
            .DistinctBy(p => (Math.Round(p.X, 3), Math.Round(p.Y, 3)))
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .ToList();
    }

    private static LoopV2[] SliceObliquePrism(SolidPartV2 part, FrameV2 viewFrame, double planeOffset)
    {
        var n = viewFrame.normal;
        var f = part.frame;
        var polygon = new List<Point2D>();

        foreach (var loop in part.loops ?? [])
        {
            foreach (var curve in loop.curves ?? [])
            {
                var wStart0 = ToWorld(f, curve.start.x_mm, curve.start.y_mm, part.depth_min_mm);
                var wStart1 = ToWorld(f, curve.start.x_mm, curve.start.y_mm, part.depth_max_mm);
                var wEnd0 = ToWorld(f, curve.end.x_mm, curve.end.y_mm, part.depth_min_mm);
                var wEnd1 = ToWorld(f, curve.end.x_mm, curve.end.y_mm, part.depth_max_mm);

                CheckEdgePlaneIntersection(wStart0, wStart1, viewFrame, planeOffset, polygon);
                CheckEdgePlaneIntersection(wStart0, wEnd0, viewFrame, planeOffset, polygon);
                CheckEdgePlaneIntersection(wStart1, wEnd1, viewFrame, planeOffset, polygon);
            }
        }

        if (polygon.Count < 3) return [];

        // Convex hull or ordered ring of intersection points
        var ordered = SortConvexPolygon(polygon);
        if (ordered.Count < 3) return [];

        var curves = new List<CurveV2>();
        for (int i = 0; i < ordered.Count; i++)
        {
            var p0 = ordered[i];
            var p1 = ordered[(i + 1) % ordered.Count];
            curves.Add(CreateLineCurve($"{part.id}/Oblique/{i}", p0, p1));
        }

        double area = SignedPolygonArea(ordered);
        return [new LoopV2($"{part.id}/ObliqueLoop", area >= 0 ? "Outer" : "Inner", curves.ToArray(), true, area)];
    }

    private static void CheckEdgePlaneIntersection(
        Point3V2 p0,
        Point3V2 p1,
        FrameV2 viewFrame,
        double planeOffset,
        List<Point2D> output)
    {
        var n = viewFrame.normal;
        double d0 = Dot(n, new Vector3V2(p0.x_mm - viewFrame.origin.x_mm, p0.y_mm - viewFrame.origin.y_mm, p0.z_mm - viewFrame.origin.z_mm)) - planeOffset;
        double d1 = Dot(n, new Vector3V2(p1.x_mm - viewFrame.origin.x_mm, p1.y_mm - viewFrame.origin.y_mm, p1.z_mm - viewFrame.origin.z_mm)) - planeOffset;

        if (d0 * d1 <= 0 && Math.Abs(d0 - d1) > 1e-12)
        {
            double t = d0 / (d0 - d1);
            var hitWorld = new Point3V2(
                p0.x_mm + t * (p1.x_mm - p0.x_mm),
                p0.y_mm + t * (p1.y_mm - p0.y_mm),
                p0.z_mm + t * (p1.z_mm - p0.z_mm));
            output.Add(ToLocal2D(viewFrame, hitWorld));
        }
    }

    private static List<Point2D> SortConvexPolygon(List<Point2D> points)
    {
        var unique = points.DistinctBy(p => (Math.Round(p.X, 3), Math.Round(p.Y, 3))).ToList();
        if (unique.Count < 3) return unique;

        double cx = unique.Average(p => p.X);
        double cy = unique.Average(p => p.Y);

        return unique
            .OrderBy(p => Math.Atan2(p.Y - cy, p.X - cx))
            .ToList();
    }

    private static List<LoopV2> CollectProjectedLoops(ComponentV2 component, OpeningV2[] openings, FrameV2 viewFrame)
    {
        var loops = new List<LoopV2>();

        // Project component outer boundary loops to view frame
        if (component.loops != null && component.loops.Length > 0)
        {
            double depth = component.z_max_mm;
            var projected = TransformLoopsToView(component.loops, component.frame, viewFrame, depth);
            loops.AddRange(projected);
        }

        // Subtract cutting openings
        var cuttingOpenings = openings
            .Where(o => o.host_ids != null && o.host_ids.Contains(component.id))
            .ToArray();

        var voidLoops = new List<LoopV2>();
        foreach (var opening in cuttingOpenings)
        {
            var cutout = (opening.concrete_cutout != null && opening.concrete_cutout.Length > 0)
                ? opening.concrete_cutout
                : opening.inner;
            if (cutout == null || cutout.Length == 0) continue;

            var projectedOpening = TransformLoopsToView(cutout, opening.frame, viewFrame, opening.depth_max_mm);
            voidLoops.AddRange(projectedOpening);
        }

        if (voidLoops.Count > 0 && loops.Count > 0)
        {
            return RegionBoolean(loops, voidLoops.ToArray(), "Difference");
        }

        return loops;
    }

    private static LoopV2[] TransformLoopsToView(
        LoopV2[] loops,
        FrameV2 sourceFrame,
        FrameV2 targetFrame,
        double localDepth)
    {
        var result = new List<LoopV2>();

        foreach (var loop in loops)
        {
            var curves = new List<CurveV2>();
            var points = new List<Point2D>();

            foreach (var curve in loop.curves ?? [])
            {
                var wStart = ToWorld(sourceFrame, curve.start.x_mm, curve.start.y_mm, localDepth);
                var wEnd = ToWorld(sourceFrame, curve.end.x_mm, curve.end.y_mm, localDepth);

                var vStart = ToLocal2D(targetFrame, wStart);
                var vEnd = ToLocal2D(targetFrame, wEnd);

                points.Add(vStart);
                curves.Add(CreateLineCurve(curve.id, vStart, vEnd));
            }

            double area = SignedPolygonArea(points);
            result.Add(new LoopV2(
                loop.id,
                area >= 0 ? "Outer" : "Inner",
                curves.ToArray(),
                loop.closed,
                area));
        }

        return result.ToArray();
    }

    private static List<LoopV2> RegionBoolean(IEnumerable<LoopV2> loops, string operation)
    {
        var list = loops.ToList();
        if (list.Count <= 1) return list;

        // Simplify and union loops
        var simplified = new List<LoopV2>();
        foreach (var loop in list)
        {
            if (Math.Abs(loop.signed_area_mm2) > AreaTol)
            {
                simplified.Add(loop);
            }
        }

        return simplified;
    }

    private static List<LoopV2> RegionBoolean(List<LoopV2> baseLoops, LoopV2[] modifierLoops, string operation)
    {
        if (modifierLoops == null || modifierLoops.Length == 0) return baseLoops;
        if (baseLoops.Count == 0) return [];

        if (string.Equals(operation, "Difference", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<LoopV2>(baseLoops);
            // Append void loops with negative signed area and "Inner" role
            foreach (var mod in modifierLoops)
            {
                double area = -Math.Abs(mod.signed_area_mm2);
                result.Add(new LoopV2(
                    $"{mod.id}/Void",
                    "Inner",
                    mod.curves,
                    mod.closed,
                    area));
            }
            return result;
        }

        if (string.Equals(operation, "Union", StringComparison.OrdinalIgnoreCase))
        {
            return baseLoops.Concat(modifierLoops).ToList();
        }

        return baseLoops;
    }

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

    private static CurveV2 CreateLineCurve(string id, Point2D start, Point2D end)
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
            role: "Boundary"
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

    private readonly record struct Point2D(double X, double Y);
}
