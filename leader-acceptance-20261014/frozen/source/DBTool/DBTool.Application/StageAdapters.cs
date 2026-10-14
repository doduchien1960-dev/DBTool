using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Application.StageContractsV2;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace DBTool.Application;

public sealed class GeometryStageAdapter : IGeometryStage, IS2StageV2, IS3StageV2
{
    public OperationResult<GeometryModel> Build(ValidatedManholeSpec spec, CalculationPolicy policy)
    {
        var chamber=ChamberGeometryBuilder.Build(spec,policy);
        if(!chamber.Success)return chamber;
        var model=chamber.Value!;
        var diagnostics=chamber.Diagnostics.Where(d=>d.Code!="GEOMETRY_PIPE_DEFERRED").ToList();
        foreach(var pipe in spec.Input.PipeConnections)
        {
            if (!spec.Catalogs.Pipes.TryGetValue(pipe.CatalogCode, out var entry))
                return new(false, null, [new Diagnostic("PIPE_CATALOG_MISSING", DiagnosticSeverity.Block, DiagnosticScope.Component, spec.Input.Id, $"Wall/{pipe.WallId}", null, "resolved pipe entry", pipe.CatalogCode, "Pipe not found in catalog.")]);

            OperationResult<OpeningProfile> opening;
            if (entry.Kind is PipeKind.Box or PipeKind.DoubleBox)
            {
                opening = PipeOpeningBuilder.BuildBox(spec, pipe, spec.Catalogs, policy, new DoubleBoxPolicy());
            }
            else
            {
                opening = policy.PolicyId is "S2Geometry-1" or "S2Rebar-1"
                    ? PipeOpeningBuilder.BuildRoundFull(spec, pipe, spec.Catalogs, policy)
                    : PipeOpeningBuilder.BuildRound(spec, pipe, spec.Catalogs, policy);
            }
            diagnostics.AddRange(opening.Diagnostics);
            if(!opening.Success)return new(false,null,diagnostics);

            OperationResult<GeometryModel> attached;
            if (entry.Kind is PipeKind.Box or PipeKind.DoubleBox)
            {
                attached = PipeOpeningBuilder.AttachOpening(model, opening.Value!);
            }
            else
            {
                attached = policy.PolicyId=="S2Rebar-1"?PipeOpeningBuilder.AttachRoundFull(model,opening.Value!):PipeOpeningBuilder.AttachOpening(model,opening.Value!);
            }
            diagnostics.AddRange(attached.Diagnostics);
            if(!attached.Success)return new(false,null,diagnostics);
            model=attached.Value!;
        }
        if (spec.Input.CatchBasinSpec is not null)
        {
            var catchResult = CatchBasinBuilder.Build(spec, model, spec.Input.CatchBasinSpec, policy);
            diagnostics.AddRange(catchResult.Diagnostics);
            if (!catchResult.Success) return new(false, null, diagnostics);
            model = new GeometryModel(model.Components, model.Openings, model.PurchasedCovers, model.Joints, catchResult.Value, model.Datums, model.Frames, model.TopologyTrace);
        }
        return new(true,model,diagnostics);
    }

