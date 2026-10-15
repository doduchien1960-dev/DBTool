using System.Collections.Immutable;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Template;
using DBTool.Domain.StageContractsV2;
namespace DBTool.AutoCAD.Tests;
/// <summary>Synthetic native oracle only. It grants no approval to a production source/font/unit contract.</summary>
public sealed class L02NativeWholeTextCommands
{
    [CommandMethod("DBTOOL_TEST_L02_WHOLETEXT",CommandFlags.Session)]
    public void Run()
    {
        var root=Environment.GetEnvironmentVariable("DBTOOL_W02_OUTPUT")??throw new InvalidOperationException("Scratch output required.");
        var run=Environment.GetEnvironmentVariable("DBTOOL_W02_RUN_ID")??throw new InvalidOperationException("Fresh run ID required.");
        var dir=Path.Combine(root,"wholetext-"+run);if(Directory.Exists(dir))throw new InvalidOperationException("Stale run.");Directory.CreateDirectory(dir);
        var font=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"arial.ttf");var fontHash=NativeContractHash.File(font);
        var targetPath=Path.Combine(dir,"SyntheticGeneratedTextTarget.dwg");var sourcePath=Path.Combine(dir,"SyntheticStyleSource.dwg");
        var oracleHandles=new List<string>();
        var previous=HostApplicationServices.WorkingDatabase;var results=new List<object>();var requests=new List<NativeTextMeasureRequestR03>();var measured=new List<NativeTextMeasureResultR03>();
        try
        {
            using(var target=new Database(true,true))
            {
                target.Insunits=UnitsValue.Millimeters;HostApplicationServices.WorkingDatabase=target;string styleHash;
                using(var tr=target.TransactionManager.StartTransaction())
                {
                    var table=(TextStyleTable)tr.GetObject(target.TextStyleTableId,OpenMode.ForWrite);var style=new TextStyleTableRecord{Name="L02SyntheticArial",FileName=font,XScale=1};table.Add(style);tr.AddNewlyCreatedDBObject(style,true);
                    styleHash=NativeAssetSignatures.Read(tr,style).Hash;tr.Commit();
                }
                target.SaveAs(sourcePath,DwgVersion.Current);var bundleHash=NativeContractHash.File(sourcePath);
                var approval=new SourceV2("SYNTHETIC-AUTHORITY","Dwg","SyntheticStyleSource.dwg",bundleHash,null,null,null,null,"Synthetic measurement fixture only; no production authority",[],null);
                var fontSource=new SourceV2("ACTUAL-FONT-BYTES","File","arial.ttf",fontHash,null,null,null,null,"Actual installed Arial bytes",[],null);
                var sources=ImmutableDictionary<string,SourceV2>.Empty.Add(approval.id,approval).Add(fontSource.id,fontSource);
                var styleContext=new NativeFontContextR03("L02SyntheticArial",styleHash,font,fontHash,null,null,"SYNTHETIC-ASCII","SYNTHETIC-IDENTITY","AutoCAD2026-synthetic-oracle",false,true,[approval.id,fontSource.id]);
                var context=new NativeAssetContextR03("asset-context-R03",bundleHash,"Millimeters",new(bundleHash,"mm","mm",1,1,[approval.id],"Synthetic test fixture only",DateTimeOffset.UtcNow),[styleContext],[],ImmutableDictionary<string,string>.Empty,sources);
                requests.Add(new("left",""+run,context.Sha256,"AVATAR WI 12345","AVATAR WI 12345","DBText",styleContext.StyleId,fontHash,null,10,0.8,0,"TextLeft","TextBase",null,false,false,false,null,[approval.id,fontSource.id]));
                requests.Add(new("center-rotated",run,context.Sha256,"Actual schedule 123.456 kg","Actual schedule 123.456 kg","DBText",styleContext.StyleId,fontHash,null,17,1.2,0.37,"TextCenter",nameof(TextVerticalMode.TextVerticalMid),null,true,false,false,null,[approval.id,fontSource.id]));
                requests.Add(requests[0] with{Id="unicode",TextRaw="THỐNG KÊ THÉP — 123,456 kg",TextDecoded="THỐNG KÊ THÉP — 123,456 kg",HeightMm=9.75,WidthFactor=0.8});
                requests.Add(requests[0] with{Id="attribute",EntityKind="Attribute",TextRaw="C3A 12Ø14",TextDecoded="C3A 12Ø14",HeightMm=8.5});
                requests.Add(requests[0] with{Id="mtext-wrapped",EntityKind="MText",TextRaw="Actual wrapped text 123 456 789",TextDecoded="Actual wrapped text 123 456 789",WidthFactor=1,HeightMm=11.25,WrappingWidthMm=75,Attachment="TopLeft",RotationRad=0.19});
                var provider=new NativeWholeTextMeasurementR03(target,stage=>File.AppendAllText(Path.Combine(dir,"native-progress.log"),DateTimeOffset.UtcNow.ToString("O")+" "+stage+"\n"));
                void Inventory(string label)
                {
                    using var inspect=target.TransactionManager.StartTransaction();
                    var space=(BlockTableRecord)inspect.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(target),OpenMode.ForRead);
                    var objects=new List<object>();
                    foreach(ObjectId id in space){var e=inspect.GetObject(id,OpenMode.ForRead);objects.Add(new{Handle=id.Handle.ToString(),Type=e.GetType().Name,Text=e is DBText t?t.TextString:null});}
                    File.WriteAllText(Path.Combine(dir,label+"-entities.json"),JsonSerializer.Serialize(objects,new JsonSerializerOptions{WriteIndented=true}));
                }
                Inventory("before-measure");
                foreach(var request in requests){measured.Add(provider.Measure(context,request));Inventory("after-"+request.Id);
                    using var verify=target.TransactionManager.StartTransaction();
                    var space=(BlockTableRecord)verify.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(target),OpenMode.ForRead);
                    int count=space.Cast<ObjectId>().Count();
                    results.Add(new{Field=request.Id+"/target-entity-count",Expected=0,Actual=count,Unit="entities",Tolerance=0,Passed=count==0});
                }
                void FaultOracle(string field, NativeTextMeasureRequestR03 request, string expected)
                {
                    string actual="NO_FAULT";
                    try { provider.Measure(context,request); }
                    catch(NativeAssetContractException e) { actual=e.Code; }
                    results.Add(new{Field=field,Expected=expected,Actual=actual,Unit="diagnostic",Tolerance=0,Passed=actual==expected});
                }
                FaultOracle("legacy-diameter-escape-no-false-coverage",requests[0] with{TextRaw="%%c14",TextDecoded="%%c14"},"TEXT_FORMAT_COVERAGE_UNVERIFIED");
                FaultOracle("native-field-no-false-coverage",requests[0] with{TextRaw="%<expression>%",TextDecoded="%<expression>%"},"TEXT_FORMAT_COVERAGE_UNVERIFIED");
                FaultOracle("invalid-alignment-no-native-crash",requests[0] with{VerticalMode="TextVertMid"},"TEXT_ALIGNMENT_UNSUPPORTED");
                FaultOracle("raw-decoded-identity",requests[0] with{TextRaw="DIFFERENT RAW SOURCE"},"TEXT_ENCODING_UNRESOLVED");
                string absent=char.ConvertFromUtf32(0x10ffff);
                FaultOracle("missing-glyph-no-fallback",requests[0] with{TextRaw=absent,TextDecoded=absent},"FONT_GLYPH_MISSING");
                // Oracle entities are created independently from the request, then saved/reopened.
                using(var tr=target.TransactionManager.StartTransaction())
                {
                    var styles=(TextStyleTable)tr.GetObject(target.TextStyleTableId,OpenMode.ForRead);var model=(BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(target),OpenMode.ForWrite);
                    foreach(var request in requests)
                    {
                        if(request.EntityKind=="MText")
                        {
                            var multi=new MText();multi.SetDatabaseDefaults(target);model.AppendEntity(multi);tr.AddNewlyCreatedDBObject(multi,true);
                            multi.TextStyleId=styles[request.StyleId];multi.Contents=request.TextDecoded;multi.TextHeight=request.HeightMm;
                            multi.Width=request.WrappingWidthMm!.Value;multi.Rotation=request.RotationRad;multi.Attachment=Enum.Parse<AttachmentPoint>(request.Attachment!);multi.Location=Point3d.Origin;oracleHandles.Add(multi.Handle.ToString());
                            continue;
                        }
                        DBText text=request.EntityKind=="Attribute"?new AttributeReference():new DBText();text.SetDatabaseDefaults(target);
                        if(text is AttributeReference attribute)
                        {
                            var blocks=(BlockTable)tr.GetObject(target.BlockTableId,OpenMode.ForWrite);
                            var block=new BlockTableRecord{Name="*U"};blocks.Add(block);tr.AddNewlyCreatedDBObject(block,true);
                            var insert=new BlockReference(Point3d.Origin,block.ObjectId);model.AppendEntity(insert);tr.AddNewlyCreatedDBObject(insert,true);
                            attribute.Tag="ORACLE";insert.AttributeCollection.AppendAttribute(attribute);tr.AddNewlyCreatedDBObject(attribute,true);
                        }
                        else{model.AppendEntity(text);tr.AddNewlyCreatedDBObject(text,true);}
                        oracleHandles.Add(text.Handle.ToString());
                        text.TextString=request.TextDecoded;text.TextStyleId=styles[request.StyleId];text.Height=request.HeightMm;text.WidthFactor=request.WidthFactor;
                        text.Rotation=request.RotationRad;text.HorizontalMode=Enum.Parse<TextHorizontalMode>(request.HorizontalMode);text.VerticalMode=Enum.Parse<TextVerticalMode>(request.VerticalMode);
                        text.IsMirroredInX=request.MirroredX;text.IsMirroredInY=request.MirroredY;text.Position=Point3d.Origin;
                        if(text.HorizontalMode!=TextHorizontalMode.TextLeft||text.VerticalMode!=TextVerticalMode.TextBase)text.AlignmentPoint=Point3d.Origin;
                        text.AdjustAlignment(target);
                    }
                    tr.Commit();
                }
                target.SaveAs(targetPath,DwgVersion.Current);
                File.WriteAllText(Path.Combine(dir,"context.json"),JsonSerializer.Serialize(context,new JsonSerializerOptions{WriteIndented=true}));
                File.WriteAllText(Path.Combine(dir,"oracle-handles.json"),JsonSerializer.Serialize(oracleHandles));
                File.WriteAllText(Path.Combine(dir,"requests.json"),JsonSerializer.Serialize(requests,new JsonSerializerOptions{WriteIndented=true}));
            }
            using(var reopened=new Database(false,true))
            {
                reopened.ReadDwgFile(targetPath,FileOpenMode.OpenForReadAndAllShare,true,string.Empty);HostApplicationServices.WorkingDatabase=reopened;
                using var tr=reopened.TransactionManager.StartTransaction();var model=(BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(reopened),OpenMode.ForRead);int index=0;
                foreach(string handle in oracleHandles)
                {
                    var id=reopened.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0);
                    var text=(Entity)tr.GetObject(id,OpenMode.ForRead);var ext=text.GeometricExtents;var measure=measured[index++];
                    var expected=new[]{ext.MinPoint.X,ext.MinPoint.Y,ext.MaxPoint.X,ext.MaxPoint.Y};var actual=new[]{measure.Ink.min_x_mm,measure.Ink.min_y_mm,measure.Ink.max_x_mm,measure.Ink.max_y_mm};
                    for(var i=0;i<expected.Length;i++)results.Add(new{Field=measure.RequestId+"/ink/"+new[]{"min_x","min_y","max_x","max_y"}[i],Expected=expected[i],Actual=actual[i],Unit="mm",Tolerance=1e-8,Passed=Math.Abs(expected[i]-actual[i])<=1e-8});
                }
            }
            File.WriteAllText(Path.Combine(dir,"measurements.json"),JsonSerializer.Serialize(measured,new JsonSerializerOptions{WriteIndented=true}));
            File.WriteAllText(Path.Combine(dir,"oracles.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
            var passed=results.Count==30&&results.All(x=>(bool)x.GetType().GetProperty("Passed")!.GetValue(x)!);
            File.WriteAllText(Path.Combine(dir,"result.json"),JsonSerializer.Serialize(new{RunId=run,Passed=passed,Scope="Synthetic whole-text native oracle only; production G08/G09 and fullpipeline remain open",TargetPath=targetPath,TargetSha256=NativeContractHash.File(targetPath),FontPath=font,FontSha256=fontHash,DllSha256=NativeContractHash.File(typeof(L02NativeWholeTextCommands).Assembly.Location)},new JsonSerializerOptions{WriteIndented=true}));
            if(!passed)throw new InvalidOperationException("Synthetic whole text native oracle failed.");File.WriteAllText(Path.Combine(dir,"complete.sentinel"),run);
        }
        catch(System.Exception error)
        {
            // Never let an unmanaged command boundary hide a managed failure.
            File.WriteAllText(Path.Combine(dir,"failure.json"),JsonSerializer.Serialize(new{Type=error.GetType().FullName,error.Message,error.StackTrace,Passed=false},new JsonSerializerOptions{WriteIndented=true}));
        }
        finally{HostApplicationServices.WorkingDatabase=previous;}
    }
}
