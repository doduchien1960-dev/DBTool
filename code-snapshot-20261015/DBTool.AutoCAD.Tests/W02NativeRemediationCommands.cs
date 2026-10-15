using System.Text.Json;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Template;
namespace DBTool.AutoCAD.Tests;

public sealed class W02NativeRemediationCommands
{
    sealed record Check(string Field,string Expected,string Actual,string Unit,double Tolerance,bool Passed);
    [CommandMethod("DBTOOL_TEST_W02_ROUNDTRIP",CommandFlags.Session)]
    public void Run()
    {
        var start=DateTimeOffset.UtcNow;var runId=Environment.GetEnvironmentVariable("DBTOOL_W02_RUN_ID")??throw new InvalidOperationException("Fresh DBTOOL_W02_RUN_ID required before binding actual text requests.");
        if(!Guid.TryParseExact(runId,"N",out _))throw new InvalidOperationException("DBTOOL_W02_RUN_ID must be a fresh GUID in N format.");
        var root=Environment.GetEnvironmentVariable("DBTOOL_W02_OUTPUT")??throw new InvalidOperationException("DBTOOL_W02_OUTPUT required; no implicit live output location.");
        var directory=Path.Combine(root,runId);if(Directory.Exists(directory))throw new InvalidOperationException("Stale run directory rejected: "+directory);Directory.CreateDirectory(directory);
        var checks=new List<Check>();var diagnostics=new List<string>();string? targetPath=null;string failureStatus="Blocked";
        try
        {
            string Required(string key)=>Environment.GetEnvironmentVariable(key)??throw new NativeAssetContractException("NATIVE_RUN_INPUT_MISSING",key,"Explicit run input required.");
            var path=Required("DBTOOL_W02_TEMPLATE");
            var context=JsonSerializer.Deserialize<NativeAssetContextR03>(File.ReadAllText(Required("DBTOOL_W02_CONTEXT")))??throw new InvalidOperationException("Invalid native context JSON.");
            var requests=JsonSerializer.Deserialize<NativeTextMeasureRequestR03[]>(File.ReadAllText(Required("DBTOOL_W02_TEXT_REQUESTS")))??throw new InvalidOperationException("Invalid actual text request JSON.");
            if(requests.Length==0)throw new NativeAssetContractException("TEXT_DRAFT_MISSING","requests","Actual formatted text requests required.");
            bool Active(string key)=>Required(key) switch{"1"=>true,"0"=>false,_=>throw new NativeAssetContractException("NATIVE_RUN_INPUT_INVALID",key,"Explicit 0 or 1 required; no inactive default.")};
            bool catchActive=Active("DBTOOL_W02_REINFORCED_CATCH");
            bool dowelActive=Active("DBTOOL_W02_DOWEL");
            var roles=NativeActiveFeaturePlanR03.Requirements(catchActive,dowelActive);
            var read=new TemplateManifestReader().Read(new(path,context.BundleSha256,roles,"VHG-Template.dwg",context.Units));
            if(!read.Success||read.Value is null)throw new InvalidOperationException(string.Join(";",read.Diagnostics.Select(d=>d.Code+":"+d.Message)));
            var manifest=read.Value;context.Validate(manifest);
            var validation=new TemplateContractValidator().ValidateWithNativeContext(manifest,roles,context);
            if(!validation.Success)throw new InvalidOperationException(string.Join(";",validation.Diagnostics.Select(d=>d.Code+":"+d.Message)));
            var bundle=TemplateAssetsV2Converter.ConvertToAssetsBundle(manifest,nativeContext:context);
            var expected=new Dictionary<string,string>(StringComparer.Ordinal);
            var measurementResults=new List<NativeTextMeasureResultR03>();
            using(var source=new Database(false,true))
            using(var target=new Database(true,true))
            {
                source.ReadDwgFile(path,FileOpenMode.OpenForReadAndAllShare,true,string.Empty);target.Insunits=UnitsValue.Millimeters;
                TemplateAssetImporter.Import(source,target,manifest,context);
                var previous=HostApplicationServices.WorkingDatabase;
                try
                {
                    HostApplicationServices.WorkingDatabase=target;
                    using(var tr=target.TransactionManager.StartTransaction())
                    {
                    var table=(BlockTable)tr.GetObject(target.BlockTableId,OpenMode.ForRead);
                    var model=(BlockTableRecord)tr.GetObject(table[BlockTableRecord.ModelSpace],OpenMode.ForWrite);
                    foreach(var role in roles.Where(r=>r.Kind==AssetKind.Block))
                    {
                        var definition=bundle.Assets.definitions.Single(x=>x.role==role.Role);
                        var insert=new BlockReference(Point3d.Origin,table[definition.name]){Layer=definition.layer_id};model.AppendEntity(insert);tr.AddNewlyCreatedDBObject(insert,true);
                        expected[role.Role+"/Name"]=definition.name;expected[role.Role+"/Position"]="0,0,0";expected[role.Role+"/Scale"]="1,1,1";expected[role.Role+"/Rotation"]="0";
                        var native=(BlockTableRecord)tr.GetObject(insert.BlockTableRecord,OpenMode.ForRead);
                        foreach(ObjectId id in native)
                        {
                            if(tr.GetObject(id,OpenMode.ForRead) is not AttributeDefinition att||att.Constant)continue;
                            var value=definition.attributes.Single(x=>x.tag==att.Tag);
                            var draft=requests.SingleOrDefault(x=>x.Id==role.Role+"/"+att.Tag)??throw new NativeAssetContractException("TEXT_DRAFT_MISSING",role.Role+"/"+att.Tag,"Actual formatted ATTDEF request required.");
                            var attribute=new AttributeReference();attribute.SetAttributeFromBlock(att,insert.BlockTransform);attribute.TextString=draft.TextDecoded;
                            insert.AttributeCollection.AppendAttribute(attribute);tr.AddNewlyCreatedDBObject(attribute,true);attribute.AdjustAlignment(target);
                            var prefix=role.Role+"/"+att.Tag+"/";
                            expected[prefix+"Text"]=draft.TextDecoded;expected[prefix+"Height"]=F(value.height_mm);expected[prefix+"WidthFactor"]=F(value.width_factor);
                            expected[prefix+"Rotation"]=F(value.rotation_rad);expected[prefix+"HorizontalMode"]=value.horizontal_mode;expected[prefix+"VerticalMode"]=value.vertical_mode;
                            expected[prefix+"Style"]=value.style_id;expected[prefix+"Position"]=P(value.insertion.x_mm,value.insertion.y_mm,value.insertion.z_mm);
                            expected[prefix+"Alignment"]=P(value.alignment.x_mm,value.alignment.y_mm,value.alignment.z_mm);
                        }
                    }
                    tr.Commit();
                    }
                    var provider=new NativeWholeTextMeasurementR03(target);
                    foreach(var request in requests)
                    {
                        if(request.RunId!=runId)throw new NativeAssetContractException("TEXT_RUN_MISMATCH",request.Id,"Requests must bind this fresh run ID supplied through DBTOOL_W02_RUN_ID.");
                        measurementResults.Add(provider.Measure(context,request));
                    }
                }
                finally{HostApplicationServices.WorkingDatabase=previous;}
                targetPath=Path.Combine(directory,"GeneratedTarget.dwg");target.SaveAs(targetPath,DwgVersion.Current);
            }
            // Source and target are closed before opening the actual generated target bytes.
            using(var reopened=new Database(false,true))
            {
                reopened.ReadDwgFile(targetPath,FileOpenMode.OpenForReadAndAllShare,true,string.Empty);
                using var tr=reopened.TransactionManager.StartTransaction();
                var actual=Readback(tr,reopened,manifest);
                foreach(var key in expected.Keys.Union(actual.Keys,StringComparer.Ordinal))
                {expected.TryGetValue(key,out var wanted);actual.TryGetValue(key,out var value);checks.Add(new(key,wanted??"<absent>",value??"<absent>","native-field",0,wanted is not null&&value is not null&&wanted==value));}
                foreach(var asset in manifest.Assets.Where(a=>a.AssetKind!=AssetKind.Hatch))
                {
                    var actualSignature=NativeAssetSignatures.Find(reopened,tr,asset.AssetKind,asset.Name);
                    foreach(var key in asset.Signature!.Properties.Keys.Union(actualSignature?.Properties.Keys??[],StringComparer.Ordinal))
                    {asset.Signature.Properties.TryGetValue(key,out var wanted);string? value=null;actualSignature?.Properties.TryGetValue(key,out value);checks.Add(new(asset.Name+"/"+key,wanted??"<absent>",value??"<absent>","native-field",0,wanted is not null&&value is not null&&wanted==value));}
                }
            }
            File.WriteAllText(Path.Combine(directory,"whole-text-measurements.json"),JsonSerializer.Serialize(measurementResults,new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(System.Exception ex){failureStatus=ex is NativeAssetContractException?"Blocked":"Failed";diagnostics.Add(ex is NativeAssetContractException fault?fault.Code+":"+fault.Field+":"+fault.Message:ex.ToString());}
        var passed=checks.Count>0&&checks.All(x=>x.Passed)&&diagnostics.Count==0&&targetPath is not null&&File.Exists(targetPath);
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{RunId=runId,StartedUtc=start,EndedUtc=DateTimeOffset.UtcNow,Passed=passed,Status=passed?"Passed":checks.Any(x=>!x.Passed)?"Failed":failureStatus,ActualDWGPath=targetPath,TargetSha256=targetPath is not null&&File.Exists(targetPath)?NativeContractHash.File(targetPath):null,DllSha256=NativeContractHash.File(typeof(W02NativeRemediationCommands).Assembly.Location),Checks=checks,Diagnostics=diagnostics},new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(directory,"complete.sentinel"),runId);
        // complete.sentinel means execution ended; only result.json Status/Checks may authorize a predicate.
        // Do not throw an uncaught managed exception across the native command boundary.
    }
    static string F(double n)=>n.ToString("R",CultureInfo.InvariantCulture);
    static string P(double x,double y,double z)=>$"{F(x)},{F(y)},{F(z)}";
    static Dictionary<string,string> Readback(Transaction tr,Database database,TemplateManifest manifest)
    {
        var fields=new Dictionary<string,string>(StringComparer.Ordinal);
        var model=(BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(database),OpenMode.ForRead);
        foreach(ObjectId id in model)
        {
            if(tr.GetObject(id,OpenMode.ForRead) is not BlockReference insert)throw new NativeAssetContractException("NATIVE_READBACK_UNEXPECTED_ENTITY",id.Handle.ToString(),"Generated roundtrip target must contain exactly its declared block instances.");
            var name=((BlockTableRecord)tr.GetObject(insert.BlockTableRecord,OpenMode.ForRead)).Name;
            var role=manifest.Assets.Single(x=>x.AssetKind==AssetKind.Block&&x.Name==name).Role;
            fields.Add(role+"/Name",name);fields.Add(role+"/Position",P(insert.Position.X,insert.Position.Y,insert.Position.Z));fields.Add(role+"/Scale",P(insert.ScaleFactors.X,insert.ScaleFactors.Y,insert.ScaleFactors.Z));fields.Add(role+"/Rotation",F(insert.Rotation));
            foreach(ObjectId aid in insert.AttributeCollection)
            {
                var a=(AttributeReference)tr.GetObject(aid,OpenMode.ForRead);var prefix=role+"/"+a.Tag+"/";
                fields.Add(prefix+"Text",a.TextString);fields.Add(prefix+"Height",F(a.Height));fields.Add(prefix+"WidthFactor",F(a.WidthFactor));fields.Add(prefix+"Rotation",F(a.Rotation));
                fields.Add(prefix+"HorizontalMode",a.HorizontalMode.ToString());fields.Add(prefix+"VerticalMode",a.VerticalMode.ToString());fields.Add(prefix+"Style",((TextStyleTableRecord)tr.GetObject(a.TextStyleId,OpenMode.ForRead)).Name);
                fields.Add(prefix+"Position",P(a.Position.X,a.Position.Y,a.Position.Z));fields.Add(prefix+"Alignment",P(a.AlignmentPoint.X,a.AlignmentPoint.Y,a.AlignmentPoint.Z));
            }
        }
        return fields;
    }
}