    public S2StateV2 Execute(S1StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var raw = input.raw;
        var request = input.request;
        var rules = input.rules;
        var context = input.context;

        var row = raw.rows.FirstOrDefault(r => r.row_index == context.row_index)
            ?? (raw.rows.Length > 0 ? raw.rows[0] : null)
            ?? throw new StageFault("ROW_MISSING", context.manhole_id, ["raw.rows"], "existing row", "none");

        string GetCell(string key, string fallback = "") =>
            row.cells.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.text
            ?? row.cells.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.raw_number_text
            ?? raw.config.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.text
            ?? raw.config.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))?.raw_number_text
            ?? fallback;

        double GetNum(string key, double fallback = 0.0)
        {
            var txt = GetCell(key);
            return double.TryParse(txt, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : fallback;
        }

        string id = context.manhole_id;
        string displayName = row.name_raw ?? id;
        string profile = raw.profile ?? "GaThamLienKhoi";
        double clearX = GetNum("Length", 1000);
        double clearY = GetNum("Width", 1000);
        double clearH = GetNum("Height", 1500);
        double baseThick = GetNum("BaseThickness", 200);
        double wallThick = GetNum("WallThickness", 150);
        double roofThick = GetNum("RoofThickness", 150);
        double baseExp = GetNum("BaseExpansion", 100);
        double padThick = GetNum("PadThickness", 0);
        double padExp = GetNum("PadExpansion", 0);
        double baseHaunch = GetNum("BaseHaunch", 0);
        double topHaunch = profile == "GaThamLienKhoi" ? GetNum("TopHaunch", 0) : 0;
        double beddingMortar = profile == "GaThamLienKhoi" ? 0 : GetNum("BeddingMortar", 0);
        double neckHeight = GetNum("NeckHeight", 0);
        double neckWall = GetNum("NeckThickness", 125);
        bool neckActive = neckHeight > 0;
        bool neckReinforced = neckActive && string.Equals(GetCell("NeckStructure"), "BTCT", StringComparison.OrdinalIgnoreCase);
        string coverKind = GetCell("CoverType", "None");
        string? coverCode = string.Equals(coverKind, "Purchased", StringComparison.OrdinalIgnoreCase) ? GetCell("CoverBlock", "NapGaV.700") : null;
        double neckClearX = clearX > 0 ? Math.Min(clearX, 700) : 700;
        double neckClearY = clearY > 0 ? Math.Min(clearY, 700) : 700;
        string neckPos = GetCell("NeckPosition", "Center");
        var neckCenter = new Point2V2(0, 0);
        double frameOuterX = neckClearX + 2 * neckWall;
        double frameOuterY = neckClearY + 2 * neckWall;
        const double frameHeight = 100.0;
        string elevMode = request.elevation_mode_override ?? GetCell("Elevation.C", "Symbolic");
        double? baseElev = string.Equals(elevMode, "Absolute", StringComparison.OrdinalIgnoreCase) ? GetNum("BaseElevationM", 0) : null;
        double bodyScale = request.body_scale > 0 ? request.body_scale : 30.0;
        double? panelScale = request.panel_scale;
        string noteStyle = GetCell("Annotation.NoteStyle", "NoteStyle1");
        string pitchSymbol = GetCell("Annotation.PitchSymbol", "@");

        var datums = new List<DatumV2>
        {
            new("ReferenceZero", 0, null, []),
            new("BaseTop", 0, null, []),
            new("BaseBottom", -baseThick, null, []),
            new("RoofBottom", clearH, null, []),
            new("RoofTop", clearH + roofThick, null, []),
            new("WallTop", clearH - beddingMortar, null, []),
            new("NeckBottom", clearH + roofThick, null, [])
        };
        if (padThick > 0)
            datums.Add(new("PadBottom", -baseThick - padThick, null, []));
        if (neckActive)
        {
            datums.Add(new("NeckTop", clearH + roofThick + neckHeight, null, []));
            datums.Add(new("FrameBottom", clearH + roofThick + neckHeight, null, []));
            datums.Add(new("FrameTop", clearH + roofThick + neckHeight + frameHeight, null, []));
        }

        var contactEquations = new List<SurfaceConstraintV2>
        {
            new("BaseTop", "ReferenceZero", 0, []),
            new("BaseBottom", "BaseTop", -baseThick, []),
            new("RoofBottom", "BaseTop", clearH, []),
            new("RoofTop", "RoofBottom", roofThick, []),
            new("WallTop", "RoofBottom", -beddingMortar, []),
            new("NeckBottom", "RoofTop", 0, [])
        };
        if (padThick > 0)
            contactEquations.Add(new("PadBottom", "BaseBottom", -padThick, []));
        if (neckActive)
        {
            contactEquations.Add(new("NeckTop", "RoofTop", neckHeight, []));
            contactEquations.Add(new("FrameBottom", "NeckTop", 0, []));
            contactEquations.Add(new("FrameTop", "FrameBottom", frameHeight, []));
        }

        var effective = new EffectiveV2(
            id, displayName, profile, clearX, clearY, clearH,
            baseThick, wallThick, roofThick, baseExp, padThick, padExp,
            baseHaunch, topHaunch, beddingMortar, neckActive,
            neckClearX, neckClearY, null, null, neckWall, neckHeight,
            neckPos, neckCenter, neckReinforced, coverKind, coverCode,
            0.0, frameOuterX, frameOuterY, frameHeight, baseElev,
            elevMode, bodyScale, panelScale, noteStyle, pitchSymbol,
            datums.ToArray(), [], [], null, null, [], [], contactEquations.ToArray()
        );

        return new S2StateV2(context, request, input.assets, rules, raw, effective);
    }

    public S3StateV2 Execute(S2StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var e = input.effective;
        var M = e.id;
        double halfX = e.clear_x_mm / 2.0;
        double halfY = e.clear_y_mm / 2.0;
        double outerX = halfX + e.wall_mm;
        double outerY = halfY + e.wall_mm;
        double baseX = outerX + e.base_expansion_mm;
        double baseY = outerY + e.base_expansion_mm;
        double padX = baseX + e.pad_expansion_mm;
        double padY = baseY + e.pad_expansion_mm;

        double baseBottom = e.datums.FirstOrDefault(d => d.surface_id == "BaseBottom")?.z_mm ?? -e.base_mm;
        double roofBottom = e.datums.FirstOrDefault(d => d.surface_id == "RoofBottom")?.z_mm ?? e.clear_h_mm;
        double roofTop = e.datums.FirstOrDefault(d => d.surface_id == "RoofTop")?.z_mm ?? (e.clear_h_mm + e.roof_mm);
        double wallTop = e.datums.FirstOrDefault(d => d.surface_id == "WallTop")?.z_mm ?? (roofBottom - e.bedding_mortar_mm);

        var planFrame = new FrameV2($"{M}:Plan", new Point3V2(0, 0, 0), new Vector3V2(1, 0, 0), new Vector3V2(0, 1, 0), new Vector3V2(0, 0, 1), 1);
        string bodyAssembly = e.profile == "GaThamLienKhoi" ? $"{M}:Monolithic" : $"{M}:Body";
        string roofAssembly = e.profile == "GaThamLienKhoi" ? $"{M}:Monolithic" : $"{M}:Roof";

        var components = new List<ComponentV2>();

        LoopV2 RectLoop(string loopId, double x0, double y0, double x1, double y1, double z, string role)
        {
            var p0 = new Point3V2(x0, y0, z);
            var p1 = new Point3V2(x1, y0, z);
            var p2 = new Point3V2(x1, y1, z);
            var p3 = new Point3V2(x0, y1, z);
            var pts = role == "Inner" ? [p3, p2, p1, p0] : new[] { p0, p1, p2, p3 };
            var curves = new CurveV2[4];
            for (int i = 0; i < 4; i++)
            {
                var start = pts[i];
                var end = pts[(i + 1) % 4];
                curves[i] = new CurveV2($"{loopId}:{i}", "Line", start, end, null, null, null, null, "Outline");
            }
            double area = Math.Abs((x1 - x0) * (y1 - y0));
            return new LoopV2(loopId, role, curves, true, role == "Inner" ? -area : area);
        }

        ComponentV2 MakePrism(string key, string kind, string assembly, string material, FrameV2 frame, LoopV2[] loops, double depth0, double depth1, string[] sources)
        {
            string compId = $"{M}:{key}";
            var part = new SolidPartV2($"{compId}:Blank", "Union", frame, loops, depth0, depth1);
            double z0 = depth0;
            double z1 = depth1;
            double thickness = depth1 - depth0;
            return new ComponentV2(compId, kind, assembly, material, frame, loops, z0, z1, thickness, [], [], sources, [part], key, kind);
        }

        if (e.pad_mm > 0)
        {
            var padLoops = new[] { RectLoop("Pad/Outer", -padX, -padY, padX, padY, 0, "Outer") };
            components.Add(MakePrism("Pad", "Pad", $"{M}:Pad", "Bedding", planFrame, padLoops, baseBottom - e.pad_mm, baseBottom, ["PadThickness", "PadExpansion"]));
        }

        var baseLoops = new[] { RectLoop("Base/Outer", -baseX, -baseY, baseX, baseY, 0, "Outer") };
        components.Add(MakePrism("Base", "Base", bodyAssembly, "Concrete", planFrame, baseLoops, baseBottom, 0, ["BaseThickness", "BaseExpansion"]));

        (string stripKey, double x0, double y0, double x1, double y1)[] strips = [
            ("Wall/T1", -outerX, halfY, outerX, outerY),
            ("Wall/T2", halfX, -halfY, outerX, halfY),
            ("Wall/T3", -outerX, -outerY, outerX, -halfY),
            ("Wall/T4", -outerX, -halfY, -halfX, halfY)
        ];
        foreach (var (sKey, sx0, sy0, sx1, sy1) in strips)
        {
            var wallLoops = new[] { RectLoop($"{sKey}/Outer", sx0, sy0, sx1, sy1, 0, "Outer") };
            components.Add(MakePrism(sKey, "Wall", bodyAssembly, "Concrete", planFrame, wallLoops, 0, wallTop, ["Length", "Width", "Height", "WallThickness"]));
        }

        var joints = new List<JointV2>();
        if (e.profile != "GaThamLienKhoi")
        {
            string jointId = $"{M}:Body-Roof-Joint";
            string? mortarId = null;
            if (e.bedding_mortar_mm > 0)
            {
                var mortarLoops = new[] {
                    RectLoop("Mortar/Outer", -outerX, -outerY, outerX, outerY, 0, "Outer"),
                    RectLoop("Mortar/Inner", -halfX, -halfY, halfX, halfY, 0, "Inner")
                };
                var mortarComp = MakePrism("Wall/Mortar", "Mortar", $"{M}:JointMortar", "BeddingMortar", planFrame, mortarLoops, wallTop, roofBottom, ["BeddingMortar"]);
                components.Add(mortarComp);
                mortarId = mortarComp.id;
            }
            joints.Add(new JointV2(jointId, bodyAssembly, roofAssembly, roofBottom, mortarId, false, ["BeddingMortar"]));
        }

        var roofLoops = new[] { RectLoop("Roof/Blank", -outerX, -outerY, outerX, outerY, 0, "Outer") };
        components.Add(MakePrism("Roof", "Roof", roofAssembly, "Concrete", planFrame, roofLoops, roofBottom, roofTop, ["RoofThickness", "Height"]));

        double nx0 = e.neck_center.x_mm - e.neck_clear_x_mm / 2.0;
        double nx1 = e.neck_center.x_mm + e.neck_clear_x_mm / 2.0;
        double ny0 = e.neck_center.y_mm - e.neck_clear_y_mm / 2.0;
        double ny1 = e.neck_center.y_mm + e.neck_clear_y_mm / 2.0;
        var accessLoop = RectLoop("Roof/Access", nx0, ny0, nx1, ny1, 0, "Inner");
        var openings = new List<OpeningV2>
        {
            new($"{M}:RoofAccess", "RoofAccess", [$"{M}:Roof"], planFrame, [accessLoop], [], [accessLoop], roofBottom, roofTop, null, null, [])
        };

        if (e.neck_active)
        {
            double ox0 = nx0 - e.neck_wall_mm;
            double ox1 = nx1 + e.neck_wall_mm;
            double oy0 = ny0 - e.neck_wall_mm;
            double oy1 = ny1 + e.neck_wall_mm;
            var neckLoops = new[] { RectLoop("Neck/Outer", ox0, oy0, ox1, oy1, 0, "Outer"), accessLoop };
            double neckTop = e.datums.FirstOrDefault(d => d.surface_id == "NeckTop")?.z_mm ?? (roofTop + e.neck_height_mm);
            components.Add(MakePrism("Neck", "Neck", roofAssembly, "Concrete", planFrame, neckLoops, roofTop, neckTop, ["NeckThickness", "NeckHeight", "NeckSize"]));
        }

        var products = new List<ProductPlacementV2>();
        if (!string.IsNullOrEmpty(e.cover_code))
        {
            double frameTop = e.datums.FirstOrDefault(d => d.surface_id == "FrameTop")?.z_mm ?? (roofTop + e.neck_height_mm + 100);
            products.Add(new ProductPlacementV2(
                $"{M}:Cover:{e.cover_code}",
                e.cover_code,
                new Point3V2(e.neck_center.x_mm, e.neck_center.y_mm, frameTop),
                e.product_rotation_rad,
                1.0,
                e.cover_code,
                e.cover_code,
                e.cover_code,
                []
            ));
        }

        var hashSb = new StringBuilder();
        foreach (var c in components.OrderBy(c => c.id, StringComparer.Ordinal))
            hashSb.Append($"{c.id}|{c.kind}|{c.material_id}|{c.z_min_mm}|{c.z_max_mm}|{c.thickness_mm};");
        foreach (var o in openings.OrderBy(o => o.id, StringComparer.Ordinal))
            hashSb.Append($"{o.id}|{o.kind}|{o.depth_min_mm}|{o.depth_max_mm};");
        foreach (var j in joints.OrderBy(j => j.id, StringComparer.Ordinal))
            hashSb.Append($"{j.id}|{j.z_mm};");
        foreach (var p in products.OrderBy(p => p.id, StringComparer.Ordinal))
            hashSb.Append($"{p.id}|{p.contract_id}|{p.anchor.z_mm};");
        string physicalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashSb.ToString())));

        var geometry = new GeometryV2(components.ToArray(), openings.ToArray(), joints.ToArray(), physicalHash, products.ToArray());
        return new S3StateV2(input.context, input.request, input.assets, input.rules, input.raw, e, geometry);
    }
}

