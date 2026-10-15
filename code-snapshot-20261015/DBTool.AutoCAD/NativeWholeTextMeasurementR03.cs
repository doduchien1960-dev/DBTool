using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DBTool.Application.Templates;
using DBTool.Domain.StageContractsV2;
namespace DBTool.AutoCAD.Template;

/// <summary>Measures the complete final string in a disposable database with the exact cloned target style. No per-glyph approximation.</summary>
public sealed class NativeWholeTextMeasurementR03(Database target, Action<string>? trace = null) : INativeWholeTextMeasurementR03
{
    public NativeTextMeasureResultR03 Measure(NativeAssetContextR03 context,NativeTextMeasureRequestR03 request)
    {
        void Fault(string code,string field,string message)=>throw new NativeAssetContractException(code,field,message);
        if(request.ContextSha256!=context.Sha256||string.IsNullOrWhiteSpace(request.Id)||string.IsNullOrWhiteSpace(request.RunId))Fault("TEXT_CONTEXT_MISMATCH","request","Run and exact context required.");
        if(request.TextRaw!=request.TextDecoded)Fault("TEXT_ENCODING_UNRESOLVED","text_raw/text_decoded","This provider supports proven identity text only; a legacy decoder needs its own verified source binding.");
        if(request.EntityKind=="MText" && request.TextDecoded.Contains('\\'))Fault("TEXT_FORMAT_COVERAGE_UNVERIFIED","MText.contents","Formatted control streams require native font-run coverage; plain-string glyph coverage is insufficient.");
        if(request.TextDecoded.Contains("%%",StringComparison.Ordinal)||request.TextDecoded.Contains("%<",StringComparison.Ordinal))Fault("TEXT_FORMAT_COVERAGE_UNVERIFIED","text control stream","Native escape/field expansion needs glyph coverage for the evaluated native string; raw character coverage cannot authorize it.");
        if(!double.IsFinite(request.HeightMm)||request.HeightMm<=0||!double.IsFinite(request.WidthFactor)||request.WidthFactor<=0||!double.IsFinite(request.RotationRad))Fault("TEXT_METRIC_INVALID","height/width_factor/rotation","Finite source-bound metrics required.");
        if(target.Insunits!=UnitsValue.Millimeters)Fault("TEXT_TARGET_UNIT_MISMATCH","target.INSUNITS","Measurement target must be normalized mm.");
        if(request.SourceIds.Length==0||request.SourceIds.Any(x=>!context.Sources.ContainsKey(x)))Fault("TEXT_SOURCE_UNRESOLVED","source_ids","Resolve all text/style/format sources.");
        var font=context.Styles.SingleOrDefault(x=>x.StyleId==request.StyleId)??throw new NativeAssetContractException("FONT_SOURCE_MISSING",request.StyleId,"Style context absent.");
        if(font.FallbackDetected||!font.ActualFontVerified||font.FontFileSha256!=request.FontFileSha256||font.BigFontSha256!=request.BigFontSha256)Fault("FONT_SUBSTITUTION_BLOCKED",request.StyleId,"Actual font identity must match verified context.");
        if(!File.Exists(font.FontPath)||NativeContractHash.File(font.FontPath)!=font.FontFileSha256||font.BigFontPath is not null && (!File.Exists(font.BigFontPath)||NativeContractHash.File(font.BigFontPath)!=font.BigFontSha256))Fault("FONT_SOURCE_MISSING",request.StyleId,"Font bytes changed or unavailable.");
        var previous=HostApplicationServices.WorkingDatabase;
        Database? scratch=null;
        try
        {
            // Newly constructed side databases have Undo disabled (AutoCAD 2026 SDK).
            // Aborting a transaction cannot be the no-mutation guarantee. Clone the
            // requested verified style into a disposable database; never append to target.
            ObjectId sourceStyleId;string sourceFileName;string sourceBigFontFileName;
            using(var sourceRead=target.TransactionManager.StartTransaction())
            {
                var sourceStyles=(TextStyleTable)sourceRead.GetObject(target.TextStyleTableId,OpenMode.ForRead);
                if(!sourceStyles.Has(request.StyleId))Fault("TEXT_STYLE_MISSING",request.StyleId,"Style absent in measurement target.");
                sourceStyleId=sourceStyles[request.StyleId];
                var sourceStyle=(TextStyleTableRecord)sourceRead.GetObject(sourceStyleId,OpenMode.ForRead);
                sourceFileName=sourceStyle.FileName;sourceBigFontFileName=sourceStyle.BigFontFileName;
                trace?.Invoke(request.Id+":source-style="+System.Text.Json.JsonSerializer.Serialize(NativeAssetSignatures.Read(sourceRead,sourceStyle)));
                if(NativeAssetSignatures.Read(sourceRead,sourceStyle).Hash!=font.NativeStyleHash)Fault("FONT_STYLE_MISMATCH",request.StyleId,"Target style differs from context.");
            }
            scratch=new Database(true,true);scratch.Insunits=target.Insunits;
            using(var mapping=new IdMapping())
                target.WblockCloneObjects(new ObjectIdCollection([sourceStyleId]),scratch.TextStyleTableId,mapping,DuplicateRecordCloning.Replace,false);
            trace?.Invoke(request.Id+":set-working-scratch-database");
            HostApplicationServices.WorkingDatabase=scratch;
            using var tr=scratch.TransactionManager.StartTransaction();
            var styles=(TextStyleTable)tr.GetObject(scratch.TextStyleTableId,OpenMode.ForRead);
            if(!styles.Has(request.StyleId))Fault("TEXT_STYLE_MISSING",request.StyleId,"Style absent in measurement target.");
            var styleId=styles[request.StyleId];
            var style=(TextStyleTableRecord)tr.GetObject(styleId,OpenMode.ForWrite);
            // WblockClone strips the directory from font paths. Restore only these
            // two exact source values on scratch, then compare the full signature.
            style.FileName=sourceFileName;style.BigFontFileName=sourceBigFontFileName;
            trace?.Invoke(request.Id+":scratch-style="+System.Text.Json.JsonSerializer.Serialize(NativeAssetSignatures.Read(tr,style)));
            if(NativeAssetSignatures.Read(tr,style).Hash!=font.NativeStyleHash)Fault("FONT_STYLE_MISMATCH",request.StyleId,"Target style differs from context.");
            if(font.BigFontPath is not null)Fault("FONT_BIGFONT_COVERAGE_UNVERIFIED",request.StyleId,"SHX/bigfont glyph coverage needs a native verified adapter; no TTF substitution.");
            if(style.Font.Bold || style.Font.Italic)Fault("FONT_RUNTIME_VARIANT_UNVERIFIED",request.StyleId,"The effective bold/italic font file must be independently resolved before measurement.");
            trace?.Invoke(request.Id+":resolve-native-font-file");
            string resolvedFont;
            try { resolvedFont=HostApplicationServices.Current.FindFile(style.FileName,scratch,FindFileHint.FontFile); }
            catch(Autodesk.AutoCAD.Runtime.Exception e) { throw new NativeAssetContractException("FONT_SOURCE_MISSING",style.FileName,e.ErrorStatus+": "+e.Message); }
            var coverage=TrueTypeTextCoverage.Verify(resolvedFont,font.FontFileSha256,request.TextDecoded);
            trace?.Invoke(request.Id+":native-font-bytes-and-glyphs-verified");
            if(style.IsVertical!=request.Vertical)Fault("TEXT_STYLE_MISMATCH","vertical","Requested vertical mode differs from style.");
            trace?.Invoke(request.Id+":before-open-model");
            var model=(BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(scratch),OpenMode.ForWrite);
            trace?.Invoke(request.Id+":after-open-model");
            Entity entity;
            if(request.EntityKind is "DBText" or "Attribute")
            {
                if(request.WrappingWidthMm is not null||request.Attachment is not null)Fault("TEXT_REQUEST_INVALID","wrapping/attachment","Single-line native text has no wrapping/attachment.");
                if(!Enum.TryParse<TextHorizontalMode>(request.HorizontalMode,out var h)||!Enum.IsDefined(h)||!Enum.TryParse<TextVerticalMode>(request.VerticalMode,out var v)||!Enum.IsDefined(v))Fault("TEXT_ALIGNMENT_UNSUPPORTED","mode","Exact native enum names required.");
                // C# flow analysis does not infer that Fault always throws.
                Enum.TryParse<TextHorizontalMode>(request.HorizontalMode,out h);Enum.TryParse<TextVerticalMode>(request.VerticalMode,out v);
                trace?.Invoke(request.Id+":before-new-text");
                DBText text=request.EntityKind=="Attribute"?new AttributeReference():new DBText();
                trace?.Invoke(request.Id+":before-set-defaults");
                text.SetDatabaseDefaults(scratch);
                trace?.Invoke(request.Id+":after-set-defaults");
                // Native alignment setters need a database-resident object/style context.
                // The temporary entity belongs only to the disposable measurement database.
                trace?.Invoke(request.Id+":before-append");
                if(text is AttributeReference attribute)
                {
                    // ATTRIB must be owned by an INSERT, never directly by model space.
                    var blocks=(BlockTable)tr.GetObject(scratch.BlockTableId,OpenMode.ForWrite);
                    var ownerDefinition=new BlockTableRecord{Name="*U"};blocks.Add(ownerDefinition);tr.AddNewlyCreatedDBObject(ownerDefinition,true);
                    var ownerReference=new BlockReference(Point3d.Origin,ownerDefinition.ObjectId);model.AppendEntity(ownerReference);tr.AddNewlyCreatedDBObject(ownerReference,true);
                    attribute.Tag="MEASUREMENT"; // Scratch-only identity, no engineering value.
                    ownerReference.AttributeCollection.AppendAttribute(attribute);tr.AddNewlyCreatedDBObject(attribute,true);
                }
                else {model.AppendEntity(text);tr.AddNewlyCreatedDBObject(text,true);}
                trace?.Invoke(request.Id+":after-append");
                trace?.Invoke(request.Id+":text-resident");
                text.TextStyleId=styleId;text.TextString=request.TextDecoded;
                text.Height=request.HeightMm;text.WidthFactor=request.WidthFactor;text.Rotation=request.RotationRad;
                text.IsMirroredInX=request.MirroredX;text.IsMirroredInY=request.MirroredY;text.HorizontalMode=h;text.VerticalMode=v;
                text.Position=Point3d.Origin;
                if(h!=TextHorizontalMode.TextLeft||v!=TextVerticalMode.TextBase)text.AlignmentPoint=Point3d.Origin;
                trace?.Invoke(request.Id+":before-adjust-alignment");text.AdjustAlignment(scratch);trace?.Invoke(request.Id+":after-adjust-alignment");entity=text;
            }
            else if(request.EntityKind=="MText")
            {
                if(request.WrappingWidthMm is not double wrap||!double.IsFinite(wrap)||wrap<=0||!Enum.TryParse<AttachmentPoint>(request.Attachment,out var attachment)||!Enum.IsDefined(attachment))Fault("TEXT_REQUEST_INVALID","wrap/attachment","MText needs finite explicit wrapping width and native attachment.");
                if(request.MirroredX||request.MirroredY||request.Vertical||request.WidthFactor!=style.XScale)Fault("TEXT_STYLE_UNSUPPORTED","MText transform","Unproven MText mirror/vertical/width override requires a separate native path.");
                Enum.TryParse<AttachmentPoint>(request.Attachment,out attachment);
                var text=new MText();trace?.Invoke(request.Id+":before-set-defaults");
                text.SetDatabaseDefaults(scratch);
                trace?.Invoke(request.Id+":after-set-defaults");text.TextStyleId=styleId;text.Contents=request.TextDecoded;
                text.TextHeight=request.HeightMm;text.Width=request.WrappingWidthMm!.Value;text.Rotation=request.RotationRad;text.Attachment=attachment;
                text.Location=Point3d.Origin;trace?.Invoke(request.Id+":before-append");
                model.AppendEntity(text);trace?.Invoke(request.Id+":after-append");
                tr.AddNewlyCreatedDBObject(text,true);entity=text;
            }
            else throw new NativeAssetContractException("TEXT_ENTITY_UNSUPPORTED",request.EntityKind,"Unsupported final entity kind.");
            trace?.Invoke(request.Id+":before-geometric-extents");
            var ext=entity.GeometricExtents;
            trace?.Invoke(request.Id+":after-geometric-extents");
            var min=new[]{ext.MinPoint.X,ext.MinPoint.Y,ext.MinPoint.Z};var max=new[]{ext.MaxPoint.X,ext.MaxPoint.Y,ext.MaxPoint.Z};
            if(min.Concat(max).Any(x=>!double.IsFinite(x)))Fault("TEXT_NATIVE_METRIC_INVALID","extents","Native extents nonfinite.");
            var ink=new Bounds2V2(min[0],min[1],max[0],max[1]);
            var raw=new NativeRawTextMeasurementR03(request.EntityKind,min,max,"mm");
            var engine=$"AutoCAD/{typeof(Database).Assembly.GetName().Version}";
            var requestHash=NativeContractHash.Compute(request);
            var measured=new NativeTextMeasureResultR03("native-text-measurement-R03",request.Id,requestHash,context.Sha256,ink,coverage.HasInk,null,null,
                [coverage.FontSha256],engine,font.EncodingRuleId,raw,request.SourceIds,["TEXT_ADVANCE_UNAVAILABLE"],"");
            // Disposal destroys the entire scratch database even when its Undo is disabled.
            // Callers retain value data only; the supplied target was read without modification.
            return measured with{MeasurementSha256=NativeContractHash.Compute(measured)};
        }
        catch(Autodesk.AutoCAD.Runtime.Exception ex)
        {throw new NativeAssetContractException("TEXT_NATIVE_MEASUREMENT_FAILED",request.Id,ex.ErrorStatus+": "+ex.Message);}
        finally {HostApplicationServices.WorkingDatabase=previous;scratch?.Dispose();}
    }
}
