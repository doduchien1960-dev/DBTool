using System.Globalization;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using DBTool.Domain;
using DBTool.Domain.Geometry;


namespace DBTool.Application;

/// <summary>Normalizes raw cells without CAD/Excel dependencies. Feature feasibility belongs to later stages.</summary>
public sealed class ManholeValidator(IRawValueParser numberParser, NumericPolicy numericPolicy) : IManholeValidator
{
    public OperationResult<ValidatedProjectInput> Validate(RawProjectInput raw,CatalogSet catalogs,CalculationRequest request,CalculationPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(raw); ArgumentNullException.ThrowIfNull(catalogs);
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(policy);
        var diagnostics=raw.Diagnostics.ToList();
        string? activeManhole = null;
        var cellReadings = new Dictionary<RawCell, OperationResult<string>>();
        string Text(RawCell? cell)
        {
            if (cell is null) return "";
            if (!cellReadings.TryGetValue(cell, out var result))
            {
                result = RawCellReader.ReadText(cell, numericPolicy);
                cellReadings.Add(cell, result);
                bool config = raw.ConfigCells.Values.Contains(cell) || activeManhole is null;
                diagnostics.AddRange(result.Diagnostics.Select(d => new Diagnostic(d.Code, d.Severity,
                    config ? DiagnosticScope.Global : DiagnosticScope.Manhole, config ? null : activeManhole,
                    null, d.Source, d.Expected, d.Actual, d.Message)));
            }
            return result.Success ? result.Value! : "";
        }
        bool Active(RawCell cell) => Text(cell) is not ("" or "0" or "Không" or "Khong" or "None") &&
            !Regex.IsMatch(Text(cell), "^0[Dd][0-9]+$");

        void Global(string code,string message,SourceRef? source=null)=>diagnostics.Add(new(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,source,null,null,message));
        var s2=policy.PolicyId is "S2Geometry-1" or "S2Rebar-1";
        var gate=s2
            ? policy.PolicyId == "S2Rebar-1" && request.Scope is (CalculationScope.FullRebar or CalculationScope.Full)
                ? request.CheckS2RebarInput()
                : request.Scope == CalculationScope.GeometryOnly ? request.CheckS2GeometryInput() : request.CheckS2QuantityInput()
            : request.CheckS1();
        if(!gate.Success) diagnostics.AddRange(gate.Diagnostics);
        if(ManholeIdentity.CanonicalText(raw.ProjectId)!=ManholeIdentity.CanonicalText(request.ProjectId)) Global("PROJECT_MISMATCH","Request belongs to a different project.");
        if(request.PolicyId!=policy.PolicyId || !request.AllowExperimental) Global("POLICY_MISMATCH","Policy/request mismatch.");
        if(catalogs.Pipes.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=catalogs.Pipes.Count) Global("CATALOG_DUPLICATE","Catalog codes collide ignoring case.");

        var rows=new Dictionary<string,RawManholeRow>(StringComparer.Ordinal);
        foreach(var row in raw.Rows)
        {
            row.Cells.TryGetValue("Name",out var nameCell);
            string name=Text(nameCell);
            if(string.IsNullOrWhiteSpace(name)) { Global("ROW_NAME_MISSING","Manhole name is required.",nameCell?.Source); continue; }
            string id=ManholeIdentity.Create(raw.ProjectId,raw.Profile,name);
            if(!rows.TryAdd(id,row)) Global("MANHOLE_ID_DUPLICATE","Duplicate canonical manhole identity.",nameCell?.Source);
        }
        foreach(string id in request.SelectedIds)
            if(!rows.ContainsKey(id)) Global("MANHOLE_NOT_FOUND",$"Selected ID not found: {id}.");
        if(!DrawingInputGate.GetBlockingDiagnostics(request.SelectedIds,diagnostics).IsEmpty)
            return new(false,null,DrawingInputGate.GetBlockingDiagnostics(request.SelectedIds,diagnostics));

        var specs=new List<ValidatedManholeSpec>();
        foreach(string id in request.SelectedIds)
        {
            activeManhole = id;
            var row=rows[id];
            var consumed=new HashSet<string>(StringComparer.Ordinal);
            var sources=new Dictionary<string,SourceRef>(StringComparer.Ordinal);
            var features=new List<FeatureRequest>();
            void Error(string code,string message,RawCell? cell,bool config)
                =>diagnostics.Add(new(code,DiagnosticSeverity.Block,config?DiagnosticScope.Global:DiagnosticScope.Manhole,config?null:id,null,cell?.Source,null,cell?.Source.RawText,message));
            RawCell? Get(string key,bool config=false)
            {
                var map=config?raw.ConfigCells:row.Cells;
                consumed.Add((config?"Config.":"Row.")+key);
                if(map.TryGetValue(key,out var cell)) { sources[(config?"Config.":"Row.")+key]=cell.Source; return cell; }
                Error("INPUT_FIELD_MISSING",$"Missing raw field {key}.",null,config); return null;
            }
            void AddParserDiagnostics(IEnumerable<Diagnostic> values,bool config)
            {
                foreach(var d in values) diagnostics.Add(new(d.Code,d.Severity,config?DiagnosticScope.Global:DiagnosticScope.Manhole,config?null:id,null,d.Source,d.Expected,d.Actual,d.Message));
            }
            // Zero below is only a local failure sentinel; any Block prevents constructing a snapshot.
            double Number(RawCell? cell,bool config,bool positive=false,string? missingCode=null)
            {
                if(cell==null) return 0;
                if(Blank(cell) && missingCode!=null) { Error(missingCode,"Required numeric input is blank.",cell,config); return 0; }
                var result=numberParser.ReadNumber(cell,numericPolicy); AddParserDiagnostics(result.Diagnostics,config);
                if(!result.Success) return 0;
                double value=result.Value!.Value;
                if(!double.IsFinite(value)) Error("NUMBER_NOT_FINITE","Finite number required.",cell,config);
                else if(positive && value<=0) Error("DIMENSION_NONPOSITIVE","Positive dimension required.",cell,config);
                return value;
            }
            double NonNegative(string key,bool config=false)
            {
                var cell=Get(key,config); double value=Number(cell,config);
                if(value<0) Error("DIMENSION_NEGATIVE","Nonnegative value required.",cell,config);
                return value;
            }
            MeshSpec? Mesh(string key)
            {
                var cell=Get(key); if(cell==null) return null;
                if(cell.Kind is not (RawCellKind.Text or RawCellKind.Blank or RawCellKind.Formula)) { Error("REBAR_FORMAT","Mesh must be text or a formula allowed by the input policy.",cell,false);return null; }
                var parsed=RebarSpecParser.Parse(Text(cell),ParseContext.MeshRequired,cell.Source);
                AddParserDiagnostics(parsed.Diagnostics,false); return parsed.Value?.Mesh;
            }
            HookPattern Hook(string key)
            {
                var cell=Get(key,true); string text=Text(cell).ToUpperInvariant();
                if(text is "0M" or "OM") return HookPattern.Hook0M;
                if(text=="1M") return HookPattern.Hook1M;
                if(text=="2M") return HookPattern.Hook2M;
                Error("HOOK_PATTERN_INVALID","Expected 0M/1M/2M.",cell,true); return HookPattern.Hook0M;
            }
            double l=Number(Get("Length"),false,true), b=Number(Get("Width"),false,true), h=Number(Get("Height"),false,true);
            double f=Number(Get("BaseThickness"),false,true), t=Number(Get("WallThickness"),false,true), r=Number(Get("RoofThickness"),false,true);
            double p=NonNegative("PadThickness",true), eb=NonNegative("BaseExpansion",true), ep=NonNegative("PadExpansion",true);
            double cover=NonNegative("CoverBottom",true), baseHaunch=NonNegative("BaseHaunch",true), neckHeight=NonNegative("NeckHeight");
            double topHaunch=raw.Profile==Profile.GaThamLienKhoi?NonNegative("TopHaunch",true):0;
            double bedding=raw.Profile==Profile.GaThamLienKhoi?0:NonNegative("BeddingMortar",true);
            var bottom=Mesh("BaseBottomMesh"); var top=Mesh("BaseTopMesh");
            var hookBottom=Hook("HookPattern.C"); var hookTop=Hook("HookPattern.D");
            var distributionCell=Get("Distribution",true);
            DistributionMode distribution=Text(distributionCell) switch { "Chia đều" or "Equalized"=>DistributionMode.Equalized,"Theo mật độ" or "DensityCentered"=>DistributionMode.DensityCentered,_=>(DistributionMode)(-1) };
            if((int)distribution<0) Error("DISTRIBUTION_REQUIRED","Unknown or blank distribution mode.",distributionCell,true);

            var elevCell=Get("Elevation.C",true);
            ElevationMode elevationMode=Text(elevCell) switch { "Tuyệt đối" or "Absolute"=>ElevationMode.Absolute,"Ký hiệu" or "Symbolic"=>ElevationMode.Symbolic,_=>(ElevationMode)(-1) };
            if((int)elevationMode<0) Error("ELEVATION_MODE_REQUIRED","Explicit elevation mode is required.",elevCell,true);
            var aa=Get("BaseElevationM"); ElevationM? datum=null;
            if(elevationMode==ElevationMode.Absolute || !Blank(aa))
            {
                double value=Number(aa,false,false,"ELEVATION_REQUIRED");
                if(double.IsFinite(value) && !Blank(aa)) datum=new ElevationM(value);
            }

            var sizeCell=Get("NeckSize",true); double roofL=0,roofB=0;
            string[] parts=Regex.Split(Text(sizeCell),"[xX×]");
            if(sizeCell==null || sizeCell.Kind is not (RawCellKind.Text or RawCellKind.Formula) || parts.Length!=2) Error("ROOF_OPENING_FORMAT","Expected two dimensions.",sizeCell,true);
            else
            {
                roofL=Number(new RawCell(RawCellKind.Text,parts[0],null,null,null,null,sizeCell.Source),true,true);
                roofB=Number(new RawCell(RawCellKind.Text,parts[1],null,null,null,null,sizeCell.Source),true,true);
                
            }
            if(raw.ConfigCells.TryGetValue("CoverBlock",out var blockCellRaw))
            {
                string coverCode=Text(blockCellRaw);
                if(catalogs.Covers.TryGetValue(coverCode,out var coverEntry) && coverEntry.BlockSet is not null)
                {
                    double? clearW=null, clearL=null;
                    if(coverEntry.OpeningContract is not null)
                    {
                        var cl=coverEntry.OpeningContract.ClearOpening;
                        clearW=cl.MaxX-cl.MinX; clearL=cl.MaxY-cl.MinY;
                    }
                    else if(coverEntry.BlockSet?.NominalOpening is not null)
                    {
                        clearW=coverEntry.BlockSet.NominalOpening.Size.Width;
                        clearL=coverEntry.BlockSet.NominalOpening.Size.Height;
                    }
                    else if(coverEntry.BlockSet?.MeasuredAperture is not null)
                    {
                        var ap=coverEntry.BlockSet.MeasuredAperture;
                        if(ap is CircleProfile cp) { clearW=2*cp.RadiusMm; clearL=2*cp.RadiusMm; }
                        else if(ap is RectangleProfile rp) { clearW=rp.Rectangle.MaxX-rp.Rectangle.MinX; clearL=rp.Rectangle.MaxY-rp.Rectangle.MinY; }
                    }
                    if(clearW.HasValue && clearL.HasValue)
                    {
                        double rot=coverEntry.RotationDegrees ?? 0;
                        bool swap=((int)Math.Round(rot/90.0)%2)!=0;
                        double effRoofL=swap?clearL.Value:clearW.Value;
                        double effRoofB=swap?clearW.Value:clearL.Value;
                        if(roofL>0 && roofB>0 && (Math.Abs(roofL-effRoofL)>1e-3 || Math.Abs(roofB-effRoofB)>1e-3))
                        {
                            diagnostics.Add(new Diagnostic("COVER_SELECTION_LOCKS_NECK",DiagnosticSeverity.Review,DiagnosticScope.Component,
                                id,"Neck",sizeCell?.Source ?? blockCellRaw.Source,$"{effRoofL}x{effRoofB}",Text(sizeCell),
                                $"Selected cover '{coverEntry.Code}' locks neck inner opening to {effRoofL}x{effRoofB} mm under rule OWNER-COVER-20261012-v2; raw workbook value preserved as provenance."));
                        }
                        roofL=effRoofL;
                        roofB=effRoofB;
                    }
                }
            }
            if(sizeCell!=null && parts.Length==2 && (roofL>l || roofB>b)) Error("ROOF_OPENING_OUTSIDE","Opening must fit inside the manhole; no clamping.",sizeCell,true);
            double roofCenterX=0,roofCenterY=0;
            var positionCell=Get("NeckPosition",true);
            NeckPosition neckPosition=NeckPosition.Center;
            if(s2)
            {
                if(!NeckPositionMap.TryParse(Text(positionCell),out neckPosition))
                    Error("ROOF_POSITION_UNSUPPORTED","Named position mapping is required.",positionCell,true);
                else if(roofL>0 && roofB>0 && roofL<=l && roofB<=b && l>0 && b>0)
                {
                    var (cx,cy)=NeckPositionMap.Center(neckPosition,l,b,roofL,roofB);
                    // Kept local for the final typed roof opening below.
                    roofCenterX=cx; roofCenterY=cy;
                }
            }
            else if(Text(positionCell) is not ("Giữa ga" or "Center")) Error("ROOF_POSITION_UNSUPPORTED","Only centered opening has a resolved rectangle in this contract; no assumed offset.",positionCell,true);

            HaunchSpec? baseHaunchSpec=null,topHaunchSpec=null;
            NeckSpec? neckSpec=null;
            if(s2)
            {
                baseHaunchSpec=new HaunchSpec(baseHaunch,baseHaunch,HaunchOwner.Base,[sources["Config.BaseHaunch"]],new HaunchRebarSpec());
                if(raw.Profile==Profile.GaThamLienKhoi)
                    topHaunchSpec=new HaunchSpec(topHaunch,topHaunch,HaunchOwner.Roof,[sources["Config.TopHaunch"]],new HaunchRebarSpec());
                if(neckHeight>0)
                {
                    var thicknessCell=Get("NeckThickness");
                    double neckThickness=Number(thicknessCell,false,true,"NECK_THICKNESS_REQUIRED");
                    if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                        neckSpec=new NeckSpec(roofL,roofB,neckThickness,neckHeight,neckPosition,[sizeCell!.Source,positionCell!.Source,thicknessCell!.Source,sources["Row.NeckHeight"]]);
                }
            }

            // Optional S2 fixture fields are consumed only when explicitly supplied. Real VHG seat
            // and collar cell meanings remain U-NECK; no source workbook mapping is invented here.
            NeckSeatSpec? seatSpec=null;
            if(s2 && raw.ConfigCells.TryGetValue("NeckSeatKind",out var seatKindCell))
            {
                consumed.Add("Config.NeckSeatKind"); sources["Config.NeckSeatKind"]=seatKindCell.Source;
                NeckSeatKind kind=Text(seatKindCell) switch
                {
                    "None"=>NeckSeatKind.None,"Step"=>NeckSeatKind.Step,"Slope"=>NeckSeatKind.Slope,_=>(NeckSeatKind)(-1)
                };
                if(!Enum.IsDefined(kind))
                    Error("NECK_SEAT_KIND_INVALID","Expected None, Step or Slope.",seatKindCell,true);
                else
                {
                    double expansion=0, seatHeight=0, seatElevation=0;
                    var seatSources=new List<SourceRef>{seatKindCell.Source};
                    var elevationCell=raw.ConfigCells.GetValueOrDefault("NeckSeatElevation");
                    if(elevationCell is null) Error("NECK_SEAT_ELEVATION_REQUIRED","Seat elevation must be explicit.",seatKindCell,true);
                    else
                    {
                        consumed.Add("Config.NeckSeatElevation"); sources["Config.NeckSeatElevation"]=elevationCell.Source;
                        seatSources.Add(elevationCell.Source);
                        seatElevation=Number(elevationCell,true,false,"NECK_SEAT_ELEVATION_REQUIRED");
                    }
                    if(kind!=NeckSeatKind.None)
                    {
                        if(neckHeight<=0) Error("NECK_SEAT_REQUIRES_NECK","Active seat requires a neck.",seatKindCell,true);
                        var expansionCell=Get("NeckSeatExpansion",true);
                        var heightCell=Get("NeckSeatHeight",true);
                        if(expansionCell is not null) seatSources.Add(expansionCell.Source);
                        if(heightCell is not null) seatSources.Add(heightCell.Source);
                        expansion=Number(expansionCell,true,false,"NECK_SEAT_EXPANSION_REQUIRED");
                        seatHeight=Number(heightCell,true,true,"NECK_SEAT_HEIGHT_REQUIRED");
                        if(expansion<0) Error("DIMENSION_NEGATIVE","Seat expansion cannot be negative.",expansionCell,true);
                        if(neckSpec is not null && neckSpec.ThicknessMm-expansion<=0)
                            Error("NECK_SEAT_WALL_INVALID","Seat would remove neck wall.",expansionCell,true);
                    }
                    if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                        seatSpec=new NeckSeatSpec(kind,expansion,seatHeight,seatElevation,seatSources);
                }
            }
            CoverPanelSpec? panelSpec=null;
            raw.ConfigCells.TryGetValue("CoverType",out var coverTypeCell);
            var coverKind=CoverKind.None;
            if(s2 && (coverTypeCell?.Kind is not (null or RawCellKind.Text or RawCellKind.Blank or RawCellKind.Formula) || !CoverKindMap.TryParse(Text(coverTypeCell),out coverKind)))
                Error("COVER_KIND_UNRESOLVED","Expected VHG cover kind Gang/BTCT or an explicit inactive cover.",coverTypeCell,true);
            if(s2 && coverTypeCell is not null) sources["Config.CoverType"]=coverTypeCell.Source;
            bool panelRequested=s2 && coverKind==CoverKind.Panel;
            if(panelRequested && raw.ConfigCells.TryGetValue("PanelCount",out var panelCountCell))
            {
                consumed.Add("Config.PanelCount"); sources["Config.PanelCount"]=panelCountCell.Source;
                if(Blank(panelCountCell)) Error("PANEL_COUNT_REQUIRED","Panel count is required when supplied.",panelCountCell,true);
                else
                {
                    var countValue=Number(panelCountCell,true);
                    if(!double.IsFinite(countValue) || countValue<1 || countValue>int.MaxValue || countValue!=Math.Truncate(countValue))
                        Error("PANEL_COUNT_INVALID","Panel count must be an integer >= 1.",panelCountCell,true);
                    else
                    {
                        var gapCell=raw.ConfigCells.GetValueOrDefault("PanelGap");
                        var directionCell=raw.ConfigCells.GetValueOrDefault("PanelDirection");
                        var heightCell=raw.ConfigCells.GetValueOrDefault("CoverHeight");
                        var supportCell=raw.ConfigCells.GetValueOrDefault("PanelSupportMode");
                        if(gapCell is null || directionCell is null || heightCell is null || supportCell is null)
                            Error("PANEL_FIELDS_REQUIRED","Gap, direction, support mode and height are required for panels.",panelCountCell,true);
                        else
                        {
                            consumed.UnionWith(["Config.PanelGap","Config.PanelDirection","Config.CoverHeight","Config.PanelSupportMode"]);
                            sources["Config.PanelGap"]=gapCell.Source; sources["Config.PanelDirection"]=directionCell.Source; sources["Config.CoverHeight"]=heightCell.Source; sources["Config.PanelSupportMode"]=supportCell.Source;
                            double gap=Number(gapCell,true), capHeight=Number(heightCell,true,true);
                            if(gap<0) Error("DIMENSION_NEGATIVE","Panel gap cannot be negative.",gapCell,true);
                            if(!PanelDirectionMap.TryParse(Text(directionCell),out var axis))
                                Error("PANEL_DIRECTION_INVALID","Panel split axis is unresolved.",directionCell,true);
                            else if(Text(supportCell) is not ("Recess" or "OverOuter"))
                                Error("PANEL_SUPPORT_UNRESOLVED","Panel support mode must be explicit.",supportCell,true);
                            else if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                                panelSpec=new CoverPanelSpec((int)countValue,gap,axis,Text(supportCell)=="Recess"?PanelSupportMode.Recess:PanelSupportMode.OverOuter,
                                    capHeight,[panelCountCell.Source,gapCell.Source,directionCell.Source,heightCell.Source,supportCell.Source]);
                        }
                    }
                }
            }

            // Owner-approved VHG F11/G11 rectangular inner recess; no fixture/default fallback.
            CollarSpec? collarSpec=null;
            double collarHeight=0;
            if(s2 && neckSpec is null && raw.ConfigCells.TryGetValue("NeckHaunchFlag",out var neckSwitch) && Active(neckSwitch))
                Error("NECK_SEAT_REQUIRES_NECK","Active F11 recess requires a positive neck height.",neckSwitch,true);
            if(s2 && seatSpec is null && neckSpec is not null)
            {
                var flag=raw.ConfigCells.GetValueOrDefault("NeckHaunchFlag");
                double expansion=0,seatHeight=0;
                var refs=new List<SourceRef>(neckSpec.Sources);
                if(flag is not null) refs.Add(flag.Source);
                if(flag is not null && Active(flag))
                {
                    var dimensions=Get("NeckHaunchSize",true);
                    var pair=Pair(dimensions,"NECK_SEAT_SIZE_INVALID");
                    expansion=pair.X; seatHeight=pair.Y;
                    if(dimensions is not null) refs.Add(dimensions.Source);
                    if(expansion>=neckSpec.ThicknessMm || seatHeight>neckHeight)
                        Error("NECK_SEAT_WALL_INVALID","Step must fit inside neck wall and height.",dimensions,true);
                    diagnostics.Add(new("VHG_NECK_STEP",DiagnosticSeverity.Review,DiagnosticScope.Component,id,"Neck",
                        flag.Source,"Step",Text(dimensions),"F11/G11 mapped as owner-approved rectangular recess."));
                }
                if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                    {
                        // HDSD p11: W terminates below the recess; D13 is the whole additional cover height.
                        neckSpec=new NeckSpec(neckSpec.InnerWidthMm,neckSpec.InnerLengthMm,neckSpec.ThicknessMm,
                            neckHeight+seatHeight,neckSpec.Position,refs);
                        seatSpec=new NeckSeatSpec(seatHeight>0?NeckSeatKind.Step:NeckSeatKind.None,expansion,seatHeight,
                            h+r+neckHeight,refs);
                    }
            }
            if(s2 && coverKind==CoverKind.Purchased && raw.ConfigCells.TryGetValue("CoverHaunchFlag",out var collarFlag) && Active(collarFlag))
            {
                var dimensions=Get("CoverHaunchSize",true);
                var pair=Pair(dimensions,"COLLAR_SIZE_INVALID");
                collarHeight=pair.Y;
                if(neckSpec is null) Error("COLLAR_NECK_REQUIRED","Collar requires neck geometry.",collarFlag,true);
                else if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                {
                    RectangleProfile Rect(double width,double length)=>new(new Rectangle2Mm(roofCenterX-width/2,roofCenterY-length/2,roofCenterX+width/2,roofCenterY+length/2));
                    collarSpec=new CollarSpec(Rect(roofL+2*pair.X,roofB+2*pair.X),Rect(roofL,roofB),
                        new DBTool.Domain.Geometry.Interval1D(h+r+neckHeight,h+r+neckHeight+pair.Y),"Concrete",
                        new[]{collarFlag.Source,dimensions!.Source}.Concat(neckSpec.Sources));
                }
            }
            (double X,double Y) Pair(RawCell? cell,string code)
            {
                var pieces=Regex.Split(Text(cell),"[xX×]");
                if(cell?.Kind is not (RawCellKind.Text or RawCellKind.Formula) || pieces.Length!=2) { Error(code,"Expected positive B x H.",cell,true);return (0,0); }
                return (Number(new RawCell(RawCellKind.Text,pieces[0],null,null,null,null,cell.Source),true,true),
                    Number(new RawCell(RawCellKind.Text,pieces[1],null,null,null,null,cell.Source),true,true));
            }
            PurchasedCoverSpec? purchasedSpec=null;
            if(s2 && coverKind==CoverKind.Purchased)
            {
                var blockCell=Get("CoverBlock",true);
                string code=Text(blockCell);
                if(blockCell?.Kind is not (RawCellKind.Text or RawCellKind.Formula) || !catalogs.Covers.TryGetValue(code,out var entry))
                    Error("U_NECK_COVER_CATALOG",$"Purchased cover '{code}' requires a physical catalog with units, local boundary, anchor, rotation and datum. CF.Data names alone are insufficient.",blockCell ?? coverTypeCell,true);
                else if(entry.InsertionOffset is null || entry.RotationDegrees is null || entry.PlacementDatum is null || entry.Sources.IsEmpty)
                    Error("U_NECK_COVER_CATALOG",$"Purchased cover '{code}' has unresolved placement/provenance.",blockCell,true);
                else
                {
                    double capHeight=0;
                    bool nativeVhg=entry.BlockSet is not null || entry.Sources.Any(x=>x.SourceKind==SourceKind.Dxf && x.RelativePath=="VHGpro 3.2/Template/ACAD-VHG-Template.dxf");
                    if(nativeVhg) capHeight=Number(Get("CoverHeight",true),true,true);
                    double? placementZ=entry.PlacementDatum==CoverPlacementDatum.NeckTop ? h+r+neckHeight+(nativeVhg?capHeight:0) : seatSpec?.SeatElevationMm;
                    if (sizeCell is not null && roofL > 0 && roofB > 0)
                    {
                        var opening=new Rectangle2Mm(roofCenterX-roofL/2,roofCenterY-roofB/2,roofCenterX+roofL/2,roofCenterY+roofB/2);
                        CoverSupportSurface? support=null;
                        if(entry.BlockSet is {NominalOpening:null} && !DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                        {
                            if(collarSpec is not null || seatSpec is {Kind:not NeckSeatKind.None})
                                Error("COVER_SUPPORT_PROFILE_UNRESOLVED","Native stepped/collar support requires its actual contact surface and frame clearance; no plain-wall substitution.",blockCell,true);
                            else
                            {
                                var edges=neckSpec is null
                                    ? new[]{-l/2-t,-b/2-t,l/2+t,b/2+t}
                                    : new[]{opening.MinX-neckSpec.ThicknessMm,opening.MinY-neckSpec.ThicknessMm,
                                        opening.MaxX+neckSpec.ThicknessMm,opening.MaxY+neckSpec.ThicknessMm};
                                double supportElevation=h+r+(neckSpec?.HeightMm ?? 0);
                                if(edges.Any(value=>!double.IsFinite(value)) || !double.IsFinite(supportElevation))
                                    Error("COVER_SUPPORT_GEOMETRY_OVERFLOW","Derived concrete support coordinates/elevation must remain finite.",blockCell,true);
                                else
                                    support=new CoverSupportSurface(new(edges[0],edges[1],edges[2],edges[3]),opening,supportElevation,
                                        new[]{sizeCell.Source,sources["Row.Length"],sources["Row.Width"],sources["Row.WallThickness"],sources["Row.Height"],sources["Row.RoofThickness"]}
                                            .Concat(neckSpec?.Sources ?? []));
                            }
                        }
                        if(entry.BlockSet?.NominalOpening is not null)
                        {
                            var compatibility=OwnerCoverAssemblyValidator.Validate(entry,opening,capHeight,
                                policy.TolerancePolicy.GeometryMm,blockCell!.Source,sizeCell.Source);
                            diagnostics.AddRange(compatibility.Diagnostics);
                        }
                        else
                        {
                            var compatibility=CoverAssemblyValidator.Validate(entry,opening,neckSpec?.ThicknessMm,
                                policy.TolerancePolicy.GeometryMm,blockCell!.Source,sizeCell.Source,support,placementZ+entry.InsertionOffset.Value.Z);
                            diagnostics.AddRange(compatibility.Diagnostics);
                        }
                    }
                    if(placementZ is null)
                        Error("U_NECK_COVER_DATUM",$"Purchased cover '{code}' requires an explicit seat support plane.",blockCell,true);
                    else
                    {
                        var offset=entry.InsertionOffset.Value;
                        double x=roofCenterX+offset.X,y=roofCenterY+offset.Y,z=placementZ.Value+offset.Z;
                        if(!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
                            Error("COVER_PLACEMENT_INVALID","Cover placement overflows.",blockCell,true);
                        else if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                            purchasedSpec=new PurchasedCoverSpec(code,new(x,y,z),entry.PhysicalSize,
                            new[]{coverTypeCell!.Source,blockCell!.Source,sources["Row.Length"],sources["Row.Width"],
                                sources["Row.Height"],sources["Row.RoofThickness"],sources["Row.NeckHeight"],sizeCell!.Source,positionCell!.Source}
                                .Concat(entry.Sources).Concat(entry.OpeningContract?.Sources ?? []).Concat(nativeVhg?new[]{sources["Config.CoverHeight"]}.Concat(collarSpec?.Sources ?? []):Array.Empty<SourceRef>()).Concat(entry.PlacementDatum==CoverPlacementDatum.SeatPlane ? seatSpec!.Sources : Array.Empty<SourceRef>()),
                            entry.RotationDegrees.Value);
                    }
                }
            }

            CatchBasinSpec? catchSpec=null;
            if(s2 && raw.ConfigCells.ContainsKey("CatchOwner"))
            {
                var ownerCell=Get("CatchOwner",true);
                var wallCell=Get("CatchWall",true);
                var positionCatchCell=Get("CatchPosition",true);
                CatchBasinOwner owner=Text(ownerCell) switch
                { "Body"=>CatchBasinOwner.Body,"Neck"=>CatchBasinOwner.Neck,_=>(CatchBasinOwner)(-1) };
                WallId catchWall=Text(wallCell) switch
                { "T1"=>WallId.T1,"T2"=>WallId.T2,"T3"=>WallId.T3,"T4"=>WallId.T4,_=>(WallId)(-1) };
                HorizontalPosition catchPosition=Text(positionCatchCell) switch
                { "Left"=>HorizontalPosition.Left,"Center"=>HorizontalPosition.Center,
                  "Right"=>HorizontalPosition.Right,_=>(HorizontalPosition)(-1) };
                if(!Enum.IsDefined(owner)) Error("CATCH_OWNER_INVALID","Expected Body or Neck.",ownerCell,true);
                if(!Enum.IsDefined(catchWall)) Error("CATCH_WALL_INVALID","Expected T1..T4.",wallCell,true);
                if(!Enum.IsDefined(catchPosition)) Error("CATCH_POSITION_INVALID","Expected Left, Center or Right.",positionCatchCell,true);
                if(owner==CatchBasinOwner.Neck && catchPosition!=HorizontalPosition.Center)
                    Error("CATCH_NECK_POSITION_UNSUPPORTED","Neck catch basin supports Center only.",positionCatchCell,true);
                var catchValues=new Dictionary<string,double>(StringComparer.Ordinal);
                var catchSources=new Dictionary<string,SourceRef>(StringComparer.Ordinal);
                foreach(var (name,key) in new (string Name,string Key)[]
                {
                    ("OpeningWidth","CatchOpeningWidth"),("OpeningHeight","CatchOpeningHeight"),
                    ("ChamberWidth","CatchChamberWidth"),("ChamberDepth","CatchChamberDepth"),
                    ("ChamberHeight","CatchChamberHeight"),("WallThickness","CatchWallThickness"),
                    ("BaseThickness","CatchBaseThickness"),("Offset","CatchOffset")
                })
                {
                    var cell=Get(key,true);
                    catchValues[name]=Number(cell,true,name!="Offset",$"{key.ToUpperInvariant()}_REQUIRED");
                    if(cell is not null) catchSources[name]=cell.Source;
                }
                if(ownerCell is not null) catchSources["Owner"]=ownerCell.Source;
                if(wallCell is not null) catchSources["Wall"]=wallCell.Source;
                if(positionCatchCell is not null) catchSources["Position"]=positionCatchCell.Source;
                if(catchValues["ChamberHeight"]<catchValues["OpeningHeight"])
                    Error("CATCH_CHAMBER_TOO_SHORT","Chamber height must contain opening height.",
                        raw.ConfigCells.GetValueOrDefault("CatchChamberHeight"),true);
                if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                {
                    double catchPad = 0, catchPadExp = 0;
                    if(raw.ConfigCells.TryGetValue("CatchPadThickness", out var cptCell) || raw.ConfigCells.TryGetValue("Catch.J", out cptCell)) { catchPad = Number(cptCell, true, false); if(cptCell != null) catchSources["Pad"] = cptCell.Source; }
                    if(raw.ConfigCells.TryGetValue("CatchPadExpansion", out var cpeCell) || raw.ConfigCells.TryGetValue("Catch.K", out cpeCell)) { catchPadExp = Number(cpeCell, true, false); if(cpeCell != null) catchSources["PadExpansion"] = cpeCell.Source; }
                    bool catchReinforced = false;
                    if(raw.ConfigCells.TryGetValue("CatchReinforced", out var crCell) || raw.ConfigCells.TryGetValue("Catch.L", out crCell)) { catchReinforced = Text(crCell) is "Có" or "BTCT"; if(crCell != null) catchSources["Reinforced"] = crCell.Source; }
                    string? catchProduct = null;
                    if(raw.ConfigCells.TryGetValue("CatchProductCode", out var cpCodeCell) || raw.ConfigCells.TryGetValue("Catch.E", out cpCodeCell)) { catchProduct = Text(cpCodeCell); if(cpCodeCell != null) catchSources["ProductCode"] = cpCodeCell.Source; }
                    catchSpec=new CatchBasinSpec(owner,catchWall,catchPosition,
                        catchValues["OpeningWidth"],catchValues["OpeningHeight"],
                        catchValues["ChamberWidth"],catchValues["ChamberDepth"],catchValues["ChamberHeight"],
                        catchValues["WallThickness"],catchValues["BaseThickness"],catchValues["Offset"],catchSources,
                        catchPad, catchPadExp, catchReinforced, catchProduct, catchPosition);
                }
            }

            MainRebarInput? mainRebar=null;
            if(policy.PolicyId=="S2Rebar-1" && request.Scope is CalculationScope.FullRebar or CalculationScope.Full)
            {
                var rebarSources=new Dictionary<string,SourceRef>();
                MeshSpec? OptionalMesh(string key)
                {
                    if(!row.Cells.TryGetValue(key,out var cell) || Blank(cell)) return null;
                    rebarSources[key]=cell.Source;
                    return Mesh(key);
                }
                double ConfigNumber(string key,bool positive=false)
                {
                    var cell=Get(key,true); if(cell is not null) rebarSources[key]=cell.Source;
                    return Number(cell,true,positive);
                }
                WallOrNeckRebarSpec? Face(string key,bool outer,bool neck)
                {
                    var mesh=OptionalMesh(key); if(mesh is null) return null;
                    string deltaKey=neck?"NeckSpacingDelta":"WallSpacingDelta",stepKey=neck?"NeckDiaStepDown":"WallDiaStepDown";
                    double delta=ConfigNumber(deltaKey), step=ConfigNumber(stepKey), coverFace=ConfigNumber("CoverWall");
                    if(delta<0 || step<0 || step!=Math.Truncate(step) || step>int.MaxValue || coverFace<0)
                    { Error("MEMBER_REBAR_CONFIG_INVALID","Nonnegative spacing delta, integer diameter step and cover required.",raw.ConfigCells.GetValueOrDefault(stepKey),true); return null; }
                    int[] diameters=[6,8,10,12,14,16];
                    if(Array.IndexOf(diameters,mesh.DiameterMm)-(int)step<0)
                    { Error("MEMBER_DIAMETER_STEP_INVALID","Diameter step-down is outside catalog.",raw.ConfigCells.GetValueOrDefault(stepKey),true);return null; }
                    if(!double.IsFinite(delta) || !double.IsFinite(step) || !double.IsFinite(coverFace) || DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any()) return null;
                    return new(mesh.DiameterMm,mesh.SpacingMm,(int)step,delta,StructureKind.ReinforcedConcrete,coverFace,
                        innerLayer:!outer,outerLayer:outer,sources:new[]{row.Cells[key].Source,raw.ConfigCells[deltaKey].Source,raw.ConfigCells[stepKey].Source,raw.ConfigCells["CoverWall"].Source});
                }
                var wallOuter=Face("WallOuterMesh",true,false); var wallInner=Face("WallInnerMesh",false,false);
                var roofTop=OptionalMesh("RoofTopMesh");var roofBottom=OptionalMesh("RoofBottomMesh");
                double roofCover=roofTop is not null || roofBottom is not null?ConfigNumber("CoverTop"):0;
                var structureCell=raw.ConfigCells.GetValueOrDefault("NeckStructure");
                string structureName=Text(structureCell);
                if(neckHeight>0 && structureName is not ("BTCT" or "Khác" or "Gạch" or "Không"))
                    Error("NECK_STRUCTURE_INVALID","Expected BTCT/Khác/Gạch/Không for an active neck.",structureCell,true);
                if(roofCover<0) Error("COVER_NEGATIVE","Roof cover cannot be negative.",raw.ConfigCells.GetValueOrDefault("CoverTop"),true);
                var structure=structureName=="BTCT"?StructureKind.ReinforcedConcrete:StructureKind.Other;
                var neckOuter=structure==StructureKind.ReinforcedConcrete && neckHeight>0?Face("NeckOuterMesh",true,true):null;
                var neckInner=structure==StructureKind.ReinforcedConcrete && neckHeight>0?Face("NeckInnerMesh",false,true):null;
                LadderSpec? ladder=null;
                double? wallSupportCover=null;
                if(raw.ConfigCells.TryGetValue("Ladder.C",out var ladderLocation) && Active(ladderLocation))
                {
                    wallSupportCover=ConfigNumber("CoverWall");
                    if(wallSupportCover<0) Error("COVER_NEGATIVE","Wall support cover cannot be negative.",raw.ConfigCells.GetValueOrDefault("CoverWall"),true);
                    if(Text(ladderLocation)!="Theo vị trí cổ") Error("LADDER_LOCATION_UNSUPPORTED","Expected Theo vị trí cổ.",ladderLocation,true);
                    var pair=Pair(Get("Ladder.E",true),"LADDER_SPACING_INVALID");
                    var dims=Get("Ladder.F",true);var pieces=Regex.Split(Text(dims),"[xX×]");
                    double diameter=ConfigNumber("Ladder.D",true);
                    if(diameter!=Math.Truncate(diameter) || diameter>int.MaxValue) Error("LADDER_DIAMETER_INVALID","Integer diameter required.",raw.ConfigCells.GetValueOrDefault("Ladder.D"),true);
                    if(dims?.Kind!=RawCellKind.Text || pieces.Length!=3) Error("LADDER_SHAPE_INVALID","Expected hook x depth x width.",dims,true);
                    else
                    {
                        var shape=pieces.Select(x=>Number(new RawCell(RawCellKind.Text,x,null,null,null,null,dims.Source),true,true)).ToArray();
                        var refs=new[]{ladderLocation.Source,dims.Source}.Concat(new[]{"Ladder.D","Ladder.E"}.Where(raw.ConfigCells.ContainsKey).Select(k=>raw.ConfigCells[k].Source)).ToArray();
                        foreach(var source in refs) rebarSources["Ladder/"+source.CellAddress]=source;
                        if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                            ladder=new(pair.X,pair.Y,0,shape[2],shape[1],shape[0],(int)diameter,sources:refs);
                    }
                }
                var hookMap=new Dictionary<string,HookPattern>();
                foreach(var (name,key,active) in new[]{
                    ("WallOuter","HookPattern.E",wallOuter is not null),("WallInner","HookPattern.F",wallInner is not null),
                    ("RoofTop","HookPattern.G",roofTop is not null),("RoofBottom","HookPattern.H",roofBottom is not null),
                    ("NeckOuter","HookPattern.I",neckOuter is not null),("NeckInner","HookPattern.J",neckInner is not null)})
                    if(active) { hookMap[name]=Hook(key);if(raw.ConfigCells.TryGetValue(key,out var hookCell)) rebarSources[key]=hookCell.Source; }
                HookMode hookMode=HookMode.ClearThickness;int? hookMultiple=null;
                foreach(var key in new[]{"HookPattern.C","HookPattern.D"})
                    if(raw.ConfigCells.TryGetValue(key,out var baseHookCell)) rebarSources[key]=baseHookCell.Source;
                bool hasHooks=hookBottom!=HookPattern.Hook0M || hookTop!=HookPattern.Hook0M || hookMap.Values.Any(x=>x!=HookPattern.Hook0M);
                if(hasHooks)
                {
                    var modeCell=Get("HookMode",true);string mode=Text(modeCell);
                    if(mode is "Theo ĐK" or "Theo DK")
                    {
                        hookMode=HookMode.DiameterMultiple;double multiple=ConfigNumber("HookMultiple",true);
                        if(multiple!=Math.Truncate(multiple) || multiple>int.MaxValue) Error("HOOK_MULTIPLE_INVALID","Integer multiple required.",raw.ConfigCells.GetValueOrDefault("HookMultiple"),true);
                        else hookMultiple=(int)multiple;
                    }
                    else if(mode!="Theo KC") Error("HOOK_MODE_INVALID","Expected Theo KC/Theo ĐK.",modeCell,true);
                    if(modeCell is not null) rebarSources["HookMode"]=modeCell.Source;
                }
                AngleFrameInput? angleFrame=null;
                if(seatSpec?.Kind==NeckSeatKind.Step && raw.ConfigCells.TryGetValue("AngleCode",out var angleCell) && Active(angleCell))
                {
                    string code=Text(angleCell); var position=raw.ConfigCells.GetValueOrDefault("AnglePosition");
                    string positions=Text(position);
                    if(!catalogs.Angles.ContainsKey(code)) Error("ANGLE_CATALOG_MISSING","Resolved steel angle catalog required.",angleCell,true);
                    if(positions.Length==0 || positions.Any(c=>c is < '1' or > '3') || positions.Distinct().Count()!=positions.Length)
                        Error("ANGLE_POSITIONS_INVALID","Choose unique positions 1, 2, 3.",position,true);
                    if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                        angleFrame=new(code,new EnabledPositionsSet(positions.Select(c=>c-'0')),[angleCell.Source,position!.Source]);
                }
                var accessoryInput=AccessoryInputMapper.Parse(raw,id,numericPolicy);
                diagnostics.AddRange(accessoryInput.Diagnostics);
                if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).Any())
                    mainRebar=new(wallOuter,wallInner,roofTop,roofBottom,roofCover,neckOuter,neckInner,structure,ladder,
                    rebarSources.ToImmutableDictionary(),hookMap,hookMode,hookMultiple,angleFrame,accessoryInput.Value,wallSupportCover);
            }
            var pipes=new List<PipeConnectionSpec>();
            for(int i=1;i<=4;i++)
            {
                var codeCell=Get($"PipeCodeT{i}"); var invertCell=Get($"InvertT{i}");
                bool codeBlank=Blank(codeCell), invertBlank=Blank(invertCell);
                if(codeBlank && invertBlank) continue;
                if(codeBlank) { Error("PIPE_CODE_REQUIRED","Invert needs a pipe code.",codeCell,false);continue; }
                if(invertBlank) { Error("PIPE_INVERT_REQUIRED","Pipe code needs an invert.",invertCell,false);continue; }
                string code=Text(codeCell);
                if(catalogs.Aliases.TryGetValue(code,out var canonical)) code=canonical;
                string? resolved=catalogs.Pipes.Keys.SingleOrDefault(k=>k.Equals(code,StringComparison.OrdinalIgnoreCase));
                if(resolved==null)
                {
                    Error("PIPE_CATALOG_MISSING","Pipe code not found.",codeCell,false);
                }
                double invert=Number(invertCell,false);
                if(invert<0) Error("PIPE_INVERT_NEGATIVE","Pipe invert must be nonnegative.",invertCell,false);
                double offset=Number(Get($"PipeOffsetT{i}",true),true), mortar=NonNegative("PipeMortar",true);
                string? pipeId=null;
                RawCell? pipeIdCell=null;
                if(s2)
                {
                    pipeId=$"Pipe/T{i}";
                    if(raw.ConfigCells.TryGetValue($"PipeIdT{i}",out pipeIdCell))
                    {
                        consumed.Add($"Config.PipeIdT{i}");
                        sources[$"Config.PipeIdT{i}"]=pipeIdCell.Source;
                        pipeId=Text(pipeIdCell);
                        if(string.IsNullOrWhiteSpace(pipeId))
                            Error("PIPE_ID_REQUIRED","An explicit pipe ID cannot be blank.",pipeIdCell,true);
                    }
                }
                if(resolved!=null && invert>=0 && double.IsFinite(invert) && double.IsFinite(offset) && mortar>=0)
                {
                    var pipeSources=new Dictionary<string,SourceRef>{{"Code",codeCell!.Source},{"Invert",invertCell!.Source}};
                    if(pipeIdCell is not null) pipeSources["Id"]=pipeIdCell.Source;
                    if(!s2 || !string.IsNullOrWhiteSpace(pipeId))
                        pipes.Add(new PipeConnectionSpec((WallId)(i-1),resolved,invert,offset,mortar,pipeSources,pipeId));
                }
            }
            var duplicatePipeId=pipes.Where(p=>p.Id is not null).GroupBy(p=>p.Id!,StringComparer.Ordinal)
                .FirstOrDefault(g=>g.Count()>1);
            if(duplicatePipeId is not null)
                diagnostics.Add(new Diagnostic("PIPE_ID_DUPLICATE",DiagnosticSeverity.Block,DiagnosticScope.Manhole,
                    id,null,duplicatePipeId.Last().SourceRefs.GetValueOrDefault("Id"),"unique pipe ID",
                    duplicatePipeId.Key,"Pipe IDs must be unique within a manhole."));
            if(bottom!=null && top!=null && f<2*cover+2*bottom.DiameterMm+2*top.DiameterMm)
                Error("BASE_LAYER_COLLISION","Two directions in both layers cannot fit.",Get("BaseThickness"),false);
            if(l+2*t+2*eb-2*cover<=0 || b+2*t+2*eb-2*cover<=0)
                Error("BAR_END_COVER_INVALID","No positive straight length remains.",Get("CoverBottom",true),true);

            string displayName=Text(Get("Name"));
            // Carry optional/out-of-scope fields intact for T9; never silently switch requested features off.
            var unsupported=new Dictionary<string,RawCell>();
            foreach(var pair in row.Cells) if(!consumed.Contains("Row."+pair.Key)) unsupported["Row."+pair.Key]=pair.Value;
            foreach(var pair in raw.ConfigCells) if(!pair.Key.Contains('!') && !consumed.Contains("Config."+pair.Key)) unsupported["Config."+pair.Key]=pair.Value;
            foreach(var (feature,keys) in new (string,string[])[] {
                ("WallRebar",["Row.WallOuterMesh","Row.WallInnerMesh"]), ("RoofRebar",["Row.RoofTopMesh","Row.RoofBottomMesh"]),
                ("NeckRebar",["Row.NeckOuterMesh","Row.NeckInnerMesh"]), ("ProductCoverOrPanel",["Config.CoverType","Config.PanelCount"]),
                ("NeckAnchor",["Config.Anchor"]), ("AngleFrame",["Config.AngleCode","Config.AnglePosition"]), ("RoofOpeningReinforcement",["Config.RoofRebar.C","Config.RoofRebar.E"]), ("Ties",["Config.Ties.H"]), ("Ladder",["Config.Ladder.C"]),
                ("PipeReinforcement",["Config.Pipe.D","Config.Pipe.F"]), ("LiftingHooks",["Config.LiftingSpec"]), ("Dowels",["Config.HasDowel"]) })
            {
                if(!s2 && feature is "AngleFrame" or "RoofOpeningReinforcement") continue;
                var parameters=keys.Where(unsupported.ContainsKey).ToDictionary(k=>k,k=>unsupported[k]);
                if(s2 && feature=="ProductCoverOrPanel")
                {
                    // Passive panel count must neither request panels nor affect the semantic hash.
                    if(coverKind!=CoverKind.Panel) parameters.Remove("Config.PanelCount");
                    features.Add(new FeatureRequest(feature,coverKind!=CoverKind.None,parameters));
                }
                else if(s2 && mainRebar is not null && feature is "WallRebar" or "RoofRebar" or "NeckRebar" or "Ladder")
                {
                    var original=keys.Select(k=>(Key:k,Cell:k.StartsWith("Row.",StringComparison.Ordinal)?row.Cells.GetValueOrDefault(k[4..]):raw.ConfigCells.GetValueOrDefault(k[7..])))
                        .Where(x=>x.Cell is not null).ToDictionary(x=>x.Key,x=>x.Cell!);
                    bool requested=feature switch {
                        "WallRebar"=>mainRebar.WallOuter is not null || mainRebar.WallInner is not null,
                        "RoofRebar"=>mainRebar.RoofTop is not null || mainRebar.RoofBottom is not null,
                        "NeckRebar"=>neckHeight>0 && (mainRebar.NeckOuter is not null || mainRebar.NeckInner is not null || mainRebar.NeckStructure==StructureKind.Other),
                        _=>mainRebar.Ladder is not null };
                    features.Add(new(feature,requested,original));
                }
                else if(parameters.Count>0)
                {
                    bool requested=feature switch {
                        "AngleFrame"=>parameters.TryGetValue("Config.AngleCode",out var angle) && Active(angle) && raw.ConfigCells.TryGetValue("NeckHaunchFlag",out var flag) && Active(flag),
                        "PipeReinforcement"=>mainRebar is not null?mainRebar.Accessories?.Pipe is not null:parameters.TryGetValue("Config.Pipe.F",out var count) && Active(count),
                        "RoofOpeningReinforcement"=>mainRebar is not null?mainRebar.Accessories?.Roof is not null:parameters.TryGetValue("Config.RoofRebar.E",out var count) && Active(count),
                        _=>parameters.Values.Any(Active) };
                    features.Add(new FeatureRequest(feature,requested,parameters));
                }
            }
            if(s2)
            {
                foreach(var feature in features.Where(f=>f.Requested && f.FeatureId!="ProductCoverOrPanel" && !(mainRebar is not null && f.FeatureId is "WallRebar" or "RoofRebar" or "NeckRebar" or "Ladder")))
                    diagnostics.Add(new Diagnostic("S2_FEATURE_DEFERRED",DiagnosticSeverity.Review,DiagnosticScope.Manhole,id,null,
                        feature.Parameters.Values.FirstOrDefault()?.Source,"consumer in later session",feature.FeatureId,
                        request.Scope==CalculationScope.GeometryOnly
                            ? $"Active {feature.FeatureId} input is retained but outside GeometryOnly scope."
                            : $"Active {feature.FeatureId} input is retained for {request.Scope}; a complete consumer is pending and the capability gate still applies."));
                if(panelRequested && !raw.ConfigCells.ContainsKey("PanelCount"))
                    Error("PANEL_COUNT_REQUIRED","Panel count is required for a panel cover.",coverTypeCell,true);
            }
            if(!DrawingInputGate.GetBlockingDiagnostics([id],diagnostics).IsEmpty) continue;
            var input=new ManholeInput(id,displayName,raw.Profile,l,b,h,f,t,r,p,eb,ep,bedding,baseHaunch,topHaunch,neckHeight,
                new Rectangle2Mm(roofCenterX-roofL/2,roofCenterY-roofB/2,roofCenterX+roofL/2,roofCenterY+roofB/2),pipes,bottom!,top!,cover,elevationMode,datum,policy.PipeInvertReference,
                hookBottom,hookTop,distribution,policy.MeshJunctionPolicy,policy.LayerStackPolicy,features,sources,unsupported,
                baseHaunchSpec:baseHaunchSpec,topHaunchSpec:topHaunchSpec,neckSpec:neckSpec,neckSeatSpec:seatSpec,coverPanelSpec:panelSpec,
                purchasedCoverSpec:purchasedSpec,catchBasinSpec:catchSpec, collarSpec:collarSpec, memberRebarSpec:MemberSpec(bottom!, top!, cover), mainRebar:mainRebar);
            specs.Add(new ValidatedManholeSpec(input,catalogs,policy.Version,sources.Values.Distinct(),diagnostics.Where(d=>d.Severity!=DiagnosticSeverity.Block)));
        }
        var blocks=DrawingInputGate.GetBlockingDiagnostics(request.SelectedIds,diagnostics);
        if(!blocks.IsEmpty) return new(false,null,diagnostics.Where(d=>d.Severity!=DiagnosticSeverity.Block || blocks.Contains(d)));
        var applicable=diagnostics.Where(d=>d.Severity!=DiagnosticSeverity.Block).ToArray();
        return new(true,new ValidatedProjectInput(specs,ValidationInputHash.Compute(specs,policy,request.Scope),policy.Version,applicable,raw.WorkbookHash),applicable);
    }
    private static MemberRebarSpec MemberSpec(MeshSpec bottom, MeshSpec top, double cover) => new([
        new MemberRebarLayerSpec("Base/Bottom/X", Layer.Bottom, Axis.X, RebarDirection.Horizontal, bottom.DiameterMm, bottom.SpacingMm, "Base/Bottom/X"),
        new MemberRebarLayerSpec("Base/Bottom/Y", Layer.Bottom, Axis.Y, RebarDirection.Vertical, bottom.DiameterMm, bottom.SpacingMm, "Base/Bottom/Y"),
        new MemberRebarLayerSpec("Base/Top/X", Layer.Top, Axis.X, RebarDirection.Horizontal, top.DiameterMm, top.SpacingMm, "Base/Top/X"),
        new MemberRebarLayerSpec("Base/Top/Y", Layer.Top, Axis.Y, RebarDirection.Vertical, top.DiameterMm, top.SpacingMm, "Base/Top/Y")
    ], cover, "Base");
    private static bool Blank(RawCell? cell)=>cell==null || cell.Kind==RawCellKind.Blank || cell.Kind==RawCellKind.Text && string.IsNullOrWhiteSpace(cell.Text);
}