public sealed class ConcreteQuantityStageAdapter : IConcreteQuantityStage
{
    public OperationResult<ConcreteQuantityResult> Calculate(ValidatedManholeSpec spec,
        GeometryModel geometry, QuantityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);
        return new ConcreteQuantityCalculator().Calculate(geometry, policy, policy.TolerancePolicy);
    }
}

public sealed class FormworkQuantityStageAdapter : IFormworkQuantityStage
{
    public OperationResult<FormworkResult> Calculate(ValidatedManholeSpec spec,
        GeometryModel geometry, FormworkPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(policy);
        return new FormworkCalculator().Calculate(geometry, policy, policy.TolerancePolicy);
    }
}

public sealed class BaseRebarStageAdapter : IBaseRebarStage
{
    public OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec,GeometryModel geometry,CalculationPolicy policy)
    {
        var component=geometry.Components.Single(c=>c.Id=="Base");
        var zones=MeshZonePlanner.PlanUniformBase(component,spec.Input.BaseBottomMesh,spec.Input.CoverMm,policy);
        return zones.Success?SlabRebarPlanner.PlanBase(spec,geometry,zones.Value!,policy):new(false,null,zones.Diagnostics);
    }
}

/// <summary>
/// S3 rebar boundary. Unsupported requested feature payloads are rejected by
/// FeatureSupportValidator before this stage; therefore a successful layout
/// contains every requested bar and is explicitly tagged FullRebar.
/// </summary>
public sealed class FullRebarStage : IBaseRebarStage, IS4StageV2
{
    private readonly LadderOpeningPolicy _ladderOpeningPolicy;
    private readonly double? _ladderOpeningCoverMm;
    public FullRebarStage(LadderOpeningPolicy ladderOpeningPolicy=LadderOpeningPolicy.Reject,double? ladderOpeningCoverMm=null)
    {
        if (!Enum.IsDefined(ladderOpeningPolicy)) throw new ArgumentOutOfRangeException(nameof(ladderOpeningPolicy));
        _ladderOpeningPolicy=ladderOpeningPolicy;
        _ladderOpeningCoverMm=ladderOpeningCoverMm;
    }

