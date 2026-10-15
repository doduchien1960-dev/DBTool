using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using DBTool.Application;
using DBTool.Drawing;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using DBTool.AutoCAD.Template;

namespace DBTool.AutoCAD.Tests;

/// <summary>Read actual host properties in one transaction; handles locate objects, never define semantics.</summary>
public sealed class SemanticSnapshotReader
{
    /// <summary>Legacy fixture inspection only. This type cannot enter the R04 acceptance gate.</summary>
    public SemanticSnapshot ReadArchived(Document document, GenerationArtifact artifact, DrawingPackage package)
    {
        void Progress(string step)
        {
            var run=Environment.GetEnvironmentVariable("DBTOOL_REGRESSION_RUN");
            if(run is not null) File.AppendAllText(Path.Combine(run,"regression-progress.log"),step+"\n");
        }
        using var tr = document.Database.TransactionManager.StartTransaction();
        var entities = new List<SemanticEntity>();
        foreach (var item in artifact.EntityMap.Entities)
        {
            var primitive = package.Primitives.Single(p => p.SemanticId == item.SemanticId);
            var handles = new[] { item.Handle }.Concat(item.ChildHandles);
            var index = 0;
            foreach (var handle in handles)
            {
                var id = document.Database.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0);
                if (tr.GetObject(id, OpenMode.ForRead) is not Entity entity) throw new InvalidOperationException("SNAPSHOT_ENTITY_MISSING");
                // ATT children belong to their block; they are not physical bars/entities in the snapshot.
                if (entity is AttributeReference) continue;
                Progress(item.SemanticId+" "+entity.GetType().Name+" properties");
                var fields = new Dictionary<string,string>(StringComparer.Ordinal) { ["Layer"]=entity.Layer, ["Linetype"]=entity.Linetype, ["ColorIndex"]=entity.ColorIndex.ToString(), ["Lineweight"]=entity.LineWeight.ToString(), ["Role"]=primitive.Role };
                var numbers = new Dictionary<string,double>(StringComparer.Ordinal);
                void Point(string key, Point3d point) { numbers[key+".X"]=point.X; numbers[key+".Y"]=point.Y; numbers[key+".Z"]=point.Z; }
                void TextStyle(ObjectId styleId, string prefix)
                {
                    var style = (TextStyleTableRecord)tr.GetObject(styleId, OpenMode.ForRead);
                    fields[prefix+"Style"] = style.Name; fields[prefix+"Font"] = style.FileName; fields[prefix+"BigFont"] = style.BigFontFileName;
                }
                switch (entity)
                {
                    case Line line: Point("Start",line.StartPoint); Point("End",line.EndPoint); break;
                    case Arc arc: Point("Center",arc.Center); numbers["Radius"]=arc.Radius; numbers["StartAngle"]=arc.StartAngle; numbers["EndAngle"]=arc.EndAngle; break;
                    case Circle circle: Point("Center",circle.Center); numbers["Radius"]=circle.Radius; break;
                    case Polyline poly:
                        fields["Closed"]=poly.Closed.ToString();
                        for(var n=0;n<poly.NumberOfVertices;n++) { Point($"Vertex{n}",poly.GetPoint3dAt(n)); numbers[$"Bulge{n}"]=poly.GetBulgeAt(n); }
                        break;
                    case BlockReference block:
                        fields["BlockName"]=((BlockTableRecord)tr.GetObject(block.BlockTableRecord,OpenMode.ForRead)).Name;
                        fields["BlockRole"]=(primitive as BlockPrimitive)?.BlockRole ?? "";
                        var transform=block.BlockTransform.ToArray();
                        for(var n=0;n<transform.Length;n++) numbers[$"Transform{n}"]=transform[n];
                        foreach(ObjectId attId in block.AttributeCollection)
                        {
                            var att=(AttributeReference)tr.GetObject(attId,OpenMode.ForRead);
                            fields["ATT."+att.Tag]=att.TextString; TextStyle(att.TextStyleId,"ATT."+att.Tag+".");
                        }
                        break;
                    case DBText text:
                        fields["Text"]=text.TextString; TextStyle(text.TextStyleId,""); Point("Position",text.Position); numbers["Height"]=text.Height; numbers["Rotation"]=text.Rotation; break;
                    case Dimension dim:
                        numbers["MeasurementMm"]=dim.Measurement; fields["DimensionText"]=dim.DimensionText;
                        fields["DimensionStyle"]=((DimStyleTableRecord)tr.GetObject(dim.DimensionStyle,OpenMode.ForRead)).Name;
                        Point("TextPosition",dim.TextPosition); break;
                    case Hatch hatch:
                        Progress("Hatch pattern"); fields["HatchPattern"]=hatch.PatternName;
                        Progress("Hatch scale"); numbers["HatchScale"]=hatch.PatternScale;
                        Progress("Hatch loop count");
                        if((Path.GetFileName(Environment.ProcessPath) ?? "").StartsWith("accoreconsole",StringComparison.OrdinalIgnoreCase))
                        {
                            // Native GetLoopAt(0) reproducibly crashes this installed 2026.1.1 Core Console.
                            // Actual boundary child polylines are still read below. This is explicitly incomplete.
                            fields["HatchLoopReadback"]="PendingGuiHost; CoreConsole native access violation";
                            break;
                        }
                        for(var n=0;n<hatch.NumberOfLoops;n++)
                        {
                            Progress("Hatch loop "+n);
                            var loop=hatch.GetLoopAt(n); fields[$"Loop{n}.Type"]=loop.LoopType.ToString();
                            if(!loop.IsPolyline) throw new InvalidOperationException("SNAPSHOT_HATCH_CURVE_UNSUPPORTED");
                            var vertex=0;
                            foreach(BulgeVertex v in loop.Polyline) { numbers[$"Loop{n}.Vertex{vertex}.X"]=v.Vertex.X; numbers[$"Loop{n}.Vertex{vertex}.Y"]=v.Vertex.Y; numbers[$"Loop{n}.Vertex{vertex++}.Bulge"]=v.Bulge; }
                        }
                        break;
                    default: throw new InvalidOperationException("SNAPSHOT_ENTITY_TYPE_UNSUPPORTED: "+entity.GetType().Name);
                }
                Progress(item.SemanticId+" bounds");
                var bounds=entity.GeometricExtents; Point("Bounds.Min",bounds.MinPoint); Point("Bounds.Max",bounds.MaxPoint);
                entities.Add(new(item.SemanticId+(index++ == 0 ? "" : $"/child/{index-1}"),entity.GetType().Name,primitive.ComponentId,null,fields,numbers,handle));
            }
        }
        tr.Commit();
        return new(artifact.DocumentToken,artifact.GenerationId,entities);
    }

    /// <summary>Read the exact saved database, with externally locked coverage, units, frames and source bindings.</summary>
    public CanonicalPhysicalSnapshot ReadCanonical(GenerationArtifact artifact,string expectedContractJson,string savedDwgPath,string runId)
    {
        var before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(savedDwgPath)));
        using var database=new Database(false,true);
        database.ReadDwgFile(savedDwgPath,FileOpenMode.OpenForReadAndAllShare,false,"");database.CloseInput(true);
        var previous=HostApplicationServices.WorkingDatabase;
        try {
            HostApplicationServices.WorkingDatabase=database;
            var result=ReadCanonicalDatabase(database,artifact,expectedContractJson,savedDwgPath,runId);
            if(before!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(savedDwgPath))))throw new InvalidOperationException("SNAPSHOT_DWG_CHANGED_DURING_CAPTURE");
            return result;
        }finally{HostApplicationServices.WorkingDatabase=previous;}
    }

    private CanonicalPhysicalSnapshot ReadCanonicalDatabase(Database database,GenerationArtifact artifact,string expectedContractJson,string savedDwgPath,string runId)
    {
        var contract=CanonicalPhysicalValidator.ValidateContract(expectedContractJson);
        var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(savedDwgPath)));
        var unitBindings=contract.GetProperty("unit_bindings").EnumerateArray().Where(b=>string.Equals(b.GetProperty("source_dwg_sha256").GetString(),hash,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(unitBindings.Length!=1)throw new InvalidOperationException((contract.TryGetProperty("generated_output_authorization",out _)?"SNAPSHOT_GENERATED_OUTPUT_PROOF_MISSING":"SNAPSHOT_UNIT_UNRESOLVED")+": saved DWG "+hash+" requires verified normalized-mm write/save/reopen proof linked to approved source unit, frame and writer revision; metadata and input hashes are insufficient");
        var scale=unitBindings[0].GetProperty("scale_to_mm").GetDouble();
        using var tr=database.TransactionManager.StartTransaction();
        var semanticByHandle=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in artifact.EntityMap.Entities) {
            semanticByHandle.Add(item.Handle,item.SemanticId);var child=0;
            foreach(var handle in item.ChildHandles) {
                var id=database.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0);
                if(tr.GetObject(id,OpenMode.ForRead) is AttributeReference)continue;
                semanticByHandle.Add(handle,item.SemanticId+"/child/"+(child++));
            }
        }
        var records=new List<NativePhysicalEntityReadback>();
        foreach(var item in semanticByHandle.OrderBy(p=>p.Value,StringComparer.Ordinal)) {
            if(!contract.GetProperty("entity_bindings").TryGetProperty(item.Value,out var binding))throw new InvalidOperationException("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED: native semantic binding "+item.Value);
            var native=(Entity)tr.GetObject(database.GetObjectId(false,new Handle(Convert.ToInt64(item.Key,16)),0),OpenMode.ForRead);
            var owner=(BlockTableRecord)tr.GetObject(native.OwnerId,OpenMode.ForRead);
            if(!owner.IsLayout)throw new InvalidOperationException("SNAPSHOT_FRAME_UNRESOLVED: generated entity owner is not a layout");
            var layout=(Layout)tr.GetObject(owner.LayoutId,OpenMode.ForRead);var actualSpace=layout.ModelType?"World":"Paper";
            var frameId=binding.GetProperty("frame_id").GetString()!;
            var frame=contract.GetProperty("frames").EnumerateArray().Single(f=>f.GetProperty("id").GetString()==frameId);
            records.Add(ReadEntity(native,item.Value,binding,actualSpace,frame,scale,tr,semanticByHandle,new HashSet<string>(StringComparer.OrdinalIgnoreCase)));
        }
        tr.Commit();
        return NativePhysicalCaptureAdapter.Normalize(records,expectedContractJson,hash,runId);
    }

    private static NativePhysicalEntityReadback ReadEntity(Entity entity,string id,JsonElement binding,string space,JsonElement frame,double scale,Transaction tr,IReadOnlyDictionary<string,string> semanticByHandle,HashSet<string> blockStack)
    {
        var read=new NativePhysicalEntityReadback(id,entity.GetType().Name,space,frame.GetProperty("id").GetString()!,JsonNode.Parse(binding.GetProperty("source_ids").GetRawText())!);
        var payload=read.Payload;var placement=read.Placement;
        double[] V(JsonElement value)=>value.EnumerateArray().Select(v=>v.GetDouble()).ToArray();
        var origin=V(frame.GetProperty("origin_xyz_mm"));var basis=new[]{V(frame.GetProperty("basis_u_xyz")),V(frame.GetProperty("basis_v_xyz")),V(frame.GetProperty("normal_xyz"))};
        JsonObject Point(string raw,string target,Point3d point,bool vector=false) {
            var input=new[]{point.X,point.Y,point.Z};var delta=vector?input:input.Zip(origin,(x,o)=>x*scale-o).ToArray();var output=basis.Select(b=>b.Zip(delta,(a,v)=>a*v).Sum()).ToArray();
            var key=vector?"xyz":"xyz_mm";var result=new JsonObject{[key]=new JsonArray(output.Select(v=>(JsonNode?)JsonValue.Create(v)).ToArray())};
            for(var i=0;i<3;i++)read.Consume(raw+"."+new[]{"X","Y","Z"}[i],JsonValue.Create(input[i]),target+"."+key+"["+i+"]",vector?"VectorToFrame":"PointToFrame");return result;
        }
        JsonObject Normal(string raw,string target,Vector3d vector)=>Point(raw,target,new Point3d(vector.X,vector.Y,vector.Z),true);
        double Number(string raw,string target,double value,bool length=false) {read.Consume(raw,JsonValue.Create(value),target,length?"ScaleToMm":"Identity");return length?value*scale:value;}
        string Text(string raw,string target,string value){read.Consume(raw,JsonValue.Create(value),target);return value;}
        bool Bool(string raw,string target,bool value){read.Consume(raw,JsonValue.Create(value),target);return value;}
        string Required(string name)=>binding.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString())?value.GetString()!:throw new InvalidOperationException("SNAPSHOT_EXPECTED_CONTRACT_REQUIRED: "+id+"."+name);
        string Style(ObjectId styleId) {
            var style=(TextStyleTableRecord)tr.GetObject(styleId,OpenMode.ForRead);
            read.Metadata("TextStyle.Font",JsonValue.Create(style.FileName),"Native font path retained for audit; encoding must have an external reviewed binding.");
            read.Metadata("TextStyle.BigFont",JsonValue.Create(style.BigFontFileName),"Native bigfont path retained; no inferred encoding.");
            return style.Name;
        }
        read.Metadata("Handle",JsonValue.Create(entity.Handle.ToString()),"Object locator only; excluded from physical identity and retained as a metadata delta.");
        read.Metadata("Layer",JsonValue.Create(entity.Layer),"Native presentation metadata retained and compared.");
        read.Metadata("Linetype",JsonValue.Create(entity.Linetype),"Native presentation metadata retained and compared.");
        read.Metadata("ColorIndex",JsonValue.Create(entity.ColorIndex),"Native presentation metadata retained and compared.");
        read.Metadata("Lineweight",JsonValue.Create(entity.LineWeight.ToString()),"Native presentation metadata retained and compared.");
        switch(entity) {
            case Line line:
                payload["start"]=Point("StartPoint","physical_payload.start",line.StartPoint);payload["end"]=Point("EndPoint","physical_payload.end",line.EndPoint);break;
            case Circle circle:
                payload["center"]=Point("Center","physical_payload.center",circle.Center);payload["normal"]=Normal("Normal","physical_payload.normal",circle.Normal);payload["radius_mm"]=Number("Radius","physical_payload.radius_mm",circle.Radius,true);break;
            case Arc arc:
                payload["center"]=Point("Center","physical_payload.center",arc.Center);payload["normal"]=Normal("Normal","physical_payload.normal",arc.Normal);payload["radius_mm"]=Number("Radius","physical_payload.radius_mm",arc.Radius,true);
                payload["start_angle_rad"]=Number("StartAngle","physical_payload.start_angle_rad",arc.StartAngle);payload["sweep_rad"]=CanonicalPhysicalValidator.NativeArcSweep(arc.StartAngle,arc.EndAngle);read.Consume("EndAngle",JsonValue.Create(arc.EndAngle),"physical_payload.sweep_rad","ArcEndAngleToSweep");
                payload["start"]=Point("StartPoint","physical_payload.start",arc.StartPoint);payload["end"]=Point("EndPoint","physical_payload.end",arc.EndPoint);break;
            case Polyline poly:
                payload["closed"]=Bool("Closed","physical_payload.closed",poly.Closed);payload["elevation_mm"]=Number("Elevation","physical_payload.elevation_mm",poly.Elevation,true);payload["normal"]=Normal("Normal","physical_payload.normal",poly.Normal);
                var vertices=new JsonArray();for(int i=0;i<poly.NumberOfVertices;i++) {var point=Point("Vertex"+i,"physical_payload.vertices["+i+"]",poly.GetPoint3dAt(i));point["bulge"]=Number("Bulge"+i,"physical_payload.vertices["+i+"].bulge",poly.GetBulgeAt(i));point["start_width_mm"]=Number("StartWidth"+i,"physical_payload.vertices["+i+"].start_width_mm",poly.GetStartWidthAt(i),true);point["end_width_mm"]=Number("EndWidth"+i,"physical_payload.vertices["+i+"].end_width_mm",poly.GetEndWidthAt(i),true);vertices.Add(point);}payload["vertices"]=vertices;break;
            case DBText text:
                payload["text"]=new JsonObject{["raw"]=Text("TextString","physical_payload.text.raw",text.TextString),["decoded"]=text.TextString};payload["encoding_rule_id"]=Required("encoding_rule_id");payload["style_id"]=Text("TextStyle.Name","physical_payload.style_id",Style(text.TextStyleId));
                payload["height_mm"]=Number("Height","physical_payload.height_mm",text.Height,true);payload["width_factor"]=Number("WidthFactor","physical_payload.width_factor",text.WidthFactor);payload["horizontal_mode"]=Text("HorizontalMode","physical_payload.horizontal_mode",text.HorizontalMode.ToString());payload["vertical_mode"]=Text("VerticalMode","physical_payload.vertical_mode",text.VerticalMode.ToString());payload["mirror_x"]=Bool("IsMirroredInX","physical_payload.mirror_x",text.IsMirroredInX);payload["mirror_y"]=Bool("IsMirroredInY","physical_payload.mirror_y",text.IsMirroredInY);payload["normal"]=Normal("Normal","physical_payload.normal",text.Normal);
                placement["anchor"]=Point("Position","placement.anchor",text.Position);placement["alignment"]=Point("AlignmentPoint","placement.alignment",text.AlignmentPoint);placement["rotation_rad"]=Number("Rotation","placement.rotation_rad",text.Rotation);placement["anchor_semantics"]="Position";placement["anchor_rule_id"]=Required("anchor_rule_id");break;
            case MText text:
                payload["contents"]=new JsonObject{["raw"]=Text("Contents","physical_payload.contents.raw",text.Contents)};payload["encoding_rule_id"]=Required("encoding_rule_id");payload["style_id"]=Text("TextStyle.Name","physical_payload.style_id",Style(text.TextStyleId));payload["text_height_mm"]=Number("TextHeight","physical_payload.text_height_mm",text.TextHeight,true);payload["width_mm"]=Number("Width","physical_payload.width_mm",text.Width,true);payload["attachment"]=Text("Attachment","physical_payload.attachment",text.Attachment.ToString());payload["normal"]=Normal("Normal","physical_payload.normal",text.Normal);
                placement["anchor"]=Point("Location","placement.anchor",text.Location);placement["rotation_rad"]=Number("Rotation","placement.rotation_rad",text.Rotation);placement["anchor_semantics"]="Location";placement["anchor_rule_id"]=Required("anchor_rule_id");break;
            case Dimension dimension:
                payload["dimension_kind"]=entity.GetType().Name;payload["measurement_mm"]=Number("Measurement","physical_payload.measurement_mm",dimension.Measurement,true);payload["text_position"]=Point("TextPosition","physical_payload.text_position",dimension.TextPosition);payload["text_override"]=Text("DimensionText","physical_payload.text_override",dimension.DimensionText);payload["style_id"]=Text("DimensionStyle.Name","physical_payload.style_id",((DimStyleTableRecord)tr.GetObject(dimension.DimensionStyle,OpenMode.ForRead)).Name);
                var style=NativeAssetSignatures.Read(tr,tr.GetObject(dimension.DimensionStyle,OpenMode.ForRead));var overrides=NativeAssetSignatures.DimensionProperties(tr,dimension);var styleReadback=JsonSerializer.Serialize(new{style.Properties,Overrides=overrides.Properties});payload["style_hash"]=CanonicalPhysicalValidator.Hash(styleReadback);read.Consume("DimensionStyle.Readback",JsonValue.Create(styleReadback),"physical_payload.style_hash","HashUtf8");payload["normal"]=Normal("Normal","physical_payload.normal",dimension.Normal);
                var points=new JsonArray();var definition=dimension switch {RotatedDimension d=>new[]{("XLine1Point",d.XLine1Point),("XLine2Point",d.XLine2Point),("DimLinePoint",d.DimLinePoint)},AlignedDimension d=>new[]{("XLine1Point",d.XLine1Point),("XLine2Point",d.XLine2Point),("DimLinePoint",d.DimLinePoint)},_=>throw new InvalidOperationException("SNAPSHOT_NATIVE_KIND_UNMAPPED: "+dimension.GetType().Name)};
                foreach(var p in definition){var node=Point(p.Item1,"physical_payload.definition_points["+points.Count+"]",p.Item2);node["role"]=p.Item1;points.Add(node);}payload["definition_points"]=points;if(dimension is RotatedDimension rotated)payload["rotation_rad"]=Number("Rotation","physical_payload.rotation_rad",rotated.Rotation);break;
            case BlockReference block:
                if(block.IsDynamicBlock)throw new InvalidOperationException("SNAPSHOT_CAPTURE_INCOMPLETE: dynamic block state needs a complete native adapter");
                var btr=(BlockTableRecord)tr.GetObject(block.BlockTableRecord,OpenMode.ForRead);payload["asset_id"]=Text("BlockTableRecord.Name","physical_payload.asset_id",btr.Name);
                var asset=BlockContent(btr,tr,blockStack);payload["asset_content_hash"]=asset.Hash;payload["dependency_ids"]=new JsonArray(asset.Dependencies.Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());read.Consume("Block.DefinitionReadback",JsonValue.Create(asset.Json),"physical_payload.asset_content_hash","HashUtf8");
                payload["insertion"]=Point("Position","physical_payload.insertion",block.Position);payload["scale"]=new JsonObject{["xyz"]=new JsonArray(Number("ScaleFactors.X","physical_payload.scale.xyz[0]",block.ScaleFactors.X),Number("ScaleFactors.Y","physical_payload.scale.xyz[1]",block.ScaleFactors.Y),Number("ScaleFactors.Z","physical_payload.scale.xyz[2]",block.ScaleFactors.Z))};payload["rotation_rad"]=Number("Rotation","physical_payload.rotation_rad",block.Rotation);payload["normal"]=Normal("Normal","physical_payload.normal",block.Normal);
                var attributes=new JsonArray();foreach(ObjectId attId in block.AttributeCollection){var a=(AttributeReference)tr.GetObject(attId,OpenMode.ForRead);var prefix="Attribute["+attributes.Count+"]";var target="physical_payload.attributes["+attributes.Count+"]";attributes.Add(new JsonObject{["tag"]=Text(prefix+".Tag",target+".tag",a.Tag),["value"]=Text(prefix+".TextString",target+".value",a.TextString),["position"]=Point(prefix+".Position",target+".position",a.Position),["alignment"]=Point(prefix+".AlignmentPoint",target+".alignment",a.AlignmentPoint),["style_id"]=Text(prefix+".TextStyle",target+".style_id",((TextStyleTableRecord)tr.GetObject(a.TextStyleId,OpenMode.ForRead)).Name),["height_mm"]=Number(prefix+".Height",target+".height_mm",a.Height,true),["width_factor"]=Number(prefix+".WidthFactor",target+".width_factor",a.WidthFactor),["rotation_rad"]=Number(prefix+".Rotation",target+".rotation_rad",a.Rotation),["horizontal_mode"]=Text(prefix+".HorizontalMode",target+".horizontal_mode",a.HorizontalMode.ToString()),["vertical_mode"]=Text(prefix+".VerticalMode",target+".vertical_mode",a.VerticalMode.ToString()),["mirror_x"]=Bool(prefix+".IsMirroredInX",target+".mirror_x",a.IsMirroredInX),["mirror_y"]=Bool(prefix+".IsMirroredInY",target+".mirror_y",a.IsMirroredInY)});}payload["attributes"]=attributes;break;
            case Hatch hatch:
                // This installed Core Console crashes inside GetLoopAt; incomplete readback must never sign equality.
                if((Path.GetFileName(Environment.ProcessPath)??"").StartsWith("accoreconsole",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("SNAPSHOT_CAPTURE_INCOMPLETE: HatchPendingGuiHost; native GetLoopAt is unsafe in Core Console 2026.1.1");
                payload["pattern_id"]=Text("PatternName","physical_payload.pattern_id",hatch.PatternName);payload["pattern_scale"]=Number("PatternScale","physical_payload.pattern_scale",hatch.PatternScale);payload["pattern_angle_rad"]=Number("PatternAngle","physical_payload.pattern_angle_rad",hatch.PatternAngle);payload["associative"]=Bool("Associative","physical_payload.associative",hatch.Associative);
                var ownerIds=new List<string>();foreach(ObjectId ownerId in hatch.GetAssociatedObjectIds()){var handle=ownerId.Handle.ToString();if(!semanticByHandle.TryGetValue(handle,out var semantic))throw new InvalidOperationException("SNAPSHOT_SOURCE_UNRESOLVED: hatch owner "+handle);ownerIds.Add(semantic);}payload["owner_ids"]=new JsonArray(ownerIds.Distinct().Order(StringComparer.Ordinal).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
                payload["loops"]=ReadHatchLoops(hatch,read,scale);break;
            default:throw new InvalidOperationException("SNAPSHOT_NATIVE_KIND_UNMAPPED: "+entity.GetType().Name);
        }
        if(entity is not(DBText or MText)) {
            // Canonical geometry is already resolved into its declared frame, so there is no residual placement.
            placement["transform"]=new JsonArray(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
        }
        return read;
    }

    private static (string Hash,string Json,string[] Dependencies) BlockContent(BlockTableRecord block,Transaction tr,HashSet<string> stack)
    {
        if(!stack.Add(block.Name))throw new InvalidOperationException("SNAPSHOT_ASSET_DEPENDENCY_CYCLE: "+string.Join(" → ",stack)+" → "+block.Name);
        try {
            var signature=NativeAssetSignatures.ReadBlock(tr,block);
            if(!signature.Properties.ContainsKey("EntityPayloadHash"))throw new InvalidOperationException("SNAPSHOT_CAPTURE_INCOMPLETE: L02 full native resource signature required; legacy bounds-only signature cannot sign Block identity");
            var dependencies=new SortedSet<string>(StringComparer.Ordinal);
            foreach(ObjectId objectId in block)if(tr.GetObject(objectId,OpenMode.ForRead) is BlockReference nested){var child=(BlockTableRecord)tr.GetObject(nested.BlockTableRecord,OpenMode.ForRead);dependencies.Add(child.Name);foreach(var d in BlockContent(child,tr,stack).Dependencies)dependencies.Add(d);}
            var properties=JsonSerializer.Serialize(signature.Properties);
            return(CanonicalPhysicalValidator.Hash(properties),properties,dependencies.ToArray());
        }finally{stack.Remove(block.Name);}
    }

    private static JsonArray ReadHatchLoops(Hatch hatch,NativePhysicalEntityReadback read,double scale)
    {
        // Preserve native tuples even when a curve/nesting adapter is unavailable. Never substitute bounds or empty loops.
        for(int i=0;i<hatch.NumberOfLoops;i++){
            var loop=hatch.GetLoopAt(i);read.Metadata("Loop"+i+".Type",JsonValue.Create(loop.LoopType.ToString()),"Native loop role retained; analytic adapter must prove closure and nesting.");
            if(loop.IsPolyline){var n=0;foreach(BulgeVertex v in loop.Polyline){read.Raw.Add("Loop"+i+".Vertex"+n+".X",v.Vertex.X);read.Raw.Add("Loop"+i+".Vertex"+n+".Y",v.Vertex.Y);read.Raw.Add("Loop"+i+".Vertex"+(n++)+".Bulge",v.Bulge);}}
        }
        throw new InvalidOperationException("SNAPSHOT_CAPTURE_INCOMPLETE: hatch analytic curves and nesting need independent GUI-host readback; raw tuples are not a complete physical payload");
    }
}
