using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Application.StageContractsV2;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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

        if(raw.selected_row_index!=context.row_index)
            throw new StageFault("ROW_SELECTION_MISMATCH",context.manhole_id,["raw.selected_row_index","context.row_index"],context.row_index.ToString(),raw.selected_row_index.ToString());
        if(raw.profile is not("GaThamLienKhoi" or "GaThamNapRoi" or "GaThamThuNapRoi"))
            throw new StageFault("PROFILE_UNRESOLVED",context.manhole_id,["raw.profile"],"one exact supported VHG profile",raw.profile??"null");
        if(raw.rows is null||raw.config is null||raw.rows.Any(r=>r is null||r.cells is null))
            throw new StageFault("RAW_INPUT_MALFORMED",context.manhole_id,["raw.rows","raw.config"],"non-null arrays and records","null");
        var selected=raw.rows.Where(r=>r.row_index==context.row_index).ToArray();
        if(selected.Length!=1)
            throw new StageFault(selected.Length==0?"ROW_MISSING":"ROW_AMBIGUOUS",context.manhole_id,["raw.rows"],"exactly one row_index "+context.row_index,selected.Length.ToString());
        var row=selected[0];
        var selectedCells=row.cells.Concat(raw.config).ToArray();
        if(selectedCells.Any(c=>c is null||string.IsNullOrWhiteSpace(c.field_key)))
            throw new StageFault("RAW_INPUT_MALFORMED",context.manhole_id,["raw.rows[].cells","raw.config"],"non-null cell with exact field key","null/blank");
        var duplicate=selectedCells.GroupBy(c=>c.field_key,StringComparer.OrdinalIgnoreCase).FirstOrDefault(g=>g.Count()!=1);
        if(duplicate is not null)
            throw new StageFault("RAW_FIELD_AMBIGUOUS",context.manhole_id,[duplicate.Key],"one binding; row/config must not silently override",string.Join(";",duplicate.Select(c=>c.sheet+"!"+c.address)),duplicate.Select(c=>c.source_id).ToArray());

        RawCellV2? FindCell(string key) =>
            row.cells.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase))
            ?? raw.config.FirstOrDefault(c => string.Equals(c.field_key, key, StringComparison.OrdinalIgnoreCase));

        (string token, string kind, string[] sourceIds) ReadCell(RawCellV2? cell, string key, bool active, bool blankAllowed = false)
        {
            if (cell == null)
            {
                if (active && !blankAllowed)
                    throw new StageFault("ACTIVE_FIELD_BLANK", key, [key], "defined cell", "missing", []);
                return ("", "Blank", []);
            }
            string[] src = string.IsNullOrEmpty(cell.source_id) ? [] : [cell.source_id];
            if (!active)
                return (cell.text ?? cell.raw_number_text ?? "", cell.kind ?? "Blank", src);

            string token;
            string kind;
            if (string.Equals(cell.kind, "Formula", StringComparison.OrdinalIgnoreCase))
            {
                bool cacheProven=FormulaRecalculationProofV2.Matches(input,cell,out var cacheReason);
                if (!cell.cache_verified || !cacheProven || string.Equals(cell.cache_kind, "Blank", StringComparison.OrdinalIgnoreCase) || string.Equals(cell.cache_kind, "Error", StringComparison.OrdinalIgnoreCase))
                {
                    throw new StageFault("FORMULA_CACHE_UNVERIFIED", cell.field_key, [cell.address], "matching recalculation receipt", (cell.cache_text??"blank")+"; proof="+cacheReason, src);
                }
                token = cell.cache_text ?? "";
                kind = cell.cache_kind ?? "Text";
            }
            else
            {
                kind=cell.kind??"Blank";
                token=kind=="Number"?cell.raw_number_text??"":cell.text??"";
            }

            if (string.Equals(kind, "Error", StringComparison.OrdinalIgnoreCase))
            {
                throw new StageFault("EXCEL_CELL_ERROR", cell.field_key, [cell.address], "valid value", token, src);
            }

            if (string.IsNullOrWhiteSpace(token) && !blankAllowed)
            {
                throw new StageFault("ACTIVE_FIELD_BLANK", cell.field_key, [cell.address], "non-blank value", "blank", src);
            }

            return (token, kind, src);
        }

        CultureInfo InputTextCulture(string key,string[] sources)
        {
            var jobs=request.jobs.Where(j=>j.job_id==context.job_id).ToArray();
            if(jobs.Length!=1||string.IsNullOrWhiteSpace(jobs[0].numeric_culture))
                throw new StageFault("NUMERIC_CULTURE_UNRESOLVED",context.manhole_id,[key,"request.jobs[].numeric_culture"],"one selected job with explicit culture",jobs.Length.ToString(),sources);
            try{return CultureInfo.GetCultureInfo(jobs[0].numeric_culture);}
            catch(CultureNotFoundException){throw new StageFault("NUMERIC_CULTURE_UNRESOLVED",context.manhole_id,[key],"installed numeric culture",jobs[0].numeric_culture,sources);}
        }
        double ParseFinite(string key)
        {
            var cell=FindCell(key);
            var (token,kind,src)=ReadCell(cell,key,active:true,blankAllowed:false);
            if(kind is not("Number" or "Text"))
                throw new StageFault("CELL_KIND_INVALID",context.manhole_id,[cell?.address??key],"Number or explicit numeric Text",kind,src);
            // Number payloads are invariant OOXML; Text numbers follow the selected job culture.
            var culture=kind=="Number"?CultureInfo.InvariantCulture:InputTextCulture(key,src);
            var style=kind=="Number"?NumberStyles.Float:NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint;
            if(!double.TryParse(token,style,culture,out var value))
                throw new StageFault("NUMBER_FORMAT",context.manhole_id,[cell?.address??key],"finite scalar without grouping separators",token,src);
            if(!double.IsFinite(value))
                throw new StageFault("NUMBER_NOT_FINITE",context.manhole_id,[cell?.address??key],"finite scalar",token,src);
            return value;
        }
        double ParsePositive(string key)
        {
            var value=ParseFinite(key);
            if(value<=0)throw new StageFault("DIMENSION_NONPOSITIVE",context.manhole_id,[FindCell(key)?.address??key],">0",value.ToString("R",CultureInfo.InvariantCulture),[FindCell(key)!.source_id]);
            return value;
        }
        double ParseNonNegative(string key,bool active=true)
        {
            // Zero here represents an explicitly inactive optional component, never an active blank.
            if(!active)return 0;
            var value=ParseFinite(key);
            if(value<0)throw new StageFault("DIMENSION_NEGATIVE",context.manhole_id,[FindCell(key)?.address??key],">=0",value.ToString("R",CultureInfo.InvariantCulture),[FindCell(key)!.source_id]);
            return value;
        }

        string id = context.manhole_id;
        string displayName = row.name_raw ?? id;
        string profile = raw.profile;

        double clearX = ParsePositive("Length");
        double clearY = ParsePositive("Width");
        double clearH = ParsePositive("Height");
        double baseThick = ParsePositive("BaseThickness");
        double wallThick = ParsePositive("WallThickness");
        double roofThick = ParsePositive("RoofThickness");
        double baseExp = ParseNonNegative("BaseExpansion", true);
        double padThick = ParseNonNegative("PadThickness", true);
        double padExp = ParseNonNegative("PadExpansion", padThick > 0);
        double baseHaunch = ParseNonNegative("BaseHaunch", true);
        double topHaunch = profile == "GaThamLienKhoi" ? ParseNonNegative("TopHaunch", true) : 0.0;
        double beddingMortar = profile == "GaThamLienKhoi" ? 0.0 : ParseNonNegative("BeddingMortar", true);
        double neckHeight = ParseNonNegative("NeckHeight", true);
        bool neckActive = neckHeight > 0;
        double neckWall = neckActive ? ParsePositive("NeckThickness") : 0.0; // Explicitly inactive; never a guessed wall.

        var structCell = FindCell("NeckStructure");
        var (structToken, _, _) = ReadCell(structCell, "NeckStructure", active: neckActive, blankAllowed: true);
        bool neckReinforced = neckActive && string.Equals(structToken, "BTCT", StringComparison.OrdinalIgnoreCase);

        var coverTypeCell = FindCell("CoverType");
        var (coverTypeToken, _, _) = ReadCell(coverTypeCell, "CoverType", active: true, blankAllowed: false);
        string coverKind = "None";
        if (string.Equals(coverTypeToken, "Gang", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(coverTypeToken, "Composite", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(coverTypeToken, "Purchased", StringComparison.OrdinalIgnoreCase))
            coverKind = "Purchased";
        else if (string.Equals(coverTypeToken, "BTCT", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(coverTypeToken, "Panel", StringComparison.OrdinalIgnoreCase))
            coverKind = "Panel";
        else if (string.IsNullOrWhiteSpace(coverTypeToken) ||
                 string.Equals(coverTypeToken, "None", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(coverTypeToken, "Không", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(coverTypeToken, "Khong", StringComparison.OrdinalIgnoreCase))
            coverKind = "None";
        else
            throw new StageFault("COVER_KIND_UNRESOLVED", id, ["CoverType"], "Gang/BTCT/None", coverTypeToken);

        string? coverCode = null;
        if (coverKind == "Purchased")
        {
            var blockCell = FindCell("CoverBlock");
            var (blockToken, _, _) = ReadCell(blockCell, "CoverBlock", active: true, blankAllowed: false);
            coverCode = blockToken;
        }

        var sizeCell=FindCell("NeckSize");
        var (sizeToken,sizeKind,sizeSources)=ReadCell(sizeCell,"NeckSize",active:true,blankAllowed:false);
        if(sizeKind!="Text")throw new StageFault("CELL_KIND_INVALID",id,["NeckSize"],"Text tuple ClearX x ClearY",sizeKind,sizeSources);
        var sizeCulture=InputTextCulture("NeckSize",sizeSources);
        var sizeParts=Regex.Split(sizeToken,@"[xX×]");
        if(sizeParts.Length!=2||!double.TryParse(sizeParts[0].Trim(),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,sizeCulture,out var neckClearX)||!double.TryParse(sizeParts[1].Trim(),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,sizeCulture,out var neckClearY)||!double.IsFinite(neckClearX)||!double.IsFinite(neckClearY)||neckClearX<=0||neckClearY<=0)
            throw new StageFault("DIMENSIONS_FORMAT",id,[sizeCell?.address??"NeckSize"],"two finite positive dimensions in source tuple order",sizeToken,sizeSources);
        var posCell=FindCell("NeckPosition");
        var (neckPosToken,_,positionSources)=ReadCell(posCell,"NeckPosition",active:neckActive,blankAllowed:!neckActive);
        NeckPosition parsedPosition=NeckPosition.Center; // Only an inactive neck has no physical placement.
        if(neckActive&&!NeckPositionMap.TryParse(neckPosToken,out parsedPosition))
            throw new StageFault("NECK_POSITION_UNRESOLVED",id,[posCell?.address??"NeckPosition"],"one exact nine-position VHG label",neckPosToken,positionSources);
        string neckPos=NeckPositionMap.Name(parsedPosition);
        if(neckClearX>clearX||neckClearY>clearY)
            throw new StageFault("NECK_OPENING_OUTSIDE",id,[sizeCell?.address??"NeckSize","Length","Width"],"neck opening inside chamber clear size",sizeToken,sizeSources);
        var center=NeckPositionMap.Center(parsedPosition,clearX,clearY,neckClearX,neckClearY);
        var neckCenter=new Point2V2(center.X,center.Y);

        double frameOuterX = neckClearX + 2 * neckWall;
        double frameOuterY = neckClearY + 2 * neckWall;
        const double frameHeight = 100.0; // Chủ nhiệm 13/10: khung 100 mm; OWNER-RULES-THONG-KE-NHOM-20261013.

        double halfOuterX = clearX / 2.0 + wallThick;
        double halfOuterY = clearY / 2.0 + wallThick;
        double neckMinX = neckCenter.x_mm - frameOuterX / 2.0;
        double neckMaxX = neckCenter.x_mm + frameOuterX / 2.0;
        double neckMinY = neckCenter.y_mm - frameOuterY / 2.0;
        double neckMaxY = neckCenter.y_mm + frameOuterY / 2.0;
        if (neckActive && (neckMinX < -halfOuterX || neckMaxX > halfOuterX || neckMinY < -halfOuterY || neckMaxY > halfOuterY))
        {
            throw new StageFault("NECK_SUPPORT_OUTSIDE", id, ["NeckThickness", "NeckSize", "NeckPosition"], "contained in roof support", $"[{neckMinX},{neckMinY},{neckMaxX},{neckMaxY}]");
        }

        string rawElev=request.elevation_mode_override??ReadCell(FindCell("Elevation.C"),"Elevation.C",active:true).token;
        string elevMode=rawElev switch{"Absolute" or "Tuyệt đối"=>"Absolute","Symbolic" or "Tương đối"=>"Symbolic",_=>throw new StageFault("ELEVATION_MODE_UNRESOLVED",id,["Elevation.C","elevation_mode_override"],"Absolute/Tuyệt đối or Symbolic/Tương đối",rawElev)};
        double? baseElev=elevMode=="Absolute"?ParseFinite("BaseElevationM"):null; // F-026 permits signed metres.
        if(!double.IsFinite(request.body_scale)||request.body_scale<=0)
            throw new StageFault("SCALE_INVALID",id,["request.body_scale"],"finite >0",request.body_scale.ToString("R",CultureInfo.InvariantCulture));
        double bodyScale=request.body_scale;
        if(request.panel_scale is double requestedPanelScale&&(!double.IsFinite(requestedPanelScale)||requestedPanelScale<=0))
            throw new StageFault("SCALE_INVALID",id,["request.panel_scale"],"null or finite >0",requestedPanelScale.ToString("R",CultureInfo.InvariantCulture));
        double? panelScale = request.panel_scale;
        string noteStyle = FindCell("Annotation.NoteStyle")?.text ?? "NoteStyle1";
        string pitchSymbol = FindCell("Annotation.PitchSymbol")?.text ?? "@";

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

        // Parse pipes for 4 walls
        var pipes = new List<PipeSpecV2>();
        (string wall, string codeKey, string invertKey, string offsetKey)[] pipeSlots = [
            ("T1", "PipeCodeT1", "InvertT1", "PipeOffsetT1"),
            ("T2", "PipeCodeT2", "InvertT2", "PipeOffsetT2"),
            ("T3", "PipeCodeT3", "InvertT3", "PipeOffsetT3"),
            ("T4", "PipeCodeT4", "InvertT4", "PipeOffsetT4")
        ];
        foreach (var (wall, codeKey, invertKey, offsetKey) in pipeSlots)
        {
            var codeCell = FindCell(codeKey);
            var invCell = FindCell(invertKey);
            var offCell = FindCell(offsetKey);
            string code = codeCell?.text ?? codeCell?.raw_number_text ?? "";
            string inv = invCell?.text ?? invCell?.raw_number_text ?? "";
            bool hasCode = !string.IsNullOrWhiteSpace(code) && code != "0" && code != "None" && code != "Không";
            bool hasInv = !string.IsNullOrWhiteSpace(inv);
            if (!hasCode && !hasInv) continue;
            if (hasCode ^ hasInv)
                throw new StageFault("PIPE_PAIR_INCOMPLETE", id, [codeKey, invertKey], "both present or both blank", $"code={code}, inv={inv}");

            if (!double.TryParse(inv, NumberStyles.Any, CultureInfo.InvariantCulture, out var invertVal))
                throw new StageFault("ACTIVE_FIELD_BLANK", invertKey, [invCell?.address ?? invertKey], "finite number", inv);

            double offsetU = 0.0;
            if (offCell != null && double.TryParse(offCell.text ?? offCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var offVal))
                offsetU = offVal;

            double dia = 300, w = 300, h = 300, wallT = 50, mortar = 30;
            var matchD = Regex.Match(code, @"[dD](\d+)");
            if (matchD.Success && double.TryParse(matchD.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDia))
            {
                dia = parsedDia; w = dia; h = dia;
                wallT = Math.Max(50, dia * 0.1);
            }
            var mCell = FindCell("PipeMortar");
            if (mCell != null && double.TryParse(mCell.text ?? mCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var mVal))
                mortar = mVal;

            var pSources = new List<string>();
            if (codeCell != null && !string.IsNullOrEmpty(codeCell.source_id)) pSources.Add(codeCell.source_id);
            if (invCell != null && !string.IsNullOrEmpty(invCell.source_id)) pSources.Add(invCell.source_id);
            if (offCell != null && !string.IsNullOrEmpty(offCell.source_id)) pSources.Add(offCell.source_id);

            pipes.Add(new PipeSpecV2(
                $"{id}:{wall}:Pipe", wall, code, matchD.Success ? "Round" : "Box",
                invertVal, offsetU, w, h, wallT, mortar, 0, 0, null, pSources.ToArray()
            ));
        }

        // Parse Catch Basin
        CatchSpecV2? catchSpec = null;
        if (profile == "GaThamThuNapRoi")
        {
            var cCell = FindCell("Catch.C");
            var dCell = FindCell("Catch.D");
            string owner = (cCell?.text ?? "Thân ga") switch { "Cổ ga" => "Neck", _ => "Body" };
            string posRaw = dCell?.text ?? "T1-Center";
            string wall = "T1";
            string pos = "Center";
            var parts = posRaw.Split(['-', ' ', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) wall = parts[0];
            if (parts.Length > 1) pos = parts[1];
            if (owner == "Neck") pos = "Center";

            double ow = 400, oh = 300, depth = 500, cHeight = 600, cWall = 150, cBase = 150, cPad = 0, cPadExp = 0;
            bool reinforced = false;
            var fCell = FindCell("Catch.F");
            if (fCell != null && !string.IsNullOrWhiteSpace(fCell.text))
            {
                var fParts = Regex.Split(fCell.text, @"[xX×]");
                if (fParts.Length == 2 && double.TryParse(fParts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var fw) && double.TryParse(fParts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var fh))
                { ow = fw; oh = fh; }
            }
            var gCell = FindCell("Catch.G");
            if (gCell != null && !string.IsNullOrWhiteSpace(gCell.text))
            {
                var gParts = Regex.Split(gCell.text, @"[xX×]");
                if (gParts.Length == 2 && double.TryParse(gParts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var gw) && double.TryParse(gParts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var gh))
                { depth = gw; cHeight = gh; }
            }
            var hCell = FindCell("Catch.H"); if (hCell != null && double.TryParse(hCell.text ?? hCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var hv)) cWall = hv;
            var iCell = FindCell("Catch.I"); if (iCell != null && double.TryParse(iCell.text ?? iCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var iv)) cBase = iv;
            var jCell = FindCell("Catch.J"); if (jCell != null && double.TryParse(jCell.text ?? jCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var jv)) cPad = jv;
            var kCell = FindCell("Catch.K"); if (kCell != null && double.TryParse(kCell.text ?? kCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var kv)) cPadExp = kv;
            var lCell = FindCell("Catch.L"); if (lCell != null && string.Equals(lCell.text, "BTCT", StringComparison.OrdinalIgnoreCase)) reinforced = true;

            var catchSources = new List<string>();
            foreach (var k in new[] { "Catch.C", "Catch.D", "Catch.E", "Catch.F", "Catch.G", "Catch.H", "Catch.I", "Catch.J", "Catch.K", "Catch.L", "Catch.M" })
            {
                var c = FindCell(k);
                if (c != null && !string.IsNullOrEmpty(c.source_id)) catchSources.Add(c.source_id);
            }

            catchSpec = new CatchSpecV2(
                active: true, owner: owner, wall: wall, position: pos, raw_position: posRaw,
                offset_u_mm: 0.0, opening_width_mm: ow, opening_height_mm: oh,
                clear_depth_mm: depth, clear_height_mm: cHeight, wall_mm: cWall, base_mm: cBase,
                pad_mm: cPad, pad_expansion_mm: cPadExp, reinforced: reinforced,
                product_code: "SCR", source_ids: catchSources.ToArray()
            );
        }

        // Parse Panel Spec
        PanelSpecV2? panelSpec = null;
        if (coverKind == "Panel")
        {
            int count = 2;
            var pCountCell = FindCell("PanelCount");
            if (pCountCell != null && int.TryParse(pCountCell.text ?? pCountCell.raw_number_text, out var pCountVal) && pCountVal > 0)
                count = pCountVal;
            string axis = "X";
            var dirCell = FindCell("PanelDirection");
            if (dirCell != null && !string.IsNullOrWhiteSpace(dirCell.text))
                axis = dirCell.text.Trim();
            double gap = 10.0;
            var gapCell = FindCell("PanelGap");
            if (gapCell != null && double.TryParse(gapCell.text ?? gapCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var gapVal))
                gap = gapVal;
            double covH = 100.0;
            var hCell = FindCell("CoverHeight");
            if (hCell != null && double.TryParse(hCell.text ?? hCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var chVal))
                covH = chVal;

            var panelSources = new List<string>();
            foreach (var k in new[] { "PanelCount", "PanelDirection", "PanelGap", "CoverHeight", "NeckHaunchSize" })
            {
                var c = FindCell(k);
                if (c != null && !string.IsNullOrEmpty(c.source_id)) panelSources.Add(c.source_id);
            }

            panelSpec = new PanelSpecV2(
                active: true, contract_id: $"{id}:PanelContract", count: count,
                split_axis: axis, gap_mm: gap, thickness_mm: covH, seat_expansion_mm: 0, seat_height_mm: 0,
                source_ids: panelSources.ToArray(), support_mode: "Recess"
            );
        }

        // Populate Members
        var members = new List<MemberSpecV2>();
        var baseSources = new[] { "Length", "Width", "BaseThickness" }.Select(FindCell).Where(c => c != null).Select(c => c!.source_id).ToArray();
        members.Add(new MemberSpecV2($"{id}:Base:Bottom:Layer1:U:MainMesh", $"{id}:Base", "Base", "Bottom", "Layer1", "U", "MainMesh", active: true, null, 12, 150.0, "Equalized", 50.0, 0, 0, "0M", "Recipe:Mesh", [], null, baseSources));
        members.Add(new MemberSpecV2($"{id}:Base:Bottom:Layer2:V:MainMesh", $"{id}:Base", "Base", "Bottom", "Layer2", "V", "MainMesh", active: true, null, 12, 150.0, "Equalized", 50.0, 0, 0, "0M", "Recipe:Mesh", [], null, baseSources));
        members.Add(new MemberSpecV2($"{id}:Base:Top:Layer1:U:MainMesh", $"{id}:Base", "Base", "Top", "Layer1", "U", "MainMesh", active: true, null, 12, 150.0, "Equalized", 50.0, 0, 0, "0M", "Recipe:Mesh", [], null, baseSources));
        members.Add(new MemberSpecV2($"{id}:Base:Top:Layer2:V:MainMesh", $"{id}:Base", "Base", "Top", "Layer2", "V", "MainMesh", active: true, null, 12, 150.0, "Equalized", 50.0, 0, 0, "0M", "Recipe:Mesh", [], null, baseSources));

        var dowelFlagCell = FindCell("HasDowel");
        bool hasDowel = profile != "GaThamLienKhoi" && (dowelFlagCell != null && (string.Equals(dowelFlagCell.text, "Có", StringComparison.OrdinalIgnoreCase) || string.Equals(dowelFlagCell.text, "true", StringComparison.OrdinalIgnoreCase) || dowelFlagCell.boolean == true));
        if (hasDowel)
        {
            var dowelSources = new[] { "HasDowel", "DowelSpec", "DowelLength" }.Select(FindCell).Where(c => c != null).Select(c => c!.source_id).ToArray();
            members.Add(new MemberSpecV2($"{id}:Roof:Bottom:None:Vertical:Dowel", $"{id}:Roof", "Roof", "Bottom", "None", "Vertical", "Dowel", active: true, null, 18, null, "Equalized", 50.0, 0, 0, null, "Recipe:Dowel", [], 4, dowelSources));
        }

        // Collect all parsed fields
        var allCells = row.cells.Concat(raw.config).DistinctBy(c => c.field_key, StringComparer.OrdinalIgnoreCase).ToArray();
        var parsedFields = new List<ParsedFieldV2>();
        foreach (var c in allCells)
        {
            bool act = true;
            string? inactReason = null;
            if (c.field_key.StartsWith("Catch.", StringComparison.OrdinalIgnoreCase) && profile != "GaThamThuNapRoi")
            { act = false; inactReason = "CATCH_PROFILE_AND_STEEL"; }
            else if (c.field_key.StartsWith("Panel.", StringComparison.OrdinalIgnoreCase) && coverKind != "Panel")
            { act = false; inactReason = "DESIGNED_PANEL"; }
            else if (c.field_key.StartsWith("Neck", StringComparison.OrdinalIgnoreCase) && !c.field_key.Equals("NeckHeight", StringComparison.OrdinalIgnoreCase) && !neckActive)
            { act = false; inactReason = "NECK_HEIGHT_POSITIVE"; }
            else if ((c.field_key.Equals("DowelSpec", StringComparison.OrdinalIgnoreCase) || c.field_key.Equals("DowelLength", StringComparison.OrdinalIgnoreCase)) && (!hasDowel || profile == "GaThamLienKhoi"))
            { act = false; inactReason = "DOWEL_ON"; }

            double? num = null;
            if (act && double.TryParse(c.text ?? c.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var nVal))
                num = nVal;

            parsedFields.Add(new ParsedFieldV2(
                field_key: c.field_key,
                sheet: c.sheet ?? "Main",
                address: c.address ?? c.field_key,
                row_index: context.row_index,
                active: act,
                inactive_reason: inactReason,
                kind: c.kind ?? "Text",
                number: num,
                integer: num.HasValue ? (int?)Math.Truncate(num.Value) : null,
                text: act ? c.text : null,
                flag: c.boolean,
                tuple_mm: [],
                diameter_mm: null,
                pitch_mm: null,
                length_mm: null,
                count: null,
                tokens: [],
                unit: "mm",
                source_ids: string.IsNullOrEmpty(c.source_id) ? [] : [c.source_id]
            ));
        }

        var allSources = allCells.Select(c => c.source_id).Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.Ordinal).ToArray();

        var effective = new EffectiveV2(
            id, displayName, profile, clearX, clearY, clearH,
            baseThick, wallThick, roofThick, baseExp, padThick, padExp,
            baseHaunch, topHaunch, beddingMortar, neckActive,
            neckClearX, neckClearY, null, null, neckWall, neckHeight,
            neckPos, neckCenter, neckReinforced, coverKind, coverCode,
            0.0, frameOuterX, frameOuterY, frameHeight, baseElev,
            elevMode, bodyScale, panelScale, noteStyle, pitchSymbol,
            datums.ToArray(), members.ToArray(), pipes.ToArray(), catchSpec, panelSpec,
            allSources, parsedFields.ToArray(), contactEquations.ToArray()
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

        if (e.pad_mm < 0)
            throw new StageFault("PAD_THICKNESS_NEGATIVE", M, ["effective.pad_mm"], ">=0", e.pad_mm.ToString(CultureInfo.InvariantCulture));

        if (e.profile == "GaThamThuNapRoi" && (e.@catch == null || !e.@catch.active))
            throw new StageFault("CATCH_REQUIRED_BUT_INACTIVE", e.id, ["catch.active"], "true for type3", "false", e.source_ids);

        double baseBottom = e.datums.FirstOrDefault(d => d.surface_id == "BaseBottom")?.z_mm ?? -e.base_mm;
        double roofBottom = e.datums.FirstOrDefault(d => d.surface_id == "RoofBottom")?.z_mm ?? e.clear_h_mm;
        double roofTop = e.datums.FirstOrDefault(d => d.surface_id == "RoofTop")?.z_mm ?? (e.clear_h_mm + e.roof_mm);
        double wallTop = e.datums.FirstOrDefault(d => d.surface_id == "WallTop")?.z_mm ?? (roofBottom - e.bedding_mortar_mm);

        var planFrame = new FrameV2($"{M}:Plan", new Point3V2(0, 0, 0), new Vector3V2(1, 0, 0), new Vector3V2(0, 1, 0), new Vector3V2(0, 0, 1), 1);
        string bodyAssembly = e.profile == "GaThamLienKhoi" ? $"{M}:Monolithic" : $"{M}:Body";
        string roofAssembly = e.profile == "GaThamLienKhoi" ? $"{M}:Monolithic" : $"{M}:Roof";

        var components = new List<ComponentV2>();
        var openings = new List<OpeningV2>();
        var joints = new List<JointV2>();
        var products = new List<ProductPlacementV2>();

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

        ComponentV2 MakePrism(string key, string kind, string assembly, string material, FrameV2 frame, LoopV2[] loops, double depth0, double depth1, string[] sources, string[]? voidIds = null)
        {
            string compId = $"{M}:{key}";
            var part = new SolidPartV2($"{compId}:Blank", "Union", frame, loops, depth0, depth1);
            double z0 = depth0;
            double z1 = depth1;
            double thickness = depth1 - depth0;
            return new ComponentV2(compId, kind, assembly, material, frame, loops, z0, z1, thickness, [], voidIds ?? [], sources, [part], key, kind);
        }

        if (e.pad_mm > 0)
        {
            var padLoops = new[] { RectLoop("Pad/Outer", -padX, -padY, padX, padY, 0, "Outer") };
            components.Add(MakePrism("Pad", "Pad", $"{M}:Pad", "Bedding", planFrame, padLoops, baseBottom - e.pad_mm, baseBottom, ["PadThickness", "PadExpansion"]));
        }

        var baseLoops = new[] { RectLoop("Base/Outer", -baseX, -baseY, baseX, baseY, 0, "Outer") };
        components.Add(MakePrism("Base", "Base", bodyAssembly, "Concrete", planFrame, baseLoops, baseBottom, 0, ["BaseThickness", "BaseExpansion"]));

        var wallVoids = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            ["T1"] = [], ["T2"] = [], ["T3"] = [], ["T4"] = []
        };

        // Pipe openings
        foreach (var p in e.pipes)
        {
            string pipeHostId = $"{M}:Wall/{p.wall}";
            double pw = p.inner_width_mm + 2 * p.wall_thickness_mm + 2 * p.mortar_mm;
            double ph = p.inner_height_mm + 2 * p.wall_thickness_mm + 2 * p.mortar_mm;
            double pu0 = p.offset_u_mm - pw / 2.0;
            double pu1 = p.offset_u_mm + pw / 2.0;
            double pv0 = p.invert_mm;
            double pv1 = p.invert_mm + ph;
            var cutLoop = RectLoop($"Pipe/{p.wall}/Cutout", pu0, pv0, pu1, pv1, 0, "Cutout");
            var pipeOpening = new OpeningV2(
                id: p.id,
                kind: p.kind,
                host_ids: [pipeHostId],
                frame: planFrame,
                inner: [cutLoop],
                pipe_outer: [],
                concrete_cutout: [cutLoop],
                depth_min_mm: 0,
                depth_max_mm: e.wall_mm,
                invert_mm: p.invert_mm,
                clearance_rule_id: "NR-01",
                source_ids: p.source_ids
            );
            openings.Add(pipeOpening);
            if (wallVoids.TryGetValue(p.wall, out var list))
                list.Add(pipeOpening.id);
        }

        // Catch reveal opening on host wall
        if (e.@catch != null && e.@catch.active)
        {
            string catchWall = e.@catch.wall ?? "T1";
            string catchHostId = $"{M}:Wall/{catchWall}";
            double cw = e.@catch.opening_width_mm;
            double ch = e.@catch.opening_height_mm;
            double cu0 = e.@catch.offset_u_mm - cw / 2.0;
            double cu1 = e.@catch.offset_u_mm + cw / 2.0;
            double cv0 = 0.0;
            double cv1 = ch;
            var catchCutLoop = RectLoop($"Catch/{catchWall}/Cutout", cu0, cv0, cu1, cv1, 0, "Cutout");
            var catchOpening = new OpeningV2(
                id: $"{M}:CatchOpening",
                kind: "CatchOpening",
                host_ids: [catchHostId],
                frame: planFrame,
                inner: [catchCutLoop],
                pipe_outer: [],
                concrete_cutout: [catchCutLoop],
                depth_min_mm: 0,
                depth_max_mm: e.wall_mm,
                invert_mm: 0,
                clearance_rule_id: "NR-01",
                source_ids: e.@catch.source_ids
            );
            openings.Add(catchOpening);
            if (wallVoids.TryGetValue(catchWall, out var cList))
                cList.Add(catchOpening.id);
        }

        (string stripKey, string wallSlot, double x0, double y0, double x1, double y1)[] strips = [
            ("Wall/T1", "T1", -outerX, halfY, outerX, outerY),
            ("Wall/T2", "T2", halfX, -halfY, outerX, halfY),
            ("Wall/T3", "T3", -outerX, -outerY, outerX, -halfY),
            ("Wall/T4", "T4", -outerX, -halfY, -halfX, halfY)
        ];
        foreach (var (sKey, wSlot, sx0, sy0, sx1, sy1) in strips)
        {
            var wallLoops = new[] { RectLoop($"{sKey}/Outer", sx0, sy0, sx1, sy1, 0, "Outer") };
            var vIds = wallVoids.TryGetValue(wSlot, out var wv) ? wv.ToArray() : [];
            components.Add(MakePrism(sKey, "Wall", bodyAssembly, "Concrete", planFrame, wallLoops, 0, wallTop, ["Length", "Width", "Height", "WallThickness"], voidIds: vIds));
        }

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
            bool dowelRequired = e.members.Any(m => m.family == "Dowel" && m.active);
            joints.Add(new JointV2(jointId, bodyAssembly, roofAssembly, roofBottom, mortarId, dowelRequired, ["BeddingMortar", "HasDowel"]));
        }
        else
        {
            if (e.members.Any(m => m.family == "Dowel" && m.active))
                throw new StageFault("DOWEL_PROFILE_CONFLICT", M, ["HasDowel", "profile"], "type2/3", e.profile);
        }

        double nx0 = e.neck_center.x_mm - e.neck_clear_x_mm / 2.0;
        double nx1 = e.neck_center.x_mm + e.neck_clear_x_mm / 2.0;
        double ny0 = e.neck_center.y_mm - e.neck_clear_y_mm / 2.0;
        double ny1 = e.neck_center.y_mm + e.neck_clear_y_mm / 2.0;
        var accessLoop = RectLoop("Roof/Access", nx0, ny0, nx1, ny1, 0, "Inner");
        string roofAccessId = $"{M}:RoofAccess";
        openings.Add(new OpeningV2(roofAccessId, "RoofAccess", [$"{M}:Roof"], planFrame, [accessLoop], [], [accessLoop], roofBottom, roofTop, null, null, e.source_ids));

        var roofLoops = new[] { RectLoop("Roof/Blank", -outerX, -outerY, outerX, outerY, 0, "Outer") };
        components.Add(MakePrism("Roof", "Roof", roofAssembly, "Concrete", planFrame, roofLoops, roofBottom, roofTop, ["RoofThickness", "Height"], voidIds: [roofAccessId]));

        if (e.neck_active)
        {
            double ox0 = nx0 - e.neck_wall_mm;
            double ox1 = nx1 + e.neck_wall_mm;
            double oy0 = ny0 - e.neck_wall_mm;
            double oy1 = ny1 + e.neck_wall_mm;
            var neckLoops = new[] { RectLoop("Neck/Outer", ox0, oy0, ox1, oy1, 0, "Outer"), accessLoop };
            double neckTop = e.datums.FirstOrDefault(d => d.surface_id == "NeckTop")?.z_mm ?? (roofTop + e.neck_height_mm);
            components.Add(MakePrism("Neck", "Neck", roofAssembly, "Concrete", planFrame, neckLoops, roofTop, neckTop, ["NeckThickness", "NeckHeight", "NeckSize"]));

            // Frame 100mm
            double frameBottom = neckTop;
            double frameTop = frameBottom + 100.0;
            var frameLoops = new[] {
                RectLoop("Frame/Outer", -e.frame_outer_x_mm / 2.0, -e.frame_outer_y_mm / 2.0, e.frame_outer_x_mm / 2.0, e.frame_outer_y_mm / 2.0, 0, "Outer"),
                RectLoop("Frame/Inner", nx0, ny0, nx1, ny1, 0, "Inner")
            };
            components.Add(MakePrism("Frame", "Frame", $"{M}:Frame", "CastIron", planFrame, frameLoops, frameBottom, frameTop, ["FrameHeight"]));
        }

        // Catch Basin Chamber and Pad
        if (e.@catch != null && e.@catch.active)
        {
            double cwOuter = e.@catch.clear_depth_mm + 2 * e.@catch.wall_mm;
            double chOuter = e.@catch.opening_width_mm + 2 * e.@catch.wall_mm;
            double cx0 = -cwOuter / 2.0;
            double cx1 = cwOuter / 2.0;
            double cy0 = outerY;
            double cy1 = outerY + chOuter;
            var catchLoops = new[] { RectLoop("Catch/Outer", cx0, cy0, cx1, cy1, 0, "Outer") };
            components.Add(MakePrism("CatchBasin/Chamber", "CatchChamber", $"{M}:CatchBasin", "Concrete", planFrame, catchLoops, -e.@catch.base_mm, e.@catch.clear_height_mm, e.@catch.source_ids));

            if (e.@catch.pad_mm > 0)
            {
                double cpOuterX = cwOuter + 2 * e.@catch.pad_expansion_mm;
                double cpOuterY = chOuter + 2 * e.@catch.pad_expansion_mm;
                var catchPadLoops = new[] { RectLoop("Catch/Pad/Outer", -cpOuterX / 2.0, outerY - e.@catch.pad_expansion_mm, cpOuterX / 2.0, outerY + chOuter + e.@catch.pad_expansion_mm, 0, "Outer") };
                components.Add(MakePrism("CatchBasin/Pad", "Pad", $"{M}:CatchPad", "Bedding", planFrame, catchPadLoops, -e.@catch.base_mm - e.@catch.pad_mm, -e.@catch.base_mm, e.@catch.source_ids));
            }
        }

        // Panel instances
        if (e.panel != null && e.panel.active)
        {
            double pWidth = e.neck_clear_x_mm / e.panel.count;
            for (int i = 0; i < e.panel.count; i++)
            {
                double px0 = nx0 + i * pWidth;
                double px1 = px0 + pWidth - e.panel.gap_mm;
                var panelLoops = new[] { RectLoop($"Panel/{i}/Outer", px0, ny0, px1, ny1, 0, "Outer") };
                components.Add(MakePrism($"CoverPanel/{i}", "CoverPanel", $"{M}:CoverPanel", "Concrete", planFrame, panelLoops, roofTop, roofTop + e.panel.thickness_mm, e.panel.source_ids));
            }
        }

        // Haunches
        if (e.base_haunch_mm > 0)
        {
            var bHaunchLoops = new[] { RectLoop("Haunch/Base/Outer", -halfX, -halfY, halfX, halfY, 0, "Outer") };
            components.Add(MakePrism("Haunch/Base", "Haunch", bodyAssembly, "Concrete", planFrame, bHaunchLoops, 0, e.base_haunch_mm, ["BaseHaunch"]));
        }
        if (e.top_haunch_mm > 0)
        {
            var tHaunchLoops = new[] { RectLoop("Haunch/Top/Outer", -halfX, -halfY, halfX, halfY, 0, "Outer") };
            components.Add(MakePrism("Haunch/Top", "Haunch", bodyAssembly, "Concrete", planFrame, tHaunchLoops, wallTop - e.top_haunch_mm, wallTop, ["TopHaunch"]));
        }

        // Product Placements
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

        string physicalHash = HashPhysicalGeometry(components, openings, joints, products);
        var geometry = new GeometryV2(components.ToArray(), openings.ToArray(), joints.ToArray(), physicalHash, products.ToArray());
        return new S3StateV2(input.context, input.request, input.assets, input.rules, input.raw, e, geometry);
    }

    public static string HashPhysicalGeometry(
        IReadOnlyList<ComponentV2> components,
        IReadOnlyList<OpeningV2> openings,
        IReadOnlyList<JointV2> joints,
        IReadOnlyList<ProductPlacementV2> products)
    {
        var sb = new StringBuilder();
        string Fmt(double v) => v.ToString("R", CultureInfo.InvariantCulture);
        string FmtPt(Point3V2 p) => $"[{Fmt(p.x_mm)},{Fmt(p.y_mm)},{Fmt(p.z_mm)}]";
        string FmtVec(Vector3V2 v) => $"[{Fmt(v.x)},{Fmt(v.y)},{Fmt(v.z)}]";
        string FmtFrame(FrameV2 f) => $"{f.id}:{FmtPt(f.origin)}:{FmtVec(f.axis_u)}:{FmtVec(f.axis_v)}:{FmtVec(f.normal)}";
        void AppendLoop(LoopV2 l)
        {
            sb.Append($"LOOP:{l.id}:{l.role}:{l.closed}:{Fmt(l.signed_area_mm2)};");
            foreach (var c in l.curves)
            {
                sb.Append($"CRV:{c.id}:{c.kind}:{FmtPt(c.start)}:{FmtPt(c.end)}:{c.role}:");
                if (c.center != null) sb.Append(FmtPt(c.center)); else sb.Append("null");
                sb.Append(':');
                if (c.radius_mm.HasValue) sb.Append(Fmt(c.radius_mm.Value)); else sb.Append("null");
                sb.Append(':');
                if (c.sweep_rad.HasValue) sb.Append(Fmt(c.sweep_rad.Value)); else sb.Append("null");
                sb.Append(';');
            }
        }

        foreach (var c in components.OrderBy(c => c.id, StringComparer.Ordinal))
        {
            sb.Append($"COMP:{c.id}:{c.kind}:{c.assembly_id}:{c.material_id}:{c.local_key}:{c.material_role}:");
            sb.Append($"{Fmt(c.thickness_mm)}:{Fmt(c.z_min_mm)}:{Fmt(c.z_max_mm)}:");
            sb.Append(FmtFrame(c.frame));
            sb.Append(":VOIDS:[");
            sb.Append(string.Join(",", c.void_ids.OrderBy(v => v, StringComparer.Ordinal)));
            sb.Append("]:LOOPS:[");
            foreach (var l in c.loops)
                AppendLoop(l);
            sb.Append("]:PARTS:[");
            foreach (var p in c.solid_parts)
            {
                sb.Append($"PART:{p.id}:{p.operation}:{Fmt(p.depth_min_mm)}:{Fmt(p.depth_max_mm)}:");
                sb.Append(FmtFrame(p.frame));
                foreach (var l in p.loops)
                    AppendLoop(l);
            }
            sb.Append("];");
        }

        foreach (var o in openings.OrderBy(o => o.id, StringComparer.Ordinal))
        {
            sb.Append($"OPN:{o.id}:{o.kind}:{Fmt(o.depth_min_mm)}:{Fmt(o.depth_max_mm)}:{o.clearance_rule_id ?? "null"}:");
            if (o.invert_mm.HasValue) sb.Append(Fmt(o.invert_mm.Value)); else sb.Append("null");
            sb.Append(":HOSTS:[");
            sb.Append(string.Join(",", o.host_ids.OrderBy(h => h, StringComparer.Ordinal)));
            sb.Append("]:FRAME:");
            sb.Append(FmtFrame(o.frame));
            sb.Append(":INNER:[");
            foreach (var l in o.inner) AppendLoop(l);
            sb.Append("]:PIPE_OUTER:[");
            foreach (var l in o.pipe_outer) AppendLoop(l);
            sb.Append("]:CONCRETE_CUTOUT:[");
            foreach (var l in o.concrete_cutout) AppendLoop(l);
            sb.Append("];");
        }

        foreach (var j in joints.OrderBy(j => j.id, StringComparer.Ordinal))
        {
            sb.Append($"JNT:{j.id}:{j.lower_assembly_id}:{j.upper_assembly_id}:{Fmt(j.z_mm)}:{j.mortar_component_id ?? "null"}:{j.dowel_required};");
        }

        foreach (var p in products.OrderBy(p => p.id, StringComparer.Ordinal))
        {
            sb.Append($"PRD:{p.id}:{p.contract_id}:{Fmt(p.physical_scale)}:{FmtPt(p.anchor)}:{Fmt(p.rotation_rad)};");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
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
            var basin = spec.Input.CatchBasinSpec;
            var catchMesh = spec.Input.MainRebar?.Accessories?.CatchMesh;
            int catchDia = catchMesh?.DiameterMm ?? 8;
            double catchPitch = catchMesh?.SpacingMm ?? 150.0;
            double catchCover = spec.Input.ConfigRawCells.TryGetValue("Protect.I", out var piCell) && double.TryParse(piCell.Text ?? piCell.CachedText, NumberStyles.Any, CultureInfo.InvariantCulture, out var piVal)
                ? piVal
                : (spec.Input.UnsupportedRawFields.TryGetValue("Protect.I", out var piCell2) && double.TryParse(piCell2.Text ?? piCell2.CachedText, NumberStyles.Any, CultureInfo.InvariantCulture, out var piVal2)
                    ? piVal2
                    : 25.0);

            var hostId = basin.Owner == CatchBasinOwner.Body ? $"Wall/{basin.Wall}" : "Neck";
            var host = geometry.Components.FirstOrDefault(c => c.Id == hostId);
            double clearWidth = basin.Wall is WallId.T1 or WallId.T3 ? spec.Input.L : spec.Input.B;
            WallFrame catchFrame;
            if (basin.Owner == CatchBasinOwner.Neck && host != null && host.Footprint.InnerLoops.Length == 1)
            {
                var inner = host.Footprint.InnerLoops[0];
                double minX = inner.Vertices.Min(p => p.X), maxX = inner.Vertices.Max(p => p.X);
                double minY = inner.Vertices.Min(p => p.Y), maxY = inner.Vertices.Max(p => p.Y);
                double width = maxX - minX, depth = maxY - minY;
                clearWidth = basin.Wall is WallId.T1 or WallId.T3 ? width : depth;
                var centered = WallFrame.Create(basin.Wall, width, depth);
                catchFrame = new WallFrame(basin.Wall,
                    new Point3Mm(centered.Origin.X + (minX + maxX) / 2, centered.Origin.Y + (minY + maxY) / 2, 0),
                    centered.Left, centered.Outward, centered.Up);
            }
            else
            {
                catchFrame = WallFrame.Create(basin.Wall, spec.Input.L, spec.Input.B);
            }

            double centerU = basin.Position switch
            {
                HorizontalPosition.Left => -clearWidth / 2 + basin.OpeningWidthMm / 2 + basin.OffsetMm,
                HorizontalPosition.Right => clearWidth / 2 - basin.OpeningWidthMm / 2 + basin.OffsetMm,
                _ => basin.OffsetMm
            };
            double hostTop = host?.ZRange.End ?? spec.Input.H;
            double bottomZ = hostTop - basin.ChamberClearHeightMm;
            double floorTopZ = bottomZ + basin.BaseThicknessMm;
            double hostThickness = basin.Owner == CatchBasinOwner.Body ? spec.Input.WallThicknessMm : 125.0;

            var rebarSpec = new CatchRebarSpec(basin.ChamberClearWidthMm, basin.ChamberClearDepthMm, catchDia, catchPitch, catchCover);
            var catchRebar = CatchBasinRebarPlanner.PlanCatch(geometry.CatchBasin, rebarSpec, policy, catchFrame, centerU, hostThickness, bottomZ, floorTopZ, hostTop);
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
        var blockedFeatures = new List<(MemberSpecV2 spec, string reason)>();

        double halfX = e.clear_x_mm / 2.0;
        double halfY = e.clear_y_mm / 2.0;
        double outerX = halfX + e.wall_mm;
        double outerY = halfY + e.wall_mm;
        double baseX = outerX + e.base_expansion_mm;
        double baseY = outerY + e.base_expansion_mm;
        double baseBottom = e.datums.FirstOrDefault(d => d.surface_id == "BaseBottom")?.z_mm ?? -e.base_mm;
        double roofBottom = e.datums.FirstOrDefault(d => d.surface_id == "RoofBottom")?.z_mm ?? e.clear_h_mm;
        double roofTop = e.datums.FirstOrDefault(d => d.surface_id == "RoofTop")?.z_mm ?? (e.clear_h_mm + e.roof_mm);
        double wallTop = e.datums.FirstOrDefault(d => d.surface_id == "WallTop")?.z_mm ?? (roofBottom - e.bedding_mortar_mm);

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

            var host = g.components.FirstOrDefault(c => c.id == s.host_id || c.local_key == s.host_id || c.id.EndsWith($":{s.host_id}", StringComparison.Ordinal));
            if (host is null)
            {
                featuresList.Add(new FeatureStateV2(s.id, "BindingMissing", $"Host {s.host_id} missing", [], []));
                blockedFeatures.Add((s, $"Host {s.host_id} missing"));
                continue;
            }

            if (s.family == "Angle")
            {
                double frameX = e.frame_outer_x_mm > 0 ? e.frame_outer_x_mm : (e.neck_clear_x_mm > 0 ? e.neck_clear_x_mm : 700.0);
                double frameY = e.frame_outer_y_mm > 0 ? e.frame_outer_y_mm : (e.neck_clear_y_mm > 0 ? e.neck_clear_y_mm : 700.0);
                double zAngle = host.z_max_mm;
                double halfW = frameX / 2.0, halfD = frameY / 2.0;
                var curves = new[]
                {
                    new CurveV2("Angle/0", "Line", new Point3V2(-halfW, -halfD, zAngle), new Point3V2(halfW, -halfD, zAngle), null, null, null, null, "Outer"),
                    new CurveV2("Angle/1", "Line", new Point3V2(halfW, -halfD, zAngle), new Point3V2(halfW, halfD, zAngle), null, null, null, null, "Outer"),
                    new CurveV2("Angle/2", "Line", new Point3V2(halfW, halfD, zAngle), new Point3V2(-halfW, halfD, zAngle), null, null, null, null, "Outer"),
                    new CurveV2("Angle/3", "Line", new Point3V2(-halfW, halfD, zAngle), new Point3V2(-halfW, -halfD, zAngle), null, null, null, null, "Outer"),
                };
                double perimeterMm = 2.0 * (frameX + frameY);
                double unitMass = 3.77; // L50x50x5 standard unit mass kg/m
                double totalMass = perimeterMm / 1000.0 * unitMass;
                var angleOcc = new AngleOccurrenceV2(
                    $"{s.id}/Angle/0", host.id, null, s.shape_recipe_id, unitMass, curves,
                    perimeterMm, totalMass, s.shape_recipe_id, s.source_ids, s.id, []);
                anglesList.Add(angleOcc);
                featuresList.Add(new FeatureStateV2(s.id, "Ready", "physical angle frames", [angleOcc.id], []));
                continue;
            }

            double cover = s.cover_mm > 0 ? s.cover_mm : 50.0;
            int dia = s.diameter_mm ?? 10;
            double pitch = s.requested_pitch_mm ?? 150.0;
            var madeIds = new List<string>();

            if (s.component_kind == "Base" || host.kind == "Base")
            {
                double zBar = s.face == "Bottom"
                    ? baseBottom + cover + (s.layer == "Layer2" ? dia : 0) + dia / 2.0
                    : 0.0 - cover - (s.layer == "Layer2" ? dia : 0) - dia / 2.0;
                double x0 = -baseX + cover, x1 = baseX - cover;
                double y0 = -baseY + cover, y1 = baseY - cover;

                if (s.direction == "U")
                {
                    double runMm = x1 - x0;
                    double spanY = y1 - y0;
                    int count = s.count ?? Math.Max(2, (int)Math.Floor(spanY / pitch) + 1);
                    for (int i = 0; i < count; i++)
                    {
                        double stY = count <= 1 ? (y0 + y1) / 2 : y0 + i * (spanY / (count - 1));
                        string parentId = $"{e.id}:{s.host_id}:{s.face}:{s.layer}:{s.direction}:{s.family}:{i}";
                        string barId = $"{parentId}/0";
                        var curves = new[] { new CurveV2("Curve0", "Line", new Point3V2(x0, stY, zBar), new Point3V2(x1, stY, zBar), null, null, null, null, "Straight") };
                        var occ = new OccurrenceV2(
                            barId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                            i, stY, dia, pitch, pitch, [], "Uncut", curves,
                            [new RoleValueV2("Straight", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
                            s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                        occurrences.Add(occ);
                        madeIds.Add(barId);
                    }
                }
                else // V
                {
                    double runMm = y1 - y0;
                    double spanX = x1 - x0;
                    int count = s.count ?? Math.Max(2, (int)Math.Floor(spanX / pitch) + 1);
                    for (int i = 0; i < count; i++)
                    {
                        double stX = count <= 1 ? (x0 + x1) / 2 : x0 + i * (spanX / (count - 1));
                        string parentId = $"{e.id}:{s.host_id}:{s.face}:{s.layer}:{s.direction}:{s.family}:{i}";
                        string barId = $"{parentId}/0";
                        var curves = new[] { new CurveV2("Curve0", "Line", new Point3V2(stX, y0, zBar), new Point3V2(stX, y1, zBar), null, null, null, null, "Straight") };
                        var occ = new OccurrenceV2(
                            barId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                            i, stX, dia, pitch, pitch, [], "Uncut", curves,
                            [new RoleValueV2("Straight", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
                            s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                        occurrences.Add(occ);
                        madeIds.Add(barId);
                    }
                }
            }
            else if (s.component_kind == "Wall" || host.kind == "Wall")
            {
                bool isT1 = s.host_id.EndsWith("T1", StringComparison.Ordinal);
                bool isT2 = s.host_id.EndsWith("T2", StringComparison.Ordinal);
                bool isT3 = s.host_id.EndsWith("T3", StringComparison.Ordinal);

                double normalCoord = isT1 ? (s.face == "Inner" ? halfY + cover + dia / 2.0 : outerY - cover - dia / 2.0)
                    : isT3 ? (s.face == "Inner" ? -halfY - cover - dia / 2.0 : -outerY + cover + dia / 2.0)
                    : isT2 ? (s.face == "Inner" ? halfX + cover + dia / 2.0 : outerX - cover - dia / 2.0)
                    : (s.face == "Inner" ? -halfX - cover - dia / 2.0 : -outerX + cover + dia / 2.0);

                double u0 = (isT1 || isT3) ? -halfX + cover : -halfY + cover;
                double u1 = (isT1 || isT3) ? halfX - cover : halfY - cover;
                double z0 = 0.0 + cover;
                double z1 = wallTop - cover;

                var wallOpenings = g.openings.Where(op => op.host_ids.Contains(host.id)).ToArray();

                if (s.direction == "Vertical")
                {
                    double runMm = z1 - z0;
                    double spanU = u1 - u0;
                    int count = s.count ?? Math.Max(2, (int)Math.Floor(spanU / pitch) + 1);
                    for (int i = 0; i < count; i++)
                    {
                        double stU = count <= 1 ? (u0 + u1) / 2 : u0 + i * (spanU / (count - 1));
                        string parentId = $"{e.id}:{s.host_id}:{s.face}:{s.layer}:{s.direction}:{s.family}:{i}";
                        string barId = $"{parentId}/0";

                        Point3V2 pStart = (isT1 || isT3) ? new Point3V2(stU, normalCoord, z0) : new Point3V2(normalCoord, stU, z0);
                        Point3V2 pEnd = (isT1 || isT3) ? new Point3V2(stU, normalCoord, z1) : new Point3V2(normalCoord, stU, z1);

                        var hitOpening = wallOpenings.FirstOrDefault(op =>
                        {
                            double opU0 = op.concrete_cutout.SelectMany(l => l.curves).Min(c => Math.Min(c.start.x_mm, c.end.x_mm));
                            double opU1 = op.concrete_cutout.SelectMany(l => l.curves).Max(c => Math.Max(c.start.x_mm, c.end.x_mm));
                            return stU >= opU0 && stU <= opU1;
                        });

                        if (hitOpening != null)
                        {
                            double opZ0 = hitOpening.concrete_cutout.SelectMany(l => l.curves).Min(c => Math.Min(c.start.y_mm, c.end.y_mm));
                            double opZ1 = hitOpening.concrete_cutout.SelectMany(l => l.curves).Max(c => Math.Max(c.start.y_mm, c.end.y_mm));
                            double cutZ0 = Math.Max(z0, opZ0 - cover);
                            double cutZ1 = Math.Min(z1, opZ1 + cover);
                            double removed = Math.Max(0, cutZ1 - cutZ0);
                            double ret1 = Math.Max(0, cutZ0 - z0);
                            double ret2 = Math.Max(0, z1 - cutZ1);
                            var retainedList = new List<double>();
                            var retainedIds = new List<string>();

                            if (ret1 > 50)
                            {
                                string segId = $"{parentId}/0";
                                Point3V2 segEnd = (isT1 || isT3) ? new Point3V2(stU, normalCoord, cutZ0) : new Point3V2(normalCoord, stU, cutZ0);
                                var occ = new OccurrenceV2(
                                    segId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                                    i, stU, dia, pitch, pitch, [hitOpening.id], "RetainedBottom", [new CurveV2("Curve0", "Line", pStart, segEnd, null, null, null, null, "Straight")],
                                    [new RoleValueV2("Straight", ret1, s.source_ids)], ret1, ret1, ret1, "Centerline", "Known",
                                    s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}:0", "Centerline");
                                occurrences.Add(occ);
                                madeIds.Add(segId);
                                retainedList.Add(ret1);
                                retainedIds.Add(segId);
                            }
                            if (ret2 > 50)
                            {
                                string segId = $"{parentId}/1";
                                Point3V2 segStart = (isT1 || isT3) ? new Point3V2(stU, normalCoord, cutZ1) : new Point3V2(normalCoord, stU, cutZ1);
                                var occ = new OccurrenceV2(
                                    segId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                                    i, stU, dia, pitch, pitch, [hitOpening.id], "RetainedTop", [new CurveV2("Curve0", "Line", segStart, pEnd, null, null, null, null, "Straight")],
                                    [new RoleValueV2("Straight", ret2, s.source_ids)], ret2, ret2, ret2, "Centerline", "Known",
                                    s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}:1", "Centerline");
                                occurrences.Add(occ);
                                madeIds.Add(segId);
                                retainedList.Add(ret2);
                                retainedIds.Add(segId);
                            }
                            double consErr = Math.Abs(runMm - (removed + retainedList.Sum()));
                            clipLedger.Add(new ClipLedgerV2(parentId, [hitOpening.id], runMm, [removed], retainedList.ToArray(), retainedIds.ToArray(), consErr));
                        }
                        else
                        {
                            var curves = new[] { new CurveV2("Curve0", "Line", pStart, pEnd, null, null, null, null, "Straight") };
                            var occ = new OccurrenceV2(
                                barId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                                i, stU, dia, pitch, pitch, [], "Uncut", curves,
                                [new RoleValueV2("Straight", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
                                s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                            occurrences.Add(occ);
                            madeIds.Add(barId);
                        }
                    }
                }
                else // Horizontal
                {
                    double runMm = u1 - u0;
                    double spanZ = z1 - z0;
                    int count = s.count ?? Math.Max(2, (int)Math.Floor(spanZ / pitch) + 1);
                    for (int i = 0; i < count; i++)
                    {
                        double stZ = count <= 1 ? (z0 + z1) / 2 : z0 + i * (spanZ / (count - 1));
                        string parentId = $"{e.id}:{s.host_id}:{s.face}:{s.layer}:{s.direction}:{s.family}:{i}";
                        string barId = $"{parentId}/0";

                        Point3V2 pStart = (isT1 || isT3) ? new Point3V2(u0, normalCoord, stZ) : new Point3V2(normalCoord, u0, stZ);
                        Point3V2 pEnd = (isT1 || isT3) ? new Point3V2(u1, normalCoord, stZ) : new Point3V2(normalCoord, u1, stZ);

                        var hitOpening = wallOpenings.FirstOrDefault(op =>
                        {
                            double opZ0 = op.concrete_cutout.SelectMany(l => l.curves).Min(c => Math.Min(c.start.y_mm, c.end.y_mm));
                            double opZ1 = op.concrete_cutout.SelectMany(l => l.curves).Max(c => Math.Max(c.start.y_mm, c.end.y_mm));
                            return stZ >= opZ0 && stZ <= opZ1;
                        });

                        if (hitOpening != null)
                        {
                            double opU0 = hitOpening.concrete_cutout.SelectMany(l => l.curves).Min(c => Math.Min(c.start.x_mm, c.end.x_mm));
                            double opU1 = hitOpening.concrete_cutout.SelectMany(l => l.curves).Max(c => Math.Max(c.start.x_mm, c.end.x_mm));
                            double cutU0 = Math.Max(u0, opU0 - cover);
                            double cutU1 = Math.Min(u1, opU1 + cover);
                            double removed = Math.Max(0, cutU1 - cutU0);
                            double ret1 = Math.Max(0, cutU0 - u0);
                            double ret2 = Math.Max(0, u1 - cutU1);
                            var retainedList = new List<double>();
                            var retainedIds = new List<string>();

                            if (ret1 > 50)
                            {
                                string segId = $"{parentId}/0";
                                Point3V2 segEnd = (isT1 || isT3) ? new Point3V2(cutU0, normalCoord, stZ) : new Point3V2(normalCoord, cutU0, stZ);
                                var occ = new OccurrenceV2(
                                    segId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                                    i, stZ, dia, pitch, pitch, [hitOpening.id], "RetainedLeft", [new CurveV2("Curve0", "Line", pStart, segEnd, null, null, null, null, "Straight")],
                                    [new RoleValueV2("Straight", ret1, s.source_ids)], ret1, ret1, ret1, "Centerline", "Known",
                                    s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}:0", "Centerline");
                                occurrences.Add(occ);
                                madeIds.Add(segId);
                                retainedList.Add(ret1);
                                retainedIds.Add(segId);
                            }
                            if (ret2 > 50)
                            {
                                string segId = $"{parentId}/1";
                                Point3V2 segStart = (isT1 || isT3) ? new Point3V2(cutU1, normalCoord, stZ) : new Point3V2(normalCoord, cutU1, stZ);
                                var occ = new OccurrenceV2(
                                    segId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                                    i, stZ, dia, pitch, pitch, [hitOpening.id], "RetainedRight", [new CurveV2("Curve0", "Line", segStart, pEnd, null, null, null, null, "Straight")],
                                    [new RoleValueV2("Straight", ret2, s.source_ids)], ret2, ret2, ret2, "Centerline", "Known",
                                    s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}:1", "Centerline");
                                occurrences.Add(occ);
                                madeIds.Add(segId);
                                retainedList.Add(ret2);
                                retainedIds.Add(segId);
                            }
                            double consErr = Math.Abs(runMm - (removed + retainedList.Sum()));
                            clipLedger.Add(new ClipLedgerV2(parentId, [hitOpening.id], runMm, [removed], retainedList.ToArray(), retainedIds.ToArray(), consErr));
                        }
                        else
                        {
                            var curves = new[] { new CurveV2("Curve0", "Line", pStart, pEnd, null, null, null, null, "Straight") };
                            var occ = new OccurrenceV2(
                                barId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                                i, stZ, dia, pitch, pitch, [], "Uncut", curves,
                                [new RoleValueV2("Straight", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
                                s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                            occurrences.Add(occ);
                            madeIds.Add(barId);
                        }
                    }
                }
            }
            else // Roof, Neck, etc.
            {
                double zBar = s.component_kind == "Roof"
                    ? (s.face == "Bottom" ? roofBottom + cover + dia / 2.0 : roofTop - cover - dia / 2.0)
                    : (roofTop + (e.neck_height_mm > 0 ? e.neck_height_mm / 2.0 : 0.0));
                double x0 = -halfX + cover, x1 = halfX - cover;
                double y0 = -halfY + cover, y1 = halfY - cover;
                double runMm = s.direction == "U" ? x1 - x0 : y1 - y0;
                double spanCross = s.direction == "U" ? y1 - y0 : x1 - x0;
                int count = s.count ?? Math.Max(2, (int)Math.Floor(spanCross / pitch) + 1);
                for (int i = 0; i < count; i++)
                {
                    double st = count <= 1 ? spanCross / 2 : i * (spanCross / (count - 1));
                    string parentId = $"{e.id}:{s.host_id}:{s.face}:{s.layer}:{s.direction}:{s.family}:{i}";
                    string barId = $"{parentId}/0";
                    Point3V2 pStart = s.direction == "U" ? new Point3V2(x0, y0 + st, zBar) : new Point3V2(x0 + st, y0, zBar);
                    Point3V2 pEnd = s.direction == "U" ? new Point3V2(x1, y0 + st, zBar) : new Point3V2(x0 + st, y1, zBar);
                    var curves = new[] { new CurveV2("Curve0", "Line", pStart, pEnd, null, null, null, null, "Straight") };
                    var occ = new OccurrenceV2(
                        barId, parentId, host.id, host.assembly_id, null, s.face, s.layer, s.direction, s.family,
                        i, st, dia, pitch, pitch, [], "Uncut", curves,
                        [new RoleValueV2("Straight", runMm, s.source_ids)], runMm, runMm, runMm, "Centerline", "Known",
                        s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                    occurrences.Add(occ);
                    madeIds.Add(barId);
                }
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
            if (joint is null)
            {
                featuresList.Add(new FeatureStateV2(s.id, "BindingMissing", "Joint with dowel_required=true missing in geometry", [], []));
                blockedFeatures.Add((s, "Joint with dowel_required=true missing in geometry"));
                continue;
            }

            double inputDowelLength = 500.0;
            var dlCell = input.raw.rows.SelectMany(r => r.cells).Concat(input.raw.config)
                .FirstOrDefault(c => string.Equals(c.field_key, "DowelLength", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(c.field_key, "Config.DowelLength", StringComparison.OrdinalIgnoreCase));
            if (dlCell != null && double.TryParse(dlCell.text ?? dlCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDl) && parsedDl > 0)
            {
                inputDowelLength = parsedDl;
            }

            int dowelDia = s.diameter_mm ?? 18;
            var ddCell = input.raw.rows.SelectMany(r => r.cells).Concat(input.raw.config)
                .FirstOrDefault(c => string.Equals(c.field_key, "DowelDiameter", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(c.field_key, "Config.DowelDiameter", StringComparison.OrdinalIgnoreCase));
            if (ddCell != null && int.TryParse(ddCell.text ?? ddCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDd) && parsedDd > 0)
            {
                dowelDia = parsedDd;
            }

            double requestedPitch = s.requested_pitch_mm ?? 200.0;
            var dpCell = input.raw.rows.SelectMany(r => r.cells).Concat(input.raw.config)
                .FirstOrDefault(c => string.Equals(c.field_key, "DowelStep", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(c.field_key, "DowelPitch", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(c.field_key, "Config.DowelStep", StringComparison.OrdinalIgnoreCase));
            if (dpCell != null && double.TryParse(dpCell.text ?? dpCell.raw_number_text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDp) && parsedDp > 0)
            {
                requestedPitch = parsedDp;
            }
            double pitchEffective = 50.0 * Math.Round(requestedPitch / 50.0, MidpointRounding.AwayFromZero);
            if (pitchEffective <= 0) pitchEffective = 50.0;

            double wallTopElev = joint.z_mm;
            double roofBottomElev = wallTopElev + e.bedding_mortar_mm;
            double gap = Math.Max(0, roofBottomElev - wallTopElev);
            if (inputDowelLength <= gap)
            {
                featuresList.Add(new FeatureStateV2(s.id, "BindingMissing", $"Dowel length {inputDowelLength} <= joint gap {gap}", [], []));
                blockedFeatures.Add((s, $"Dowel length {inputDowelLength} <= joint gap {gap}"));
                continue;
            }

            double embedTotal = inputDowelLength - gap;
            double lowerEmbed = embedTotal / 2.0;
            double upperEmbed = embedTotal / 2.0;
            double z0 = wallTopElev - lowerEmbed;
            double z1 = roofBottomElev + upperEmbed; // z1 - z0 = inputDowelLength exactly

            double holeDia = dowelDia + 7.0; // holeDia > dowelDia
            double holeDepth = upperEmbed + 10.0; // holeDepth >= upperEmbed
            double holeZmin = roofBottomElev;
            double holeZmax = roofBottomElev + holeDepth;
            var holeAxis = new Vector3V2(0, 0, 1);

            double midX = (halfX + outerX) / 2.0;
            double midY = (halfY + outerY) / 2.0;
            int totalCount = s.count ?? 4;
            var stationPoints = new List<(double X, double Y, int idx)>();
            if (totalCount == 4)
            {
                stationPoints.Add((0, midY, 0));
                stationPoints.Add((midX, 0, 1));
                stationPoints.Add((0, -midY, 2));
                stationPoints.Add((-midX, 0, 3));
            }
            else
            {
                int countPerWall = Math.Max(1, totalCount / 4);
                int idx = 0;
                for (int c = 0; c < countPerWall; c++) stationPoints.Add((-halfX + (c + 0.5) * e.clear_x_mm / countPerWall, midY, idx++));
                for (int c = 0; c < countPerWall; c++) stationPoints.Add((midX, -halfY + (c + 0.5) * e.clear_y_mm / countPerWall, idx++));
                for (int c = 0; c < countPerWall; c++) stationPoints.Add((-halfX + (c + 0.5) * e.clear_x_mm / countPerWall, -midY, idx++));
                for (int c = 0; c < countPerWall; c++) stationPoints.Add((-midX, -halfY + (c + 0.5) * e.clear_y_mm / countPerWall, idx++));
            }

            var madeIds = new List<string>();
            for (int i = 0; i < stationPoints.Count; i++)
            {
                var (sx, sy, stIdx) = stationPoints[i];
                string parentId = $"{e.id}:{s.host_id}:None:Bottom:Vertical:Dowel:{i}";
                string barId = $"{parentId}/0";

                var barStart = new Point3V2(sx, sy, z0);
                var barEnd = new Point3V2(sx, sy, z1);
                var holeCenter = new Point3V2(sx, sy, roofBottomElev);

                var curves = new[] { new CurveV2("Curve0", "Line", barStart, barEnd, null, null, null, null, "Straight") };
                var occ = new OccurrenceV2(
                    barId, parentId, s.host_id, joint.upper_assembly_id, null, "None", "Bottom", "Vertical", "Dowel",
                    i, i * pitchEffective, dowelDia, pitchEffective, pitchEffective, [], "Uncut", curves,
                    [new RoleValueV2("Length", inputDowelLength, s.source_ids)], inputDowelLength, inputDowelLength, inputDowelLength, "Centerline", "Known",
                    s.shape_recipe_id, s.source_ids, s.id, $"{s.id}:{i}", "Centerline");
                occurrences.Add(occ);
                madeIds.Add(barId);

                string holeId = $"{s.id}:Hole:{i}";
                var pair = new DowelPairV2(
                    $"{joint.id}:{i}", joint.id, barId, holeId,
                    holeCenter, holeDia, holeDepth, holeAxis, holeZmin, holeZmax, s.source_ids);
                dowelPairs.Add(pair);
            }
            featuresList.Add(new FeatureStateV2(s.id, "Ready", "one physical bar/hole per approved station", madeIds.ToArray(), []));
        }

        if (blockedFeatures.Count > 0)
        {
            if (input.request.generation_mode != "Preview")
            {
                var (firstBlocked, reason) = blockedFeatures[0];
                throw new StageFault(
                    "REBAR_REQUIRED_FEATURE_BLOCKED",
                    firstBlocked.id,
                    [firstBlocked.host_id],
                    "existing host component and joint in geometry",
                    reason,
                    firstBlocked.source_ids);
            }
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
        {
            hashSb.Append($"{occ.id}|{occ.parent_id}|{occ.host_id}|{occ.face}|{occ.layer}|{occ.direction}|{occ.family}|{occ.station_index}|{occ.station_mm.ToString("F3", CultureInfo.InvariantCulture)}|{occ.diameter_mm}|{occ.straight_run_mm.ToString("F3", CultureInfo.InvariantCulture)}|{occ.schedule_length_mm?.ToString("F3", CultureInfo.InvariantCulture) ?? "null"}|{occ.measurement_basis};");
            foreach (var c in occ.path)
            {
                hashSb.Append($"C:{c.id}:{c.kind}:{c.role}:({c.start.x_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.start.y_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.start.z_mm.ToString("F3", CultureInfo.InvariantCulture)})->({c.end.x_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.end.y_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.end.z_mm.ToString("F3", CultureInfo.InvariantCulture)})");
                if (c.center != null) hashSb.Append($":C({c.center.x_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.center.y_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.center.z_mm.ToString("F3", CultureInfo.InvariantCulture)})");
                if (c.radius_mm.HasValue) hashSb.Append($":R={c.radius_mm.Value.ToString("F3", CultureInfo.InvariantCulture)}");
                hashSb.Append(';');
            }
            foreach (var rv in occ.role_values)
                hashSb.Append($"R:{rv.role}={rv.value_mm.ToString("F3", CultureInfo.InvariantCulture)};");
        }
        foreach (var a in anglesList.OrderBy(a => a.id, StringComparer.Ordinal))
        {
            hashSb.Append($"{a.id}|{a.host_id}|{a.shape_recipe_id}|{a.unit_mass_kg_per_m.ToString("F3", CultureInfo.InvariantCulture)}|{a.total_length_mm.ToString("F3", CultureInfo.InvariantCulture)}|{a.mass_kg.ToString("F3", CultureInfo.InvariantCulture)};");
            foreach (var c in a.path)
                hashSb.Append($"AC:({c.start.x_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.start.y_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.start.z_mm.ToString("F3", CultureInfo.InvariantCulture)})->({c.end.x_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.end.y_mm.ToString("F3", CultureInfo.InvariantCulture)},{c.end.z_mm.ToString("F3", CultureInfo.InvariantCulture)});");
        }
        string physicalHashRes = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashSb.ToString())));

        var geomHashSb = new StringBuilder();
        geomHashSb.Append(g.physical_hash);
        foreach (var p in dowelPairs.OrderBy(p => p.id, StringComparer.Ordinal))
        {
            geomHashSb.Append($"P:{p.id}|{p.joint_id}|{p.dowel_occurrence_id}|{p.hole_id}|Center=({p.center.x_mm.ToString("F3", CultureInfo.InvariantCulture)},{p.center.y_mm.ToString("F3", CultureInfo.InvariantCulture)},{p.center.z_mm.ToString("F3", CultureInfo.InvariantCulture)})|Axis=({p.hole_axis.x.ToString("F3", CultureInfo.InvariantCulture)},{p.hole_axis.y.ToString("F3", CultureInfo.InvariantCulture)},{p.hole_axis.z.ToString("F3", CultureInfo.InvariantCulture)})|Dia={p.hole_diameter_mm.ToString("F3", CultureInfo.InvariantCulture)}|Depth={p.hole_depth_mm.ToString("F3", CultureInfo.InvariantCulture)}|Z={p.hole_z_min_mm.ToString("F3", CultureInfo.InvariantCulture)}..{p.hole_z_max_mm.ToString("F3", CultureInfo.InvariantCulture)};");
        }
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

        // D09: Ensure geometry components provide exact component/assembly ownership for scoped IDs (e.g. MH1:Base)
        var geometry = AlignGeometryComponents(input.geometry, input.groups, input.effective.id);

        var planner = new NativeSteelSchedulePlanner();
        var schedules = planner.PlanRowsAndSummaries(
            input.effective,
            geometry,
            input.groups,
            input.assets,
            input.rules);

        // N10 & D03/D04: Preflight schedules to ensure all rows, blocks, tags, marks, and raw numeric values are valid
        PreflightSchedules(schedules, input.assets);

        return new S8StateV2(
            input.context,
            input.request,
            input.assets,
            input.rules,
            input.raw,
            input.effective,
            geometry,
            input.rebar,
            input.groups,
            input.projection,
            input.annotations,
            schedules);
    }

    private static GeometryV2 AlignGeometryComponents(GeometryV2 geometry, GroupsV2 groups, string manholeId)
    {
        if (geometry?.components == null || geometry.components.Length == 0 || groups?.items == null)
            return geometry!;

        var existingMap = new Dictionary<string, ComponentV2>(StringComparer.Ordinal);
        foreach (var c in geometry.components)
        {
            existingMap[c.id] = c;
        }

        var extraComponents = new List<ComponentV2>();

        foreach (var g in groups.items)
        {
            if (string.IsNullOrEmpty(g.host_id)) continue;
            if (existingMap.ContainsKey(g.host_id)) continue;

            string unadorned = g.host_id.Contains(':')
                ? g.host_id.Substring(g.host_id.LastIndexOf(':') + 1)
                : g.host_id;

            var match = existingMap.Values.FirstOrDefault(c =>
                string.Equals(c.id, unadorned, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.kind, unadorned, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                var alias = match with { id = g.host_id };
                existingMap[g.host_id] = alias;
                extraComponents.Add(alias);
            }
        }

        if (extraComponents.Count == 0) return geometry;

        return new GeometryV2(
            components: geometry.components.Concat(extraComponents).ToArray(),
            openings: geometry.openings,
            joints: geometry.joints,
            physical_hash: geometry.physical_hash,
            products: geometry.products);
    }

    private static void PreflightSchedules(SchedulesV2 schedules, AssetsV2 assets)
    {
        ArgumentNullException.ThrowIfNull(schedules);
        ArgumentNullException.ThrowIfNull(assets);

        var assetMap = (assets.definitions ?? [])
            .Where(a => !string.IsNullOrEmpty(a.name))
            .ToDictionary(a => a.name, StringComparer.OrdinalIgnoreCase);

        foreach (var row in schedules.rows)
        {
            if (string.IsNullOrWhiteSpace(row.native_asset))
            {
                throw new StageFault(
                    "SCHEDULE_ROW_ASSET_MISSING",
                    row.id,
                    ["native_asset"],
                    "non-empty native asset name",
                    "empty");
            }

            if (!assetMap.TryGetValue(row.native_asset, out var assetDef))
            {
                throw new StageFault(
                    "ASSET_EXACT_NAME_MISSING",
                    row.native_asset,
                    ["assets.definitions"],
                    "exact native block asset in definitions",
                    "not found");
            }

            if (string.IsNullOrWhiteSpace(row.group_id))
            {
                throw new StageFault(
                    "SCHEDULE_ROW_GROUP_MISSING",
                    row.id,
                    ["group_id"],
                    "valid group_id",
                    "empty");
            }

            if (row.attributes == null || row.attributes.Length == 0)
            {
                throw new StageFault(
                    "SCHEDULE_ROW_ATTRIBUTES_EMPTY",
                    row.id,
                    ["attributes"],
                    "non-empty bound attributes",
                    "0");
            }

            foreach (var attr in row.attributes)
            {
                if (string.IsNullOrWhiteSpace(attr.tag))
                {
                    throw new StageFault(
                        "SCHEDULE_ROW_TAG_MISSING",
                        row.id,
                        ["tag"],
                        "non-empty tag",
                        "empty");
                }

                if (string.IsNullOrWhiteSpace(attr.text))
                {
                    throw new StageFault(
                        "SCHEDULE_ROW_TEXT_MISSING",
                        row.id,
                        [attr.tag],
                        "non-empty formatted text",
                        "empty");
                }

                if (attr.value_kind is "Diameter" or "Length" or "Metres" or "Count")
                {
                    if (!attr.raw_number.HasValue || double.IsNaN(attr.raw_number.Value) || attr.raw_number.Value <= 0)
                    {
                        throw new StageFault(
                            "SCHEDULE_ROW_NUMERIC_INVALID",
                            row.id,
                            [attr.tag],
                            "> 0",
                            attr.raw_number?.ToString(CultureInfo.InvariantCulture) ?? "null");
                    }
                }
                else if (attr.value_kind is "Mass")
                {
                    if (!attr.raw_number.HasValue || double.IsNaN(attr.raw_number.Value) || attr.raw_number.Value < 0)
                    {
                        throw new StageFault(
                            "SCHEDULE_ROW_MASS_INVALID",
                            row.id,
                            [attr.tag],
                            ">= 0",
                            attr.raw_number?.ToString(CultureInfo.InvariantCulture) ?? "null");
                    }
                }

                if (attr.source_ids == null || attr.source_ids.Length == 0)
                {
                    throw new StageFault(
                        "SCHEDULE_ROW_SOURCE_MISSING",
                        row.id,
                        [attr.tag],
                        "non-empty source_ids",
                        "empty");
                }
            }
        }

        foreach (var summary in schedules.summaries)
        {
            if (string.IsNullOrWhiteSpace(summary.native_asset) || !assetMap.ContainsKey(summary.native_asset))
            {
                throw new StageFault(
                    "ASSET_EXACT_NAME_MISSING",
                    summary.native_asset ?? "null",
                    ["assets.definitions"],
                    "exact native summary asset in definitions",
                    "not found");
            }
        }

        foreach (var caption in schedules.captions)
        {
            if (string.IsNullOrWhiteSpace(caption.glyph_asset))
            {
                throw new StageFault(
                    "CAPTION_GLYPH_MISSING",
                    caption.group_id,
                    ["glyph_asset"],
                    "non-empty glyph_asset",
                    "empty");
            }
            if (string.IsNullOrWhiteSpace(caption.text))
            {
                throw new StageFault(
                    "CAPTION_TEXT_MISSING",
                    caption.group_id,
                    ["text"],
                    "non-empty caption text",
                    "empty");
            }
        }
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
        var groupRegistry = inputs.GroupRegistry ?? PresentationGroupMap.PlanPreDrawing(
            inputs.RebarLayout.Occurrences,
            inputs.RebarLayout.Angles,
            inputs.SteelSchedule,
            inputs.AngleSchedule);
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
        var groupMap = PresentationGroupMap.FromPlannedGroups(groups);

        // Carrier coverage boundary verification
        var cov = groupMap.ValidateCoverageV2(input.rebar);
        if (cov.OrphanOccurrences > 0 || cov.PhantomOccurrences > 0)
        {
            throw new StageFault(
                "PRESENTATION_GROUP_COVERAGE_INVALID",
                input.effective.id,
                ["member_ids"],
                "zero orphans and zero phantoms",
                $"orphans={cov.OrphanOccurrences}, phantoms={cov.PhantomOccurrences}");
        }

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

        if (_nativePort is null)
            throw new StageFault("NATIVE_GENERATION_PORT_MISSING", input.context.manhole_id,
                ["composition.native_generation_port"], "configured real AutoCAD native generation port", "NotRun",
                input.context.sources.Select(source => source.id).ToArray());
        var native = _nativePort.Write(input, CancellationToken.None);

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

        // Missing stages are configuration faults. Never fabricate assets,
        // tolerance rules, workbook hashes, rows or a native acceptance result.
        if (_s0 is null)
            throw new StageFault("PIPELINE_STAGE_NOT_CONFIGURED", input.context.manhole_id,
                ["composition.s0"], "configured S0 assets/rules reader", "NotRun",
                input.context.sources.Select(source => source.id).ToArray());
        var s0 = _s0.Execute(input);
        token.ThrowIfCancellationRequested();
        if (_s1 is null)
            throw new StageFault("PIPELINE_STAGE_NOT_CONFIGURED", input.context.manhole_id,
                ["composition.s1"], "configured S1 workbook reader", "NotRun",
                input.context.sources.Select(source => source.id).ToArray());
        var s1 = _s1.Execute(s0);
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