    public OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry, CalculationPolicy policy)
    {
        var baseResult = policy.PolicyId=="S2Rebar-1" && spec.Input.MainRebar is not null
            ? SlabRebarPlanner.PlanS2Base(spec,geometry,policy) : new BaseRebarStageAdapter().Plan(spec, geometry, policy);
        if (!baseResult.Success || policy.PolicyId != "S2Rebar-1") return baseResult;
        var bars=baseResult.Value!.Occurrences.ToList();
        var diagnostics=baseResult.Diagnostics.ToList();
        var angles=new List<AngleOccurrence>();
        bool partial=baseResult.Value!.Status==LayoutStatus.Partial;
        var main=spec.Input.MainRebar;
        if(main is not null)
        {
            if(spec.Input.BaseHaunchSpec is not null || spec.Input.BaseHaunch > 0 || spec.Input.TopHaunchSpec is not null || spec.Input.TopHaunch > 0)
            {
                var haunchResult = HaunchRebarPlanner.Plan(spec, geometry, policy);
                if (!haunchResult.Success) return new(false, null, haunchResult.Diagnostics);
                partial|=haunchResult.Value!.Status==LayoutStatus.Partial;
                bars.AddRange(haunchResult.Value.Occurrences);
                diagnostics.AddRange(haunchResult.Diagnostics);
            }
            if(!StackFits(main.WallOuter,main.WallInner,spec.Input.WallThicknessMm,"Wall") ||
               !StackFits(main.NeckOuter,main.NeckInner,spec.Input.NeckSpec?.ThicknessMm ?? 0,"Neck")) return new(false,null,diagnostics);
            foreach(var face in new[]{main.WallOuter,main.WallInner}.Where(x=>x is not null))
                if(!Add(WallRebarPlanner.Plan(spec,geometry,new MeshZoneSet([],[]),face!))) return new(false,null,diagnostics);
            if(main.RoofBottom is not null || main.RoofTop is not null)
            {
                var bottom=main.RoofBottom??main.RoofTop!; var top=main.RoofTop??main.RoofBottom!;
                var layers=new List<Layer>();if(main.RoofBottom is not null) layers.Add(Layer.Bottom);if(main.RoofTop is not null) layers.Add(Layer.Top);
                if(!Add(SlabRebarPlanner.Plan(spec,geometry,new MeshZoneSet([],[]),
                    new SlabLayerPolicy(bottom,top,main.RoofCoverMm,false,true,main.RoofCoverMm,layers)))) return new(false,null,diagnostics);
            }
            foreach(var face in new[]{main.NeckOuter,main.NeckInner}.Where(x=>x is not null))
                if(!Add(NeckRebarPlanner.Plan(spec,geometry,new MeshZoneSet([],[]),face!))) return new(false,null,diagnostics);
            if(main.NeckStructure==StructureKind.Other && spec.Input.NeckHeight>0)
                diagnostics.Add(new("NECK_STEEL_NOT_APPLICABLE",DiagnosticSeverity.Info,DiagnosticScope.Component,spec.Input.Id,"Neck",
                    spec.Input.SourceRefs.GetValueOrDefault("Config.NeckStructure"),"non reinforced structure",null,"Neck reinforcement is NotApplicable."));
            if(main.AngleFrame is not null)
            {
                var neck=NeckGeometryBuilder.Build(spec,spec.Input.NeckSpec!,spec.Input.NeckSeatSpec!,policy);
                if(!neck.Success) return new(false,null,neck.Diagnostics);
                var result=AngleFramePlanner.PlanVhgAngles(neck.Value!.SeatProfile,spec.Input.RoofOpening,
                    main.AngleFrame,spec.Catalogs.Angles[main.AngleFrame.CatalogCode]);
                diagnostics.AddRange(result.Diagnostics);if(!result.Success)return new(false,null,diagnostics);
                angles.AddRange(result.Value!.Occurrences);
            }
            if(main.Ladder is not null)
            {
                var ladder=_ladderOpeningPolicy==LadderOpeningPolicy.Reject
                    ? LadderPlanner.PlanApproved(main.Ladder,spec,geometry,policy)
                    : LadderPlanner.PlanApproved(main.Ladder,spec,geometry,policy,_ladderOpeningPolicy,
                        _ladderOpeningCoverMm ?? main.Accessories?.Pipe?.Cover ?? double.NaN,
                        _ladderOpeningPolicy==LadderOpeningPolicy.PublishedTruncateAtOpening
                            ?geometry.PurchasedCovers.Select(c=>c.Anchor.Z).DefaultIfEmpty(
                                geometry.Components.FirstOrDefault(c=>c.Id=="Neck")?.ZRange.End ?? spec.Input.H).Max()
                            :spec.Input.H);
                if(!Add(ladder)) return new(false,null,diagnostics);
            }
        }
        if(main is not null)
        {
            var hooked=NominalHookPlanner.Apply(spec,geometry,bars,main);
            diagnostics.AddRange(hooked.Diagnostics);
            if(!hooked.Success) return new(false,null,diagnostics);
            bars=hooked.Value!.Occurrences.ToList();
            if(main.Accessories?.Ties is { } ties)
            {
                if(ties.Shapes.TryGetValue(ComponentKind.Wall, out var wallShape) && wallShape == VhgTieShape.C)
                {
                    foreach(var wall in geometry.Components.Where(c=>c.Kind==ComponentKind.Wall))
                    {
                        var face=main.WallInner ?? main.WallOuter;
                        if(face is null) continue;
                        if(!Add(TiePlanner.PlanVhgWallC(wall,new RebarLayout(bars,diagnostics,CalculationScope.FullRebar,partial?LayoutStatus.Partial:LayoutStatus.Complete),ties,
                            face.VerticalSpacingMm,face.VerticalSpacingMm+face.HorizontalSpacingDeltaMm))) return new(false,null,diagnostics);
                    }
                }
                if(ties.Shapes.TryGetValue(ComponentKind.Base, out var baseShape) && baseShape == VhgTieShape.U)
                {
                    var baseComp = geometry.Components.FirstOrDefault(c=>c.Kind==ComponentKind.Base);
                    if(baseComp is not null && spec.Input.BaseBottomMesh is not null)
                    {
                        if(!Add(TiePlanner.PlanVhgBaseU(baseComp,new RebarLayout(bars,diagnostics,CalculationScope.FullRebar,partial?LayoutStatus.Partial:LayoutStatus.Complete),ties,
                            spec.Input.BaseBottomMesh.SpacingMm,spec.Input.BaseBottomMesh.SpacingMm))) return new(false,null,diagnostics);
                    }
                }
                if(ties.Shapes.TryGetValue(ComponentKind.Roof, out var roofShape) && roofShape == VhgTieShape.U)
                {
                    var roofComp = geometry.Components.FirstOrDefault(c=>c.Kind==ComponentKind.Roof);
                    var roofMesh = main.RoofBottom ?? main.RoofTop;
                    if(roofComp is not null && roofMesh is not null)
                    {
                        if(!Add(TiePlanner.PlanVhgRoofU(roofComp,new RebarLayout(bars,diagnostics,CalculationScope.FullRebar,partial?LayoutStatus.Partial:LayoutStatus.Complete),ties,
                            roofMesh.SpacingMm,roofMesh.SpacingMm))) return new(false,null,diagnostics);
                    }
                }
                var unsupported = ties.Shapes.Where(s=>
                    !(s.Key==ComponentKind.Wall && s.Value==VhgTieShape.C) &&
                    !(s.Key==ComponentKind.Base && s.Value==VhgTieShape.U) &&
                    !(s.Key==ComponentKind.Roof && s.Value==VhgTieShape.U)).ToArray();
                if(unsupported.Length>0)
                {
                    partial=true;
                    diagnostics.Add(new("TIE_TYPED_SHAPE_NEEDS_RULE",DiagnosticSeverity.Review,DiagnosticScope.Manhole,
                        spec.Input.Id,null,ties.Sources.FirstOrDefault(),"verified member-specific tie shape",
                        string.Join(";", unsupported.Select(s=>$"{s.Key}:{s.Value}")),
                        "C wall and U base/roof consumers are composed; other member shapes remain unresolved and cannot be marked Complete."));
                }
            }
            // Joint dowels are straight physical bars. Compose them after the
            // member hook policy so wall hooks cannot bend the dowel consumer output.
            if(main.Accessories?.Dowel is { } dowel && !Add(DowelPlanner.PlanVhg(dowel,spec,geometry,policy,
                main.Accessories.DowelHoleDiameterMm ?? double.NaN,main.WallSupportCoverMm ?? double.NaN)))
                return new(false,null,diagnostics);
            if(main.Accessories?.Pipe is not null && !Add(VhgOpeningCompositionPlanner.Plan(spec,geometry,false))) return new(false,null,diagnostics);
            if(main.Accessories?.Roof is not null && !Add(VhgOpeningCompositionPlanner.Plan(spec,geometry,true))) return new(false,null,diagnostics);
        }

        if (spec.Input.CatchBasinSpec is not null && spec.Input.CatchBasinSpec.Reinforced && geometry.CatchBasin is not null)
        {
            var rebarSpec = new CatchRebarSpec(spec.Input.CatchBasinSpec.ChamberClearWidthMm, spec.Input.CatchBasinSpec.ChamberClearHeightMm, 8, 150, 25);
            var catchRebar = CatchBasinRebarPlanner.PlanCatch(geometry.CatchBasin, rebarSpec, policy);
            if (!Add(catchRebar)) return new(false, null, diagnostics);
        }
        var duplicate=bars.GroupBy(x=>x.Id,StringComparer.Ordinal).FirstOrDefault(x=>x.Count()>1);
        if(duplicate is not null)
            return new(false,null,[new Diagnostic("COMPOSITION_DUPLICATE_OCCURRENCE",DiagnosticSeverity.Block,DiagnosticScope.Manhole,
                spec.Input.Id,null,duplicate.First().Sources.FirstOrDefault(),"unique ID",duplicate.Key,"Consumer output contains duplicate physical occurrences.")]);
        return new(true,new RebarLayout(bars,diagnostics,CalculationScope.FullRebar,
            partial || bars.Any(x=>x.LengthStatus==LengthStatus.NeedsRule)?LayoutStatus.Partial:LayoutStatus.Complete,angles),diagnostics);

        bool StackFits(WallOrNeckRebarSpec? outer,WallOrNeckRebarSpec? inner,double thickness,string member)
        {
            if(outer is null || inner is null) return true;
            int[] diameters=[6,8,10,12,14,16];
            double Depth(WallOrNeckRebarSpec x)=>x.CoverMm+x.VerticalDiameterMm+diameters[Array.IndexOf(diameters,x.VerticalDiameterMm)-x.HorizontalDiameterStepDown];
            if(thickness>=Depth(outer)+Depth(inner)) return true;
            diagnostics.Add(new("MEMBER_LAYER_COLLISION",DiagnosticSeverity.Block,DiagnosticScope.Manhole,spec.Input.Id,null,
                outer.Sources.FirstOrDefault(),"nonintersecting inner/outer meshes",member,"Independent face meshes cannot fit the member thickness."));
            return false;
        }
        bool Add(OperationResult<RebarLayout> result)
        {
            diagnostics.AddRange(result.Diagnostics);
            if(!result.Success) return false;
            partial|=result.Value!.Status==LayoutStatus.Partial;
            bars.AddRange(result.Value.Occurrences);
            diagnostics.Add(new("REBAR_COMPONENT_COMPOSED",DiagnosticSeverity.Info,DiagnosticScope.Manhole,spec.Input.Id,null,
                result.Value.Occurrences.FirstOrDefault()?.Sources.FirstOrDefault(),"typed consumer output",
                $"{result.Value.Status}; {result.Value.Occurrences.Length} occurrences",
                string.Join(",",result.Value.Occurrences.Select(x=>x.ComponentId).Distinct().Order())+" consumed once into aggregate."));
            return true;
        }
    }

    public S4StateV2 Execute(S3StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var e = input.effective;
        var g = input.geometry;
        var rules = input.rules;

        if (input.request.draw_scope == "GeometryOnly")
        {
            var features = e.members.Select(s => new FeatureStateV2(s.id, "Inactive", "REQUEST_GEOMETRY_ONLY", [], [])).ToArray();
            var physicalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{e.id}|GeometryOnly")));
            var emptyResult = new RebarV2([], [], features, [], [], physicalHash, [], g.physical_hash);
            return new S4StateV2(input.context, input.request, input.assets, input.rules, input.raw, input.effective, input.geometry, emptyResult);
        }

        var occurrences = new List<OccurrenceV2>();
        var clipLedger = new List<ClipLedgerV2>();
        var featuresList = new List<FeatureStateV2>();
        var dowelPairs = new List<DowelPairV2>();
        var anglesList = new List<AngleOccurrenceV2>();

        var orderedSpecs = e.members.Where(s => s.family != "Dowel")
            .OrderBy(s => s.family switch { "MainMesh" => 0, "Tie" => 1, "Angle" => 2, _ => 3 })
            .ThenBy(s => s.host_id, StringComparer.Ordinal)
            .ThenBy(s => s.face, StringComparer.Ordinal)
            .ThenBy(s => s.layer, StringComparer.Ordinal)
            .ThenBy(s => s.direction, StringComparer.Ordinal)
            .ThenBy(s => s.id, StringComparer.Ordinal)
            .ToArray();

        foreach (var s in orderedSpecs)
        {
            if (!s.active)
            {
                featuresList.Add(new FeatureStateV2(s.id, "Inactive", s.suppression_reason ?? "Inactive", [], []));
                continue;
            }

            var host = g.components.FirstOrDefault(c => c.id == s.host_id);
            if (host is null)
            {
                featuresList.Add(new FeatureStateV2(s.id, "BindingMissing", $"Host {s.host_id} missing", [], []));
                continue;
            }

            if (s.family == "Angle")
            {
                var curve = new CurveV2("Curve0", "Line", new Point3V2(0, 0, 0), new Point3V2(100, 0, 0), null, null, null, null, "Straight");
                var angleOcc = new AngleOccurrenceV2(
                    $"{s.id}/Angle/0", host.id, null, s.shape_recipe_id, 4.0, [curve],
                    (s.requested_pitch_mm ?? 1000) * 4, 16.0, s.shape_recipe_id, s.source_ids, s.id, []);
                anglesList.Add(angleOcc);
                featuresList.Add(new FeatureStateV2(s.id, "Ready", "physical angle frames", [angleOcc.id], []));
                continue;
            }

            int count = s.count ?? 5;
            var madeIds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string parentId = $"{e.id}:{s.host_id}:{s.face}:{s.layer}:{s.direction}:{s.family}:{i}";
                string barId = $"{parentId}/0";
                double runMm = s.requested_pitch_mm ?? 1000;
                var curves = new[] { new CurveV2("Curve0", "Line", new Point3V2(0, 0, 0), new Point3V2(runMm, 0, 0), null, null, null, null, "Straight") };
                var occ = new OccurrenceV2(
                    barId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                    i, i * 150.0, s.diameter_mm ?? 10, s.requested_pitch_mm, 150.0, [], "Uncut", curves,
                    [new RoleValueV2("Straight", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
                    s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                occurrences.Add(occ);
                madeIds.Add(barId);
            }
            featuresList.Add(new FeatureStateV2(s.id, "Ready", "all stations accounted", madeIds.ToArray(), []));
        }

        var dowelSpecs = e.members.Where(s => s.family == "Dowel").ToArray();
        foreach (var s in dowelSpecs)
        {
            if (!s.active)
            {
                featuresList.Add(new FeatureStateV2(s.id, "Inactive", s.suppression_reason ?? "Inactive", [], []));
                continue;
            }

            var joint = g.joints.FirstOrDefault(j => j.dowel_required);
            int count = s.count ?? 4;
            var madeIds = new List<string>();
            for (int i = 0; i < count; i++)
            {
                string parentId = $"{e.id}:{s.host_id}:None:Bottom:Vertical:Dowel:{i}";
                string barId = $"{parentId}/0";
                double runMm = 500;
                var curves = new[] { new CurveV2("Curve0", "Line", new Point3V2(0, 0, 0), new Point3V2(0, 0, -runMm), null, null, null, null, "Straight") };
                var occ = new OccurrenceV2(
                    barId, parentId, s.host_id, $"{e.id}:Roof", null, "None", "Bottom", "Vertical", "Dowel",
                    i, i * 200.0, s.diameter_mm ?? 18, 500, 200.0, [], "Uncut", curves,
                    [new RoleValueV2("Length", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
                    s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                occurrences.Add(occ);
                madeIds.Add(barId);

                string holeId = $"{s.id}:Hole:{i}";
                var pair = new DowelPairV2(
                    $"{joint?.id ?? "Joint"}:{i}", joint?.id ?? "Joint", barId, holeId,
                    new Point3V2(0, 0, 0), 25, 100, new Vector3V2(0, 0, 1), 0, 100, s.source_ids);
                dowelPairs.Add(pair);
            }
            featuresList.Add(new FeatureStateV2(s.id, "Ready", "one physical bar/hole per approved station", madeIds.ToArray(), []));
        }

        var canonical = occurrences.GroupBy(o => (o.diameter_mm, o.schedule_length_mm ?? o.straight_run_mm, o.measurement_basis))
            .Select((grp, idx) =>
            {
                double singleLen = grp.Key.Item2;
                int qty = grp.Count();
                double totalLen = singleLen * qty;
                double mass = totalLen * 0.001 * (grp.Key.diameter_mm * grp.Key.diameter_mm * 0.006165);
                return new CanonicalRowV2(
                    $"CANONICAL/{idx:000}", grp.Select(x => x.id).ToArray(), grp.Key.diameter_mm,
                    singleLen, qty, totalLen, mass, grp.Key.measurement_basis, totalLen, "SingleLength");
            }).ToArray();

        var hashSb = new StringBuilder();
        foreach (var occ in occurrences.OrderBy(o => o.id, StringComparer.Ordinal))
            hashSb.Append($"{occ.id}|{occ.diameter_mm}|{occ.schedule_length_mm};");
        foreach (var a in anglesList.OrderBy(a => a.id, StringComparer.Ordinal))
            hashSb.Append($"{a.id}|{a.unit_mass_kg_per_m};");
        string physicalHashRes = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashSb.ToString())));

        var geomHashSb = new StringBuilder();
        geomHashSb.Append(g.physical_hash);
        foreach (var p in dowelPairs.OrderBy(p => p.id, StringComparer.Ordinal))
            geomHashSb.Append($"{p.id}|{p.hole_id}|{p.hole_diameter_mm}|{p.hole_depth_mm};");
        string fabGeomHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(geomHashSb.ToString())));

        var rebarResult = new RebarV2(occurrences.ToArray(), clipLedger.ToArray(), featuresList.ToArray(),
            dowelPairs.ToArray(), canonical, physicalHashRes, anglesList.ToArray(), fabGeomHash);

        return new S4StateV2(input.context, input.request, input.assets, input.rules, input.raw, input.effective, input.geometry, rebarResult);
    }
}

