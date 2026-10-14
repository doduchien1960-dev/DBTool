#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;

namespace DBTool.Drawing;

/// <summary>
/// S6 Catch basin assembly projector.
/// Generates projection views, geometry curves, opening maps, material targets,
/// rebar curves, and native product references for CatchPlan, Catch11, and Catch22
/// from the shared physical assembly.
/// </summary>
public sealed class CatchAssemblyProjector
{
    private const double DefaultBasisTol = 1e-6;
    private const double DefaultGeomTol = 1e-4;

    /// <summary>
    /// Projects Catch Basin 3D assembly into 2D drawing projection contracts.
    /// </summary>
    public ProjectionV2 Project(
        EffectiveV2 effective,
        GeometryV2 geometry,
        RebarV2 rebar,
        GroupsV2 groups,
        AssetsV2 assets,
        RulesV2 rules)
    {
        ArgumentNullException.ThrowIfNull(effective);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(rebar);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(rules);

        var catchSpec = effective.@catch;
        bool isCatchActive = catchSpec != null && catchSpec.active;

        var views = new List<ViewV2>();
        var curves = new List<ProjectedCurveV2>();
        var targets = new List<Target2V2>();
        var openingMap = new List<ProjectedOpeningV2>();
        var regionsList = new List<Region2V2>();
        var nativeReferences = new List<ProjectedNativeV2>();

        double scale = effective.body_scale > 0 ? effective.body_scale : 30.0;
        string ownerId = Encode(effective.id, "Catch");

        var catchRoles = new[] { "CatchPlan", "Catch11", "Catch22" };

        if (!isCatchActive)
        {
            foreach (var role in catchRoles)
            {
                var dummyFrame = MakeFrame(Encode(effective.id, role, "Projection"), new Point3V2(0, 0, 0), new Vector3V2(1, 0, 0), new Vector3V2(0, 1, 0), new Vector3V2(0, 0, 1));
                views.Add(new ViewV2(
                    id: Encode(effective.id, role),
                    role: role,
                    owner_id: ownerId,
                    frame: dummyFrame,
                    section_offset_mm: 0.0,
                    scale: scale,
                    mirror: false,
                    required: false,
                    na_reason: "CatchBasinNotConfigured",
                    component_cuts: []
                ));
            }

            return new ProjectionV2(
                views: views.ToArray(),
                curves: [],
                targets: [],
                opening_map: [],
                regions: [],
                native_references: []
            );
        }

        // Catch is active: build 3 views
        var catchComponents = (geometry.components ?? [])
            .Where(c => IsCatchComponent(c))
            .ToList();

        // If no components explicitly tagged Catch, include components containing Catch in assembly_id or local_key
        if (catchComponents.Count == 0)
        {
            catchComponents = (geometry.components ?? []).ToList();
        }

        foreach (var role in catchRoles)
        {
            string viewId = Encode(effective.id, role);
            (FrameV2 frame, ViewPartCutV2[] cuts) = MakeCatchViewFrameAndCuts(role, effective, catchComponents, viewId);

            var view = new ViewV2(
                id: viewId,
                role: role,
                owner_id: ownerId,
                frame: frame,
                section_offset_mm: 0.0,
                scale: scale,
                mirror: false,
                required: true,
                na_reason: null,
                component_cuts: cuts
            );
            views.Add(view);
        }

        // Emit regions using AssemblyBoundaryEmitter from W03
        var emitter = new AssemblyBoundaryEmitter();
        var allRegions = emitter.Emit(geometry, null, views.ToArray(), rules);
        regionsList.AddRange(allRegions);

        // Process each view
        foreach (var view in views)
        {
            var viewRegions = regionsList.Where(r => string.Equals(r.view_id, view.id, StringComparison.Ordinal)).ToList();

            // 1. Geometry Curves and Material Targets from Regions
            foreach (var region in viewRegions)
            {
                var curveIds = new List<string>();
                var loopIds = new List<string>();

                foreach (var loop in region.loops ?? [])
                {
                    loopIds.Add(loop.id);
                    foreach (var c in loop.curves ?? [])
                    {
                        string cid = Encode(view.id, c.id);
                        curveIds.Add(cid);
                        curves.Add(new ProjectedCurveV2(
                            id: cid,
                            view_id: view.id,
                            source_id: region.id,
                            group_id: null,
                            kind: c.kind,
                            start: XY(c.start),
                            end: XY(c.end),
                            center: c.center != null ? XY(c.center) : null,
                            radius_mm: c.radius_mm,
                            sweep_rad: c.sweep_rad,
                            visibility: string.Equals(region.representation_kind, "TrueSection", StringComparison.OrdinalIgnoreCase) ? "Sectioned" : "Visible",
                            role: "Geometry",
                            projection_frame_id: region.projection_frame_id
                        ));
                    }
                }

                Point2V2 targetPoint = ComputeRegionTargetPoint(region);
                targets.Add(new Target2V2(
                    id: Encode(view.id, region.id, "Material"),
                    view_id: view.id,
                    source_id: region.id,
                    group_id: null,
                    kind: "Material",
                    point: targetPoint,
                    curve_ids: curveIds.ToArray(),
                    region_loop_ids: loopIds.ToArray(),
                    visible: true
                ));
            }

            // 2. Openings
            var allOpenings = (geometry.openings ?? []).ToList();
            foreach (var opening in allOpenings)
            {
                var cuts = (view.component_cuts ?? [])
                    .Where(cut => opening.host_ids != null && opening.host_ids.Contains(cut.component_id))
                    .ToList();

                foreach (var cut in cuts)
                {
                    string representation = string.Equals(cut.mode, "Projection", StringComparison.OrdinalIgnoreCase)
                        ? "ProjectedOpening"
                        : "SectionContour";

                    var loopCurves = (opening.inner ?? opening.concrete_cutout ?? [])
                        .SelectMany(l => l.curves ?? [])
                        .ToList();

                    var emittedIds = new List<string>();
                    foreach (var oc in loopCurves)
                    {
                        var projCurves = ProjectAnalyticCurve(oc, cut.frame, view);
                        foreach (var pc in projCurves)
                        {
                            var updated = new ProjectedCurveV2(
                                id: pc.id,
                                view_id: pc.view_id,
                                source_id: opening.id,
                                group_id: null,
                                kind: pc.kind,
                                start: pc.start,
                                end: pc.end,
                                center: pc.center,
                                radius_mm: pc.radius_mm,
                                sweep_rad: pc.sweep_rad,
                                visibility: "Visible",
                                role: "Opening",
                                projection_frame_id: cut.frame.id
                            );
                            curves.Add(updated);
                            emittedIds.Add(updated.id);
                        }
                    }

                    openingMap.Add(new ProjectedOpeningV2(
                        opening_id: opening.id,
                        view_id: view.id,
                        host_id: cut.component_id,
                        representation: representation,
                        curve_ids: emittedIds.ToArray(),
                        projection_frame_id: cut.frame.id
                    ));

                    targets.Add(new Target2V2(
                        id: Encode(view.id, opening.id, cut.component_id),
                        view_id: view.id,
                        source_id: opening.id,
                        group_id: null,
                        kind: "Opening",
                        point: null,
                        curve_ids: emittedIds.ToArray(),
                        region_loop_ids: [],
                        visible: true
                    ));
                }
            }

            // 3. Rebar occurrences belonging to Catch
            var catchOccurrences = (rebar.occurrences ?? [])
                .Where(b => IsCatchOccurrence(b, catchComponents))
                .ToList();

            foreach (var b in catchOccurrences)
            {
                var group = (groups.items ?? []).FirstOrDefault(g => g.member_ids != null && g.member_ids.Contains(b.id));
                string? groupId = group?.id;

                var cut = (view.component_cuts ?? []).FirstOrDefault(c => string.Equals(c.component_id, b.host_id, StringComparison.Ordinal))
                    ?? (view.component_cuts ?? []).FirstOrDefault();

                var cutFrame = cut?.frame ?? view.frame;
                var barCurveIds = new List<string>();

                foreach (var c in b.path ?? [])
                {
                    var projCurves = ProjectAnalyticCurve(c, cutFrame, view);
                    foreach (var pc in projCurves)
                    {
                        var rebarCurve = new ProjectedCurveV2(
                            id: pc.id,
                            view_id: view.id,
                            source_id: b.id,
                            group_id: groupId,
                            kind: pc.kind,
                            start: pc.start,
                            end: pc.end,
                            center: pc.center,
                            radius_mm: pc.radius_mm,
                            sweep_rad: pc.sweep_rad,
                            visibility: "Visible",
                            role: string.Equals(b.family, "MainMesh", StringComparison.OrdinalIgnoreCase) ? "SteelMain" : "SteelAccessory",
                            projection_frame_id: cutFrame.id
                        );
                        curves.Add(rebarCurve);
                        barCurveIds.Add(rebarCurve.id);
                    }
                }

                Point2V2? targetPt = null;
                if (b.path != null && b.path.Length > 0)
                {
                    var firstCurve = b.path[0];
                    targetPt = ProjectPointToFrame(firstCurve.start, cutFrame);
                }

                string targetKind = string.Equals(b.family, "Tie", StringComparison.OrdinalIgnoreCase) ? "TieNode" : "Bar";
                targets.Add(new Target2V2(
                    id: Encode(view.id, b.id, targetKind),
                    view_id: view.id,
                    source_id: b.id,
                    group_id: groupId,
                    kind: targetKind,
                    point: targetPt,
                    curve_ids: barCurveIds.ToArray(),
                    region_loop_ids: [],
                    visible: true
                ));
            }

            // 4. Products in Catch (e.g. SCR Cover, NAPCUATHU)
            foreach (var prod in geometry.products ?? [])
            {
                string assetName = view.role switch
                {
                    "CatchPlan" => prod.plan_asset,
                    "Catch11" => prod.section_aa_asset,
                    "Catch22" => prod.section_bb_asset,
                    _ => prod.plan_asset
                };

                if (!string.IsNullOrWhiteSpace(assetName))
                {
                    var pt2 = ProjectPointToFrame(prod.anchor, view.frame);
                    nativeReferences.Add(new ProjectedNativeV2(
                        id: Encode(view.id, prod.id, assetName),
                        view_id: view.id,
                        source_id: prod.id,
                        asset_name: assetName,
                        insertion: pt2,
                        rotation_rad: prod.rotation_rad,
                        scale_x: 1.0,
                        scale_y: 1.0,
                        source_ids: prod.source_ids ?? []
                    ));
                }
            }

            // 5. Register ViewTitle & Elevation targets (H29)
            targets.Add(new Target2V2(
                id: Encode(view.id, "ViewTitle"),
                view_id: view.id,
                source_id: view.id,
                group_id: null,
                kind: "ViewTitle",
                point: new Point2V2(0, 0),
                curve_ids: [],
                region_loop_ids: [],
                visible: true
            ));
        }

        return new ProjectionV2(
            views: views.ToArray(),
            curves: curves.ToArray(),
            targets: targets.ToArray(),
            opening_map: openingMap.ToArray(),
            regions: regionsList.ToArray(),
            native_references: nativeReferences.ToArray()
        );
    }

