using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Render;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;

namespace DBTool.AutoCAD.Tests;

/// <summary>Isolated native-asset transform test. Every insertion is rolled back; not assembly approval.</summary>
public sealed class CoverPlacementHostCommands
{
    [CommandMethod("DBTOOL_TEST_COVER_PLACEMENT",CommandFlags.Session)]
    public void Run()
    {
        string output=Environment.GetEnvironmentVariable("DBTOOL_COVER_HOST_OUTPUT")??throw new InvalidOperationException("Scratch evidence directory required.");
        string hash=Environment.GetEnvironmentVariable("DBTOOL_COVER_HOST_HASH")??throw new InvalidOperationException("Scratch source hash required.");
        Directory.CreateDirectory(output);
        var doc=Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument!;
        using var locked=doc.LockDocument();
        var db=doc.Database;
        string[] names;
        int initialCount;
        using(var read=db.TransactionManager.StartTransaction())
        {
            var blocks=(BlockTable)read.GetObject(db.BlockTableId,OpenMode.ForRead);
            names=blocks.Cast<ObjectId>().Select(id=>((BlockTableRecord)read.GetObject(id,OpenMode.ForRead)).Name)
                .Where(n=>n.EndsWith(".MB",StringComparison.Ordinal)||n.EndsWith(".AA",StringComparison.Ordinal)||n.EndsWith(".BB",StringComparison.Ordinal))
                .OrderBy(n=>n,StringComparer.Ordinal).ToArray();
            initialCount=((BlockTableRecord)read.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForRead)).Cast<ObjectId>().Count();
        }
        var checks=new List<object>();
        var normalExtents=new Dictionary<string,double[]>();
        foreach(string name in names)
        foreach(bool mirrored in new[]{false,true})
        {
            try
            {
                using var tr=db.TransactionManager.StartTransaction();
                var space=(BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
                var sink=new CadPrimitiveSink(db,tr,space,new(0,0));
                var source=new SourceRef(SourceKind.Dwg,"scratch-active.dwg",hash,null,null,null,"Native block transform fixture; no structural assembly approval");
                var manifest=new TemplateManifest("template-manifest-v1",hash,InspectionStatus.Inspected,
                    [new("Layer:test",AssetKind.Layer,"0",false,[],source),new("FixtureBlock",AssetKind.Block,name,false,[],source)]);
                var validated=new TemplateContractValidator().Validate(manifest,[]).Value!;
                var bounds=new Bounds2Mm(0,0,4000,4000);
                var primitive=new BlockPrimitive("cover","test","v",null,"FixtureBlock",new(2000,2000),1,[],mirrorHorizontal:mirrored);
                var package=new DrawingPackage(hash,"cover-placement-fixture",[new("v",ViewPurpose.Plan,new(0,0),bounds,30)],
                    [primitive],bounds,CalculationScope.Full);
                var result=new CadRenderer().Render(package,validated,sink);
                if(!result.Success) throw new InvalidOperationException(string.Join(";",result.Diagnostics.Select(d=>d.Message)));
                var handle=result.Value!.Entities.Single(e=>e.SemanticId=="cover").Handle;
                var reference=(BlockReference)tr.GetObject(db.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0),OpenMode.ForRead);
                void Near(double actual,double expected)
                {if(Math.Abs(actual-expected)>1e-5) throw new InvalidOperationException($"Expected{expected:R}, actual{actual:R}");}
                Near(reference.ScaleFactors.X,mirrored?-1:1);Near(reference.ScaleFactors.Y,1);Near(reference.ScaleFactors.Z,1);
                Near(reference.Rotation,0);Near(reference.Position.X,2000);Near(reference.Position.Y,2000);
                var ext=reference.GeometricExtents;
                double[] local=[ext.MinPoint.X-2000,ext.MinPoint.Y-2000,ext.MaxPoint.X-2000,ext.MaxPoint.Y-2000];
                if(!mirrored) normalExtents.Add(name,local);
                else
                {
                    var normal=normalExtents[name];
                    Near(local[0],-normal[2]);Near(local[2],-normal[0]);Near(local[1],normal[1]);Near(local[3],normal[3]);
                }
                checks.Add(new{Name=name,Mirrored=mirrored,Passed=true,Error=(string?)null,Extents=local});
                // No Commit: every native insertion is deliberately aborted.
            }
            catch(System.Exception ex)
            {checks.Add(new{Name=name,Mirrored=mirrored,Passed=false,Error=ex.ToString()});}
            File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        }
        using var finalRead=db.TransactionManager.StartTransaction();
        int finalCount=((BlockTableRecord)finalRead.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForRead)).Cast<ObjectId>().Count();
        File.WriteAllText(Path.Combine(output,"counts.json"),JsonSerializer.Serialize(new{NativeViews=names.Length,InitialCount=initialCount,FinalCount=finalCount,AbortPreserved=(initialCount==finalCount)}));
    }
}