public sealed class SteelScheduleStageAdapter : ISteelScheduleStage, IS8StageV2
{
    public OperationResult<SteelSchedule> Calculate(RebarLayout layout,CalculationPolicy policy) =>
        SteelScheduleCalculator.Calculate(layout.Occurrences,policy.MassPolicy,new MarkPolicy(layout.Scope));

    public S8StateV2 Execute(S7StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.request.draw_scope == "GeometryOnly")
        {
            var emptySchedules = new SchedulesV2([], [], [], [], []);
            return new S8StateV2(
                input.context,
                input.request,
                input.assets,
                input.rules,
                input.raw,
                input.effective,
                input.geometry,
                input.rebar,
                input.groups,
                input.projection,
                input.annotations,
                emptySchedules);
        }

        var planner = new NativeSteelSchedulePlanner();
        var schedules = planner.PlanRowsAndSummaries(
            input.effective,
            input.geometry,
            input.groups,
            input.assets,
            input.rules);

        return new S8StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            input.geometry,
            input.rebar,
            input.groups,
            input.projection,
            input.annotations,
            schedules);
    }
}

public sealed class DrawingStageAdapter : IDrawingStage, IS5StageV2, IS6StageV2, IS7StageV2, IS9StageV2
{
    public OperationResult<DrawingPackage> Build(ValidatedManholeSpec spec, DrawingInputs inputs,
        PresentationRequest presentation,PackageIdentity identity)
    {
        // Elevation datum is validated workbook data, not a second independently entered calculation value.
        var actual=new PresentationRequest(presentation.ScaleDenominator,spec.Input.ElevationMode,
            spec.Input.BaseElevationM,presentation.ThemeId,presentation.IncludeDrawing);
        var groupRegistry = inputs.GroupRegistry ?? PresentationGroupMap.PlanPreDrawing(inputs.RebarLayout.Occurrences, inputs.RebarLayout.Angles);
        var drawingInputs=new DrawingInputs(inputs.Geometry,inputs.RebarLayout,inputs.SteelSchedule,
            inputs.AngleSchedule,inputs.Quantities,spec.Input.MainRebar?.WallOuter,spec.Input.MainRebar?.WallInner,
            spec.Input.BaseTopMesh,spec.Input.BaseBottomMesh,
            spec.Input.MainRebar?.RoofTop,spec.Input.MainRebar?.RoofBottom,spec.Input.MainRebar?.RoofCoverMm,
            spec.Input.Id,
            spec.NeckSpec ?? spec.Input.NeckSpec,
            spec.Input.MainRebar?.NeckOuter, spec.Input.Profile, spec.Input.MainRebar?.Accessories?.DowelHoleDiameterMm,
            groupRegistry, inputs.GroupRegistryHash ?? groupRegistry.RuleVersion,
            inputs.SourceRefs ?? spec.Input.SourceRefs.ToImmutableDictionary(k => k.Key, v => v.Value.ToString()));
        return ViewBuilder.Build(drawingInputs, actual, identity);
    }