    private static (FrameV2 Frame, ViewPartCutV2[] Cuts) MakeCatchViewFrameAndCuts(
        string role,
        EffectiveV2 effective,
        List<ComponentV2> components,
        string viewId)
    {
        Point3V2 origin = new(0, 0, 0);
        Vector3V2 u, v, n;

        switch (role)
        {
            case "CatchPlan":
                u = new(1, 0, 0);
                v = new(0, 1, 0);
                n = new(0, 0, 1);
                break;
            case "Catch11":
                u = new(0, 1, 0);
                v = new(0, 0, 1);
                n = new(1, 0, 0);
                break;
            case "Catch22":
                u = new(1, 0, 0);
                v = new(0, 0, 1);
                n = new(0, -1, 0);
                break;
            default:
                u = new(1, 0, 0);
                v = new(0, 1, 0);
                n = new(0, 0, 1);
                break;
        }

        var frame = MakeFrame(Encode(effective.id, role, "Projection"), origin, u, v, n);
        var cuts = new List<ViewPartCutV2>();

        foreach (var c in components)
        {
            string mode = role switch
            {
                "CatchPlan" => "Projection",
                _ => "TrueSection"
            };

            var cutFrame = MakeFrame(Encode(frame.id, c.id), origin, u, v, n);
            cuts.Add(new ViewPartCutV2(c.id, cutFrame, mode));
        }

        return (frame, cuts.ToArray());
    }

