using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DBTool.AutoCAD.Render;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using System.Text.Json;

namespace DBTool.AutoCAD.Tests;
public sealed class Phase5AHostCommands
{
    [CommandMethod("DBTOOL_TEST_P5A",CommandFlags.Session)]
    public void Run()
    {
        string output=Environment.GetEnvironmentVariable("DBTOOL_P5A_OUTPUT")??throw new InvalidOperationException("Scratch evidence required.");
        var checks=new List<object>();
        var doc=Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument!;
        void Check(string name,string block,IReadOnlyList<BlockAttributeIntent> attrs,double x)
        {
            try {
                using var locked=doc.LockDocument();
                var db=doc.Database;
                using var tr=db.TransactionManager.StartTransaction();
                var space=(BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
                var sink=new CadPrimitiveSink(db,tr,space,new(0,0));
                if(!sink.HasBlock(block))throw new InvalidOperationException("Missing native "+block);
                var result=sink.AppendBlock(block,new(x,0),0.2,attrs.ToDictionary(a=>a.Key,a=>a.Value),new CadPrimitiveStyle("0"));
                var reference=(BlockReference)tr.GetObject(db.GetObjectId(false,new Handle(Convert.ToInt64(result.BlockHandle,16)),0),OpenMode.ForRead);
                var actual=reference.AttributeCollection.Cast<ObjectId>().Select(id=>(AttributeReference)tr.GetObject(id,OpenMode.ForRead)).ToDictionary(a=>a.Tag,a=>a.TextString,StringComparer.OrdinalIgnoreCase);
                foreach(var attr in attrs)if(!actual.TryGetValue(attr.Key,out var value)||value!=attr.Value)throw new InvalidOperationException("Attribute differs: "+attr.Key);
                sink.ValidateEntityOutput([result.BlockHandle,..result.AttributeHandles],null);
                var ext=reference.GeometricExtents;
                checks.Add(new {Name=name,Passed=true,Error=(string?)null,Tags=actual,Min=new[]{ext.MinPoint.X,ext.MinPoint.Y},Max=new[]{ext.MaxPoint.X,ext.MaxPoint.Y}});
                // Intentionally abort: native fixtures never persist into the scratch file.
            } catch(System.Exception ex) {checks.Add(new {Name=name,Passed=false,Error=ex.ToString()});}
        }
        Check("TK.THKL_9_tags_and_extents",ScheduleNativeAttributes.SummaryBlockName,ScheduleNativeAttributes.Summary(new Dictionary<string,double>{["DAY1"]=12,["CO2"]=3},15),0);
        Check("TK.TG_catalog_mass_units_and_extents",ScheduleNativeAttributes.AngleBlockName,ScheduleNativeAttributes.Angle("A001","V50",2,1500,3000,2,6),5000);
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }
}