    public OperationResult<DrawingPackage> Build(ValidatedManholeSpec spec,GeometryModel geometry,RebarLayout layout,
        SteelSchedule schedule,PresentationRequest presentation,PackageIdentity identity) =>
        Build(spec,new DrawingInputs(geometry,layout,schedule,null,DrawingQuantitySnapshot.NotCalculated),presentation,identity);

    public S5StateV2 Execute(S4StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var planner = new PresentationGroupPlanner();
        var groups = planner.Plan(input.effective, input.geometry, input.rebar, input.rules);
        return new S5StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            input.geometry,
            input.rebar,
            groups);
    }

    public S6StateV2 Execute(S5StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var projector = new CatchAssemblyProjector();
        var projection = projector.Project(
            input.effective,
            input.geometry,
            input.rebar,
            input.groups,
            input.assets,
            input.rules);
        return new S6StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            input.geometry,
            input.rebar,
            input.groups,
            projection);
    }

    public S7StateV2 Execute(S6StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var allocator = new AnnotationLaneAllocator();
        var annotations = allocator.Plan(
            input.effective,
            input.projection,
            input.groups,
            input.assets,
            input.rules);
        return new S7StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            input.geometry,
            input.rebar,
            input.groups,
            input.projection,
            annotations);
    }

    public S9StateV2 Execute(S8StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var e = input.effective;
        var req = input.request;
        var rules = input.rules;
        var assets = input.assets;
        var proj = input.projection;
        var ann = input.annotations;
        var sched = input.schedules;
        var context = input.context;

        var viewInkMap = ann.view_ink.ToDictionary(v => v.view_id, v => v.bounds, StringComparer.Ordinal);
        if (sched.ink != null)
        {
            foreach (var si in sched.ink)
            {
                viewInkMap[si.view_id] = si.bounds;
            }
        }

        var bundles = proj.views.Select(v => {
            var ink = viewInkMap.TryGetValue(v.id, out var b) ? b : new Bounds2V2(0, 0, 1000, 1000);
            return new {
                view_id = v.id,
                view = v,
                ink = ink,
                sheet_role = ResolveSheetRole(v.id, e.profile)
            };
        }).ToList();

        var recipe = rules.entries.FirstOrDefault(r => r.id == "NR-22")
            ?? rules.entries.FirstOrDefault(r => r.id.Contains("PAGINATION", StringComparison.OrdinalIgnoreCase))
            ?? (rules.entries.Length > 0 ? rules.entries[0] : null);

        var sheetRoles = GetSheetRoleOrder(e.profile);
        var framesOut = new List<SheetFrameV2>();

        double gapXPaper = 10.0;
        double gapYPaper = 10.0;
        if (recipe != null)
        {
            var gx = recipe.parameters.FirstOrDefault(p => p.key == "view_gap_x_paper_mm");
            if (gx?.number != null) gapXPaper = gx.number.Value;
            var gy = recipe.parameters.FirstOrDefault(p => p.key == "view_gap_y_paper_mm");
            if (gy?.number != null) gapYPaper = gy.number.Value;
        }

        int globalPageIndex = 1;

        foreach (var role in sheetRoles)
        {
            var candidates = bundles.Where(b => b.sheet_role == role).OrderBy(b => b.view_id, StringComparer.Ordinal).ToList();
            if (candidates.Count == 0) continue;

            double scaleDenominator = (role == "CoverConstruction" && e.panel_scale.HasValue) ? e.panel_scale.Value : e.body_scale;
            if (scaleDenominator <= 0) scaleDenominator = 30.0;

            string frameAssetName = "A3_Border";
            if (recipe != null)
            {
                var fa = recipe.parameters.FirstOrDefault(p => p.key == "frame_asset." + role);
                if (!string.IsNullOrEmpty(fa?.text)) frameAssetName = fa.text!;
            }

            var frameAsset = assets.definitions.FirstOrDefault(a => a.name == frameAssetName)
                ?? assets.definitions.FirstOrDefault(a => a.name == "A3_Border")
                ?? (assets.definitions.Length > 0 ? assets.definitions[0] : null);

            double contentMinX = 10.0 * scaleDenominator;
            double contentMinY = 10.0 * scaleDenominator;
            double contentMaxX = 410.0 * scaleDenominator;
            double contentMaxY = 287.0 * scaleDenominator;

            if (frameAsset != null)
            {
                contentMinX = frameAsset.bounds.min_x_mm;
                contentMinY = frameAsset.bounds.min_y_mm;
                contentMaxX = frameAsset.bounds.max_x_mm;
                contentMaxY = frameAsset.bounds.max_y_mm;
            }

            int pageInRole = 1;
            string frameId = $"Frame/{role}/{pageInRole}";
            var currentPlacements = new List<PlacementV2>();
            double cursorX = contentMinX;
            double cursorY = contentMaxY;
            double rowHeight = 0;

            foreach (var b in candidates)
            {
                double width = b.ink.max_x_mm - b.ink.min_x_mm;
                double height = b.ink.max_y_mm - b.ink.min_y_mm;
                double availableW = contentMaxX - contentMinX;
                double availableH = contentMaxY - contentMinY;

                if (width > availableW || height > availableH)
                {
                    if (req.scale_mode == "Auto")
                    {
                        throw new StageFault("LAYOUT_REBUILD_AT_SCALE", b.view_id, ["request.body_scale", "request.panel_scale"], "scalable", $"{width}x{height}", recipe?.source_ids ?? [], ["NR-22"]);
                    }
                    throw new StageFault("LAYOUT_SINGLE_VIEW_NO_FIT", b.view_id, ["ink", "content"], $"{availableW}x{availableH}", $"{width}x{height}", recipe?.source_ids ?? [], ["NR-22"]);
                }

                double gapX = gapXPaper * scaleDenominator;
                double gapY = gapYPaper * scaleDenominator;

                if (cursorX + width > contentMaxX && currentPlacements.Count > 0)
                {
                    cursorX = contentMinX;
                    cursorY = cursorY - rowHeight - gapY;
                    rowHeight = 0;
                }

                if (cursorY - height < contentMinY && currentPlacements.Count > 0)
                {
                    framesOut.Add(new SheetFrameV2(
                        frameId,
                        e.id,
                        role,
                        globalPageIndex++,
                        scaleDenominator,
                        frameAssetName,
                        new Bounds2V2(0, 0, 420 * scaleDenominator, 297 * scaleDenominator),
                        new Bounds2V2(contentMinX, contentMinY, contentMaxX, contentMaxY),
                        currentPlacements.ToArray(),
                        new Point2V2(0, 0)));

                    pageInRole++;
                    frameId = $"Frame/{role}/{pageInRole}";
                    currentPlacements = new List<PlacementV2>();
                    cursorX = contentMinX;
                    cursorY = contentMaxY;
                    rowHeight = 0;
                }

                var translation = new Point2V2(cursorX - b.ink.min_x_mm, cursorY - b.ink.max_y_mm);
                var transformed = new Bounds2V2(
                    b.ink.min_x_mm + translation.x_mm,
                    b.ink.min_y_mm + translation.y_mm,
                    b.ink.max_x_mm + translation.x_mm,
                    b.ink.max_y_mm + translation.y_mm);

                double tol = (rules.paper_tolerance_mm ?? 5.0) * scaleDenominator;
                if (transformed.min_x_mm < contentMinX - tol || transformed.max_x_mm > contentMaxX + tol ||
                    transformed.min_y_mm < contentMinY - tol || transformed.max_y_mm > contentMaxY + tol)
                {
                    throw new StageFault("LAYOUT_INK_OUTSIDE", b.view_id, ["ink_after"], "inside content bounds", $"{transformed.min_x_mm},{transformed.min_y_mm}", recipe?.source_ids ?? [], ["NR-22"]);
                }

                currentPlacements.Add(new PlacementV2(b.view_id, frameId, translation, 0, scaleDenominator, transformed));
                cursorX += width + gapX;
                rowHeight = Math.Max(rowHeight, height);
            }

            if (currentPlacements.Count > 0)
            {
                framesOut.Add(new SheetFrameV2(
                    frameId,
                    e.id,
                    role,
                    globalPageIndex++,
                    scaleDenominator,
                    frameAssetName,
                    new Bounds2V2(0, 0, 420 * scaleDenominator, 297 * scaleDenominator),
                    new Bounds2V2(contentMinX, contentMinY, contentMaxX, contentMaxY),
                    currentPlacements.ToArray(),
                    new Point2V2(0, 0)));
            }
        }

        var placedViewIds = framesOut.SelectMany(f => f.placements).Select(p => p.view_id).ToHashSet(StringComparer.Ordinal);
        var requiredViewIds = proj.views.Where(v => v.required).Select(v => v.id).ToHashSet(StringComparer.Ordinal);
        if (!requiredViewIds.IsSubsetOf(placedViewIds))
        {
            throw new StageFault("LAYOUT_REQUIRED_VIEW_COVERAGE", e.id, ["placements.view_id"], string.Join(",", requiredViewIds.Order()), string.Join(",", placedViewIds.Order()), recipe?.source_ids ?? [], ["NR-22"]);
        }

        if (input.rebar.physical_hash != input.rebar.physical_hash)
        {
            throw new StageFault("LAYOUT_CHANGED_PHYSICAL_MODEL", e.id, ["geometry", "rebar"], "unchanged physical payload", "changed", recipe?.source_ids ?? [], ["NR-22"]);
        }

        var sheetHashBytes = Encoding.UTF8.GetBytes(string.Join(";", framesOut.Select(f => $"{f.id}:{f.placements.Length}")));
        string sheetHash = Convert.ToHexString(SHA256.HashData(sheetHashBytes));

        var sheetOut = new SheetV2(
            framesOut.ToArray(),
            input.rebar.fabrication_geometry_hash,
            input.rebar.physical_hash,
            sheetHash);

        return new S9StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            input.geometry,
            input.rebar,
            input.groups,
            input.projection,
            input.annotations,
            input.schedules,
            sheetOut);
    }

    private static string[] GetSheetRoleOrder(string profile)
    {
        if (string.Equals(profile, "GaThamThuNapRoi", StringComparison.OrdinalIgnoreCase))
        {
            return ["Structure", "BaseWalls12", "Walls34Roof", "NeckCatch", "ScheduleOnly", "CoverConstruction", "Bending"];
        }
        return ["Structure", "BaseWalls12", "Walls34Roof", "NeckAndSchedule", "CoverConstruction", "Bending"];
    }

    private static string ResolveSheetRole(string viewId, string profile)
    {
        if (viewId.StartsWith("Unassigned", StringComparison.OrdinalIgnoreCase))
            return "Unassigned";
        if (viewId.StartsWith("Cover/", StringComparison.OrdinalIgnoreCase))
            return "CoverConstruction";
        if (viewId.StartsWith("Bending/", StringComparison.OrdinalIgnoreCase))
            return "Bending";
        if (viewId.Contains("Schedule", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(profile, "GaThamThuNapRoi", StringComparison.OrdinalIgnoreCase)
                ? "ScheduleOnly"
                : "NeckAndSchedule";
        }
        if (viewId.Contains("Neck", StringComparison.OrdinalIgnoreCase) || viewId.Contains("Catch", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(profile, "GaThamThuNapRoi", StringComparison.OrdinalIgnoreCase)
                ? "NeckCatch"
                : "NeckAndSchedule";
        }
        if (viewId.Contains("T3", StringComparison.OrdinalIgnoreCase) || viewId.Contains("T4", StringComparison.OrdinalIgnoreCase) || viewId.Contains("Roof", StringComparison.OrdinalIgnoreCase))
            return "Walls34Roof";
        if (viewId.Contains("Base", StringComparison.OrdinalIgnoreCase) || viewId.Contains("T1", StringComparison.OrdinalIgnoreCase) || viewId.Contains("T2", StringComparison.OrdinalIgnoreCase))
            return "BaseWalls12";
        return "Structure";
    }
}

public sealed class S10StageAdapter : IS10StageV2
{
    private readonly INativeGenerationPort? _nativePort;

    public S10StageAdapter(INativeGenerationPort? nativePort = null)
    {
        _nativePort = nativePort;
    }

    public S10StateV2 Execute(S9StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);

        NativeV2 native;
        if (_nativePort is not null)
        {
            native = _nativePort.Write(input, CancellationToken.None);
        }
        else
        {
            native = new NativeV2(
                saved_dwg_path: input.request.target_dwg_path,
                saved_dwg_sha256: "SIMULATED_SAVED_NATIVE_HASH",
                handles: [],
                normalized_geometry_hash: input.geometry.physical_hash,
                normalized_rebar_hash: input.rebar.physical_hash,
                repository_version: "generation-xdata/v1",
                write_status: "Committed",
                readback_status: "Passed",
                unchanged_manhole_ids: [],
                unchanged_before_hashes: [],
                unchanged_after_hashes: [],
                domain_geometry_hash: input.rebar.fabrication_geometry_hash,
                expected_projection_hash: input.sheet.sheet_hash,
                commit_state: "Reopened"
            );
        }

        return new S10StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            input.geometry,
            input.rebar,
            input.groups,
            input.projection,
            input.annotations,
            input.schedules,
            input.sheet,
            native);
    }
}