    private static List<ProjectedCurveV2> ProjectAnalyticCurve(CurveV2 c, FrameV2 frame, ViewV2 view)
    {
        var result = new List<ProjectedCurveV2>();
        var pStart = ProjectPointToFrame(c.start, frame);
        var pEnd = ProjectPointToFrame(c.end, frame);

        if (string.Equals(c.kind, "Line", StringComparison.OrdinalIgnoreCase))
        {
            double dist = Math.Sqrt(Math.Pow(pEnd.x_mm - pStart.x_mm, 2) + Math.Pow(pEnd.y_mm - pStart.y_mm, 2));
            if (dist > 1e-4)
            {
                result.Add(new ProjectedCurveV2(
                    id: Encode(view.id, c.id),
                    view_id: view.id,
                    source_id: c.id,
                    group_id: null,
                    kind: "Line",
                    start: pStart,
                    end: pEnd,
                    center: null,
                    radius_mm: null,
                    sweep_rad: null,
                    visibility: "Visible",
                    role: "Steel",
                    projection_frame_id: frame.id
                ));
            }
        }
        else if (string.Equals(c.kind, "Arc", StringComparison.OrdinalIgnoreCase))
        {
            Point2V2? pCenter = c.center != null ? ProjectPointToFrame(c.center, frame) : null;
            result.Add(new ProjectedCurveV2(
                id: Encode(view.id, c.id),
                view_id: view.id,
                source_id: c.id,
                group_id: null,
                kind: "Arc",
                start: pStart,
                end: pEnd,
                center: pCenter,
                radius_mm: c.radius_mm,
                sweep_rad: c.sweep_rad,
                visibility: "Visible",
                role: "Steel",
                projection_frame_id: frame.id
            ));
        }

        return result;
    }

