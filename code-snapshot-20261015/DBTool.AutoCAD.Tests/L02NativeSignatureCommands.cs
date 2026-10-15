using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DBTool.AutoCAD.Template;
namespace DBTool.AutoCAD.Tests;
public sealed class L02NativeSignatureCommands
{
    [CommandMethod("DBTOOL_TEST_L02_SIGNATURES",CommandFlags.Session)]
    public void Run()
    {
        var root=Environment.GetEnvironmentVariable("DBTOOL_W02_OUTPUT")??throw new InvalidOperationException("Explicit scratch output root required.");
        var runId=Environment.GetEnvironmentVariable("DBTOOL_W02_RUN_ID")??throw new InvalidOperationException("Fresh run ID required.");
        var dir=Path.Combine(root,"signature-"+runId);if(Directory.Exists(dir))throw new InvalidOperationException("Stale run directory.");Directory.CreateDirectory(dir);
        var template=Environment.GetEnvironmentVariable("DBTOOL_W02_TEMPLATE");
        if(template is not null)
        { using var inventoryDb=new Database(false,true);inventoryDb.ReadDwgFile(template,FileOpenMode.OpenForReadAndAllShare,true,string.Empty);using var it=inventoryDb.TransactionManager.StartTransaction();var styleTable=(TextStyleTable)it.GetObject(inventoryDb.TextStyleTableId,OpenMode.ForRead);var fontInventory=new List<object>();foreach(ObjectId sid in styleTable){var style=(TextStyleTableRecord)it.GetObject(sid,OpenMode.ForRead);fontInventory.Add(new{style.Name,style.FileName,style.BigFontFileName,style.XScale,Typeface=style.Font.TypeFace});}File.WriteAllText(Path.Combine(dir,"actual-template-font-inventory.json"),JsonSerializer.Serialize(fontInventory,new JsonSerializerOptions{WriteIndented=true}));var bt=(BlockTable)it.GetObject(inventoryDb.BlockTableId,OpenMode.ForRead);var inventory=new List<object>();foreach(ObjectId bid in bt){var record=(BlockTableRecord)it.GetObject(bid,OpenMode.ForRead);var counts=new Dictionary<string,int>();foreach(ObjectId eid in record){var kind=it.GetObject(eid,OpenMode.ForRead).GetType().Name;counts[kind]=counts.GetValueOrDefault(kind)+1;}string? hash=null;string? fault=null;if(!record.IsLayout&&!record.Name.StartsWith("*D",StringComparison.Ordinal)){try{hash=NativeAssetSignatures.ReadBlock(it,record).Hash;}catch(System.Exception ex){fault=ex.ToString();}}inventory.Add(new{Name=record.Name,record.IsLayout,Counts=counts,ContentHash=hash,Fault=fault});}File.WriteAllText(Path.Combine(dir,"actual-template-entity-inventory.json"),JsonSerializer.Serialize(inventory,new JsonSerializerOptions{WriteIndented=true})); }
        var results=new List<object>();
        using var db=new Database(true,true);
        using var tr=db.TransactionManager.StartTransaction();
        var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
        var child=new BlockTableRecord{Name="InteriorWitness"};table.Add(child);tr.AddNewlyCreatedDBObject(child,true);
        void Add(Entity e){child.AppendEntity(e);tr.AddNewlyCreatedDBObject(e,true);}
        Add(new Line(new Point3d(0,0,0),new Point3d(100,100,0)));
        var line=new Line(new Point3d(10,20,0),new Point3d(90,20,0));Add(line);
        var parent=new BlockTableRecord{Name="ParentWitness"};table.Add(parent);tr.AddNewlyCreatedDBObject(parent,true);
        var reference=new BlockReference(Point3d.Origin,child.ObjectId);parent.AppendEntity(reference);tr.AddNewlyCreatedDBObject(reference,true);
        var first=NativeAssetSignatures.ReadBlock(tr,child);var parentFirst=NativeAssetSignatures.ReadBlock(tr,parent);
        line.StartPoint=new Point3d(10,40,0);line.EndPoint=new Point3d(90,40,0);
        var moved=NativeAssetSignatures.ReadBlock(tr,child);var parentMoved=NativeAssetSignatures.ReadBlock(tr,parent);
        bool sameBounds=new[]{"Bounds/MinX","Bounds/MinY","Bounds/MaxX","Bounds/MaxY"}.All(k=>first.Properties[k]==moved.Properties[k]);
        results.Add(new{Field="InteriorY20to40/content_hash",Expected="different hash; same bounds",Actual=new{Before=first.Hash,After=moved.Hash,SameBounds=sameBounds},Unit="native-coordinate/hash",Tolerance=0,Passed=sameBounds&&first.Hash!=moved.Hash});
        results.Add(new{Field="ChildGeometry/parent_content_hash",Expected="different hash",Actual=new{Before=parentFirst.Hash,After=parentMoved.Hash},Unit="hash",Tolerance=0,Passed=parentFirst.Hash!=parentMoved.Hash});
        line.ColorIndex=3;var colored=NativeAssetSignatures.ReadBlock(tr,parent);
        results.Add(new{Field="ChildColor/parent_content_hash",Expected="different hash",Actual=new{Before=parentMoved.Hash,After=colored.Hash},Unit="hash",Tolerance=0,Passed=parentMoved.Hash!=colored.Hash});
        var hatch=new Hatch();child.AppendEntity(hatch);tr.AddNewlyCreatedDBObject(hatch,true);hatch.SetHatchPattern(HatchPatternType.PreDefined,"SOLID");
        void Loop(double min,double max,HatchLoopTypes kind)
        {hatch.AppendLoop(kind,new Point2dCollection{new(min,min),new(max,min),new(max,max),new(min,max)},new DoubleCollection{0,0,0,0});}
        Loop(0,100,HatchLoopTypes.Outermost);Loop(40,60,HatchLoopTypes.Default);hatch.EvaluateHatch(true);
        var hatchFirst=NativeAssetSignatures.EntityPayload(tr,hatch);hatch.RemoveLoopAt(1);Loop(30,70,HatchLoopTypes.Default);hatch.EvaluateHatch(true);
        var hatchChanged=NativeAssetSignatures.EntityPayload(tr,hatch);var hatchPass=hatchFirst.Hash!=hatchChanged.Hash;
        results.Add(new{Field="HatchInnerLoop/content_hash",Expected="different hash",Actual=new{Before=hatchFirst.Hash,After=hatchChanged.Hash},Unit="hash",Tolerance=0,Passed=hatchPass});        File.WriteAllText(Path.Combine(dir,"signature-oracles.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
        if(!hatchPass||!sameBounds||first.Hash==moved.Hash||parentFirst.Hash==parentMoved.Hash||parentMoved.Hash==colored.Hash)throw new InvalidOperationException("L02 native signature oracle failed.");
        File.WriteAllText(Path.Combine(dir,"complete.sentinel"),runId);
    }
}