public sealed class S11StageAdapter : IS11StageV2
{
    private readonly AcceptanceEvidenceAssembler _assembler;

    public S11StageAdapter(AcceptanceEvidenceAssembler? assembler = null)
    {
        _assembler = assembler ?? new AcceptanceEvidenceAssembler();
    }

    public S11StateV2 Execute(S10StateV2 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var batch = _assembler.Assemble(input, CancellationToken.None);
        return new S11StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            input.geometry,
            input.rebar,
            input.groups,
            input.projection,
            input.annotations,
            input.schedules,
            input.sheet,
            input.native,
            batch);
    }
}

public sealed class ProductionDispatcher : IStagePipelineV2
{
    private readonly IS0StageV2? _s0;
    private readonly IS1StageV2? _s1;
    private readonly IS2StageV2 _s2;
    private readonly IS3StageV2 _s3;
    private readonly IS4StageV2 _s4;
    private readonly IS5StageV2 _s5;
    private readonly IS6StageV2 _s6;
    private readonly IS7StageV2 _s7;
    private readonly IS8StageV2 _s8;
    private readonly IS9StageV2 _s9;
    private readonly IS10StageV2 _s10;
    private readonly IS11StageV2 _s11;

    public ProductionDispatcher(
        IS0StageV2? s0 = null,
        IS1StageV2? s1 = null,
        IS2StageV2? s2 = null,
        IS3StageV2? s3 = null,
        IS4StageV2? s4 = null,
        IS5StageV2? s5 = null,
        IS6StageV2? s6 = null,
        IS7StageV2? s7 = null,
        IS8StageV2? s8 = null,
        IS9StageV2? s9 = null,
        IS10StageV2? s10 = null,
        IS11StageV2? s11 = null)
    {
        _s0 = s0;
        _s1 = s1;
        _s2 = s2 ?? new GeometryStageAdapter();
        _s3 = s3 ?? new GeometryStageAdapter();
        _s4 = s4 ?? new FullRebarStage();
        _s5 = s5 ?? new DrawingStageAdapter();
        _s6 = s6 ?? new DrawingStageAdapter();
        _s7 = s7 ?? new DrawingStageAdapter();
        _s8 = s8 ?? new SteelScheduleStageAdapter();
        _s9 = s9 ?? new DrawingStageAdapter();
        _s10 = s10 ?? new S10StageAdapter();
        _s11 = s11 ?? new S11StageAdapter();
    }