    private static Point2V2 ProjectPointToFrame(Point3V2 p, FrameV2 frame)
    {
        double dx = p.x_mm - frame.origin.x_mm;
        double dy = p.y_mm - frame.origin.y_mm;
        double dz = p.z_mm - frame.origin.z_mm;

        double uCoord = dx * frame.axis_u.x + dy * frame.axis_u.y + dz * frame.axis_u.z;
        double vCoord = dx * frame.axis_v.x + dy * frame.axis_v.y + dz * frame.axis_v.z;

        return new Point2V2(uCoord, vCoord);
    }

    private static Point2V2 ComputeRegionTargetPoint(Region2V2 region)
    {
        if (region.loops == null || region.loops.Length == 0) return new Point2V2(0, 0);

        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var loop in region.loops)
        {
            foreach (var c in loop.curves ?? [])
            {
                minX = Math.Min(minX, Math.Min(c.start.x_mm, c.end.x_mm));
                minY = Math.Min(minY, Math.Min(c.start.y_mm, c.end.y_mm));
                maxX = Math.Max(maxX, Math.Max(c.start.x_mm, c.end.x_mm));
                maxY = Math.Max(maxY, Math.Max(c.start.y_mm, c.end.y_mm));
            }
        }

        if (minX == double.MaxValue) return new Point2V2(0, 0);
        return new Point2V2((minX + maxX) / 2.0, (minY + maxY) / 2.0);
    }

    private static bool IsCatchComponent(ComponentV2 c)
    {
        return string.Equals(c.kind, "Catch", StringComparison.OrdinalIgnoreCase) ||
               (c.assembly_id != null && c.assembly_id.Contains("Catch", StringComparison.OrdinalIgnoreCase)) ||
               (c.local_key != null && c.local_key.Contains("Catch", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCatchOccurrence(OccurrenceV2 b, List<ComponentV2> catchComponents)
    {
        if (b.assembly_id != null && b.assembly_id.Contains("Catch", StringComparison.OrdinalIgnoreCase)) return true;
        if (b.region_role != null && b.region_role.Contains("Catch", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(b.family, "Catch", StringComparison.OrdinalIgnoreCase)) return true;
        return catchComponents.Any(c => string.Equals(c.id, b.host_id, StringComparison.Ordinal));
    }

    private static FrameV2 MakeFrame(string id, Point3V2 origin, Vector3V2 u, Vector3V2 v, Vector3V2 n)
    {
        return new FrameV2(id, origin, u, v, n, 1);
    }

    private static Point2V2 XY(Point3V2 p) => new(p.x_mm, p.y_mm);

    private static string Encode(params string?[] parts)
    {
        return string.Join("/", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