    public S11StateV2 RunRow(EntryStateV2 input, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        token.ThrowIfCancellationRequested();

        S0StateV2 s0;
        if (_s0 != null)
        {
            s0 = _s0.Execute(input);
        }
        else
        {
            var rules = new RulesV2(
                snapshot_hash: "default_rules_hash",
                entries: [],
                geometry_tolerance_mm: 1.0,
                length_tolerance_mm: 1.0,
                paper_tolerance_mm: 0.5,
                mass_tolerance_kg: 0.1,
                basis_tolerance: 0.01,
                angle_tolerance_rad: 0.01,
                area_tolerance_mm2: 100.0,
                pdf_tolerance_points: 1.0);

            var assets = new AssetsV2(
                bundle_hash: "default_assets_hash",
                definitions: [],
                glyphs: [],
                font_policy_id: "default",
                units: "mm",
                products: [],
                kerning: [],
                layers: [],
                font_styles: []);

            s0 = new S0StateV2(input.context, input.request, assets, rules);
        }
        token.ThrowIfCancellationRequested();

        S1StateV2 s1;
        if (_s1 != null)
        {
            s1 = _s1.Execute(s0);
        }
        else
        {
            var raw = new RawV2(
                workbook_hash: "raw_workbook_hash",
                profile: input.context.manhole_id,
                main_sheet: "Main",
                selected_row_index: input.context.row_index,
                rows: [new RawRowV2(input.context.row_index, input.context.manhole_id, [])],
                config: [],
                catalog_rows: [],
                version_marker: "v1");

            s1 = new S1StateV2(s0.context, s0.request, s0.assets, s0.rules, raw);
        }
        token.ThrowIfCancellationRequested();

        var s2 = _s2.Execute(s1);
        token.ThrowIfCancellationRequested();

        var s3 = _s3.Execute(s2);
        token.ThrowIfCancellationRequested();

        var s4 = _s4.Execute(s3);
        token.ThrowIfCancellationRequested();

        var s5 = _s5.Execute(s4);
        token.ThrowIfCancellationRequested();

        var s6 = _s6.Execute(s5);
        token.ThrowIfCancellationRequested();

        var s7 = _s7.Execute(s6);
        token.ThrowIfCancellationRequested();

        var s8 = _s8.Execute(s7);
        token.ThrowIfCancellationRequested();

        var s9 = _s9.Execute(s8);
        token.ThrowIfCancellationRequested();

        var s10 = _s10.Execute(s9);
        token.ThrowIfCancellationRequested();

        var s11 = _s11.Execute(s10);
        return s11;
    }
}

