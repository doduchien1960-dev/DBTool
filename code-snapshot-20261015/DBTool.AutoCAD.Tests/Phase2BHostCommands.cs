using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Generation;
using DBTool.AutoCAD.Render;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;

namespace DBTool.AutoCAD.Tests;

public sealed class Phase2BHostCommands
{
    [CommandMethod("DBTOOL_TEST_P2B", CommandFlags.Session)]
    public void Run()
    {
        var output = Environment.GetEnvironmentVariable("DBTOOL_P2B_OUTPUT") ?? throw new InvalidOperationException("Scratch evidence required.");
        var checks = new List<object>();
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"startup.txt"),"managed entry");
        void Check(string name, Action action)
        {
            File.WriteAllText(Path.Combine(output,"running.txt"), name);
            try { action(); checks.Add(new { Name=name, Passed=true, Error=(string?)null }); }
            catch (System.Exception ex) { checks.Add(new { Name=name, Passed=false, Error=ex.ToString() }); }
            File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        }
        void Assert(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
        var document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument!;
        File.AppendAllText(Path.Combine(output,"startup.txt"),"\ndocument resolved");
        var hash = Environment.GetEnvironmentVariable("DBTOOL_P2B_SOURCE_HASH") ?? throw new InvalidOperationException("Scratch hash required.");
        var source = new SourceRef(SourceKind.Dwg,"scratch-active.dwg",hash,null,null,null,"Isolated layer 0 geometry fixture");
        File.AppendAllText(Path.Combine(output,"startup.txt"),"\nsource resolved");
        // Isolated geometry fixture: layer 0/Standard only; this does not authorize the VHG production source.
        var manifest = new TemplateManifest("template-manifest-v1",hash,InspectionStatus.Inspected,
            [new TemplateAsset("Layer:test",AssetKind.Layer,"0",false,[],source),
             new TemplateAsset("FixtureBlock",AssetKind.Block,"P2B-FIXTURE",false,[],source)]);
        var template = new TemplateContractValidator().Validate(manifest,[]).Value!;
        File.AppendAllText(Path.Combine(output,"startup.txt"),"\nmanifest validated");
        var bounds = new Bounds2Mm(-10,-20,90,180);
        var view = new ViewDefinition("v",ViewPurpose.Plan,new(20,30),bounds,30);
        DrawingPackage Package(params DrawingPrimitive[] primitives) => new(hash,"p2b-fixture",[view],primitives,bounds,CalculationScope.Full);
        var style = new CadPrimitiveStyle("0");
        Database? fixtureDatabase = null;
        void WithSink(Action<CadPrimitiveSink,Transaction> action)
        {
            using var documentLock = document.LockDocument();
            var db = document.Database;
            fixtureDatabase = db;
            using var tr = db.TransactionManager.StartTransaction();
            var space = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db),OpenMode.ForWrite);
            var table=(BlockTable)tr.GetObject(db.BlockTableId,OpenMode.ForWrite);
            if(!table.Has("P2B-FIXTURE"))
            {
                var definition=new BlockTableRecord{Name="P2B-FIXTURE"};
                table.Add(definition);tr.AddNewlyCreatedDBObject(definition,true);
                var line=new Line(new Point3d(0,0,0),new Point3d(10,0,0));
                definition.AppendEntity(line);tr.AddNewlyCreatedDBObject(line,true);
            }
            var sink = new CadPrimitiveSink(db,tr,space,new(100,200));
            action(sink,tr);
        }
        Entity EntityFor(Transaction tr,string handle) => (Entity)tr.GetObject(fixtureDatabase!.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0),OpenMode.ForRead);
        foreach (var sweep in new[] { Math.PI/2,-Math.PI/2,Math.PI,-Math.PI,2*Math.PI,-2*Math.PI })
            Check("N10_SignedArc_"+sweep,()=>WithSink((sink,tr)=>
            {
                var entity=EntityFor(tr,sink.AppendArc(new(0,0),10,0,sweep,style));
                Assert(entity is Circle || entity is Arc,"Physical arc/circle required");
                if(entity is Arc arc)
                {
                    Assert(arc.TotalAngle==Math.Abs(sweep),"Complementary arc emitted");
                    Assert(arc.Length==10*Math.Abs(sweep),"Arc length differs");
                    var first = new Point3d(110,200,0);
                    var last = new Point3d(100+10*Math.Cos(sweep),200+10*Math.Sin(sweep),0);
                    Assert((arc.StartPoint==first && arc.EndPoint==last) || (arc.StartPoint==last && arc.EndPoint==first),"Arc endpoints differ");
                    Assert(arc.GeometricExtents.MinPoint.Y >= (sweep>0?200:190),"Arc bounds differ");
                }
                else Assert(((Circle)entity).Radius==10,"Full-turn radius differs");
            }));
        foreach(var radii in new[] {(40d,20d),(20d,40d)})
            Check("Ellipse_"+radii,()=>WithSink((sink,tr)=>
            {
                var entity=(Ellipse)EntityFor(tr,sink.AppendEllipse(new(0,0),radii.Item1,radii.Item2,0,2*Math.PI,style));
                var ext=entity.GeometricExtents;
                Assert(ext.MinPoint.X==100-radii.Item1 && ext.MaxPoint.X==100+radii.Item1,"Ellipse X radius lost");
                Assert(ext.MinPoint.Y==200-radii.Item2 && ext.MaxPoint.Y==200+radii.Item2,"Ellipse Y radius lost");
            }));
        Check("MinimaRotationInsertionOnce",()=>WithSink((sink,tr)=>
        {
            sink.SetViewTransform(new ViewTransform(new(20,30),1,true),bounds);
            var line=(Line)EntityFor(tr,sink.AppendLine(new(-10,180),new(90,180),style));
            Assert(line.StartPoint==new Point3d(120,230,0),"Start transformed incorrectly");
            Assert(line.EndPoint==new Point3d(120,330,0),"End transformed incorrectly");
        }));
        Check("ScaledDimensionKeepsLogicalMeasurement",()=>WithSink((sink,tr)=>
        {
            sink.SetViewTransform(new ViewTransform(new(20,30),2),bounds);
            var dimension=(RotatedDimension)EntityFor(tr,sink.AppendDimension(new(0,0),new(100,0),100,5,style));
            Assert(dimension.Measurement==50 && dimension.DimensionText=="100","Scaled view changed logical dimension text");
        }));
        Check("PatternedHatchFollowsScaleAndRotation",()=>WithSink((sink,tr)=>
        {
            sink.SetViewTransform(new ViewTransform(new(20,30),2,true),bounds);
            var result=sink.AppendHatch([new(0,0),new(100,0),new(100,100),new(0,100)],[],new CadPrimitiveStyle("0",HatchScale:12,HatchPattern:"ANSI31"));
            var hatch=(Hatch)EntityFor(tr,result.HatchHandle);
            Assert(hatch.PatternScale==6 && hatch.PatternAngle==Math.PI/2,"Hatch pattern failed to follow its view");
        }));
        Check("N09_HatchConstructionHidden",()=>
        {
            var repository=new GenerationRepository();
            var hatch=new HatchPrimitive("area","test","v",null,[new(0,0),new(100,0),new(100,100),new(0,100)],[],DrawingMaterialKind.Concrete);
            var result=new CadGenerationWriter(document,repository).WriteAsync(Package(hatch),template,
                new GenerationRequest(GenerationMode.Create,repository.GetDocumentToken(document),null,hash,new(100,200),"p2b-hatch"),default).Result;
            Assert(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
            using var tr=document.Database.TransactionManager.StartTransaction();
            fixtureDatabase=document.Database;
            var mapped=result.Value!.EntityMap.Entities.Single();
            Assert(mapped.ChildHandles.All(h=>!EntityFor(tr,h).Visible),"Hatch adds plotted outlines");
        });
        Check("CropDetected",()=>WithSink((sink,tr)=>
        {
            var handle=sink.AppendLine(new(0,0),new(100,0),style);
            try { sink.ValidateEntityOutput([handle],new Bounds2Mm(0,0,50,50)); }
            catch(InvalidOperationException ex) when(ex.Message.StartsWith("CAD_ENTITY_CROPPED")) { return; }
            throw new InvalidOperationException("Crop not detected");
        }));
        Check("ZeroPathDetected",()=>WithSink((sink,tr)=>
        {
            var handle=sink.AppendLine(new(0,0),new(0,0),style);
            try { sink.ValidateEntityOutput([handle],null); }
            catch(InvalidOperationException ex) when(ex.Message.StartsWith("CAD_ENTITY_ZERO_PATH")) { return; }
            throw new InvalidOperationException("Zero path not detected");
        }));
        var primitives = new DrawingPrimitive[]
        {
            new PolylinePrimitive("poly","test","v",null,null,null,[new(0,0),new(10,0)],false),
            new CirclePrimitive("circle","test","v",null,null,null,new(0,0),10),
            new TextPrimitive("text","test","v",null,null,null,new(0,0),"test",10,0),
            new DimensionIntentPrimitive("dim","test","v",null,null,null,new(0,0),new(10,0),10,"10",5),
            new PathPrimitive("path","test","v",null,null,null,[new ArcPathSegment(new(0,0),10,0,-Math.PI/2)],false,ViewPurpose.Plan,10,DrawingMaterialKind.RoundSteel),
            new SectionBoundaryPrimitive("ellipse","test","v","Base",null,new SectionBoundaryLoop(
                [new SectionEllipseEdge(new(0,0),40,20,0,Math.PI),new SectionEllipseEdge(new(0,0),40,20,Math.PI,2*Math.PI)],BoundaryWinding.CounterClockwise)),
            new HatchPrimitive("hatch","test","v",null,[new(0,0),new(10,0),new(10,10),new(0,10)],[],DrawingMaterialKind.Concrete),
            new TableRowPrimitive("row","test","v",null,null,null,"r",[new TableCell("q",TableCellKind.Number,null,12)],placements:[new("q",new(20,90),60)]),
            new BlockPrimitive("block","test","v",null,"FixtureBlock",new(0,0),1,[]),
            new PathPrimitive("tie","Rebar/Tie","v",null,null,null,[new LinePathSegment(new(0,0),new(10,0))],false,ViewPurpose.Plan,10,DrawingMaterialKind.RoundSteel)
        };
        Check("RendererSinkFake_AllPrimitiveDispatchAndPlacement",()=>
        {
            var sink=new RecordingSink();
            var result=new CadRenderer().Render(Package(primitives),template,sink);
            Assert(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
            Assert(sink.Transforms==primitives.Length,"Every primitive must receive the view placement");
            Assert(sink.Ellipses==2,"Ellipse was dispatched as circle");
            Assert(sink.TextPositions.Contains(new Point2Mm(20,90)),"Cell coordinates ignored");
            Assert(sink.Styles.All(s=>s.ColorIndex==256),"N08 material overrides ByLayer");
            Assert(sink.Styles.Any(s=>s.Layer=="VHG-Thep3" && s.Linetype=="ByLayer"),"N08 tie style lost");
        });
        Check("P3_MaterialPatternsIgnoreUnrelatedRequiredAsset",()=>
        {
            var materialManifest=new TemplateManifest("template-manifest-v1",hash,InspectionStatus.Inspected,
                [new TemplateAsset("Hatch:unrelated",AssetKind.Hatch,"SOLID",true,[],source)]);
            var materialTemplate=new TemplateContractValidator().Validate(materialManifest,[]).Value!;
            var sink=new RecordingSink();
            var hatches=new DrawingPrimitive[] {
                new HatchPrimitive("p3-concrete","Material/Concrete","v","Base",[new(0,0),new(100,0),new(100,100),new(0,100)],[],DrawingMaterialKind.Concrete),
                new HatchPrimitive("p3-pad","Material/Bedding","v","Pad",[new(0,0),new(100,0),new(100,100),new(0,100)],[],DrawingMaterialKind.Concrete)};
            var result=new CadRenderer().Render(Package(hatches),materialTemplate,sink);
            Assert(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
            Assert(sink.Styles[0].HatchPattern=="AR-CONC" && sink.Styles[0].HatchScale==12,"Concrete policy differs");
            Assert(sink.Styles[1].HatchPattern=="ANSI32" && sink.Styles[1].HatchScale==120,"Pad policy differs");
            Assert(sink.Styles.All(s=>s.Layer=="VHG-VL" && s.ColorIndex==256),"Material role must use VL/ByLayer");
            var missing=new RecordingSink { PatternsAvailable=false };
            var rejected=new CadRenderer().Render(Package(hatches),materialTemplate,missing);
            Assert(!rejected.Success && rejected.Diagnostics.Any(d=>d.Code=="TEMPLATE_HATCH_MISSING") && missing.Styles.Count==0,
                "Missing material patterns must block before writing");
        });
        Check("EmptyGenerationRejectedBeforeSink",()=>
        {
            var sink=new RecordingSink();
            var result=new CadRenderer().Render(Package(),template,sink);
            Assert(!result.Success && sink.Styles.Count==0,"Empty generation was accepted");
        });
        Check("RendererReal_AllPrimitiveOutput",()=>WithSink((sink,tr)=>
        {
            var result=new CadRenderer().Render(Package(primitives),template,sink);
            Assert(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
            Assert(result.Value!.Entities.Length==primitives.Length,"Primitive map lost entities");
        }));
        Check("LayoutEntityExtentsMatchRotatedFrame",()=>WithSink((sink,tr)=>
        {
            var frame=new SheetViewFrame("v",ViewPurpose.Plan,bounds,new Bounds2Mm(10,20,10+200d/30,20+100d/30),
                new ViewTransform(new(10,20),30,true),"1:30",[]);
            var layout=new SheetLayout("fixture",[new SheetLayoutSheet(1,1,new Bounds2Mm(0,0,420,297),[frame])]);
            var rectangle=new PolylinePrimitive("frame","test","v",null,null,null,[new(-10,-20),new(90,-20),new(90,180),new(-10,180)],true);
            var result=new CadRenderer().Render(Package(rectangle),template,sink,layout);
            Assert(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Message)));
            var ext=EntityFor(tr,result.Value!.Entities.Single().Handle).GeometricExtents;
            Assert(ext.MinPoint==new Point3d(400,800,0) && ext.MaxPoint==new Point3d(600,900,0),"Physical extents differ from layout");
        }));
        Check("ActualFrameCropDetected",()=>WithSink((sink,tr)=>
        {
            var frame=new SheetViewFrame("v",ViewPurpose.Plan,bounds,new Bounds2Mm(10,20,11,21),new ViewTransform(new(10,20),30),"1:30",[]);
            var layout=new SheetLayout("fixture",[new SheetLayoutSheet(1,1,new Bounds2Mm(0,0,420,297),[frame])]);
            var rectangle=new PolylinePrimitive("frame","test","v",null,null,null,[new(-10,-20),new(90,-20),new(90,180),new(-10,180)],true);
            var result=new CadRenderer().Render(Package(rectangle),template,sink,layout);
            Assert(!result.Success && result.Diagnostics.Any(d=>d.Message.Contains("CAD_ENTITY_CROPPED")),"Narrow frame silently passed");
        }));
        Check("CreateReplaceAndFaultRollbackOwnership",()=>
        {
            var repository=new GenerationRepository();
            var writer=new CadGenerationWriter(document,repository);
            GenerationRequest Request(GenerationMode mode,string id) => new(mode,repository.GetDocumentToken(document),mode==GenerationMode.Replace?id:null,hash,new(100,200),id);
            var package=Package(primitives[0]);
            var create=writer.WriteAsync(package,template,Request(GenerationMode.Create,"p2b-owned"),default).Result;
            Assert(create.Success,string.Join(";",create.Diagnostics.Select(d=>d.Message)));
            var before=repository.ReadOwnedHandles(document,"p2b-owned");
            Assert(!writer.WriteAsync(package,template,Request(GenerationMode.Create,"p2b-owned"),default).Result.Success,"Duplicate Create succeeded");
            Assert(before.SequenceEqual(repository.ReadOwnedHandles(document,"p2b-owned")),"Duplicate Create changed ownership");
            var replace=writer.WriteAsync(package,template,Request(GenerationMode.Replace,"p2b-owned"),default).Result;
            Assert(replace.Success,"Replace failed");
            var after=repository.ReadOwnedHandles(document,"p2b-owned");
            Assert(after.Count==before.Count && !after.SequenceEqual(before),"Replace duplicated or retained old entities");
            var fault=Package(new TableRowPrimitive("bad","test","v",null,null,null,"r",[new TableCell("q",TableCellKind.Number,null,12)]));
            Assert(!writer.WriteAsync(fault,template,Request(GenerationMode.Replace,"p2b-owned"),default).Result.Success,"Fault did not fail");
            Assert(after.SequenceEqual(repository.ReadOwnedHandles(document,"p2b-owned")),"Fault erased prior generation");
        });
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }

    [CommandMethod("DBTOOL_TEST_P2B_AREA")]
    public void VerifyAreaAfterRegen() => AppendCheck("N09_AreaAfterCommandRegen",()=>
    {
        var document=Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument!;
        if(new GenerationRepository().ReadOwnedHatchAreas(document,"p2b-hatch").Single()!=10000)
            throw new InvalidOperationException("Native hatch area differs");
    });

    [CommandMethod("DBTOOL_TEST_P2B_UNDO")]
    public void VerifyUndo() => AppendCheck("CommandUndoRestoresOwnership",()=>
    {
        var document=Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument!;
        var repository=new GenerationRepository();
        if(repository.ReadOwnedHandles(document,"p2b-owned").Count!=0 || repository.ReadOwnedHandles(document,"p2b-hatch").Count!=0)
            throw new InvalidOperationException("Undo did not remove command-owned generations");
    });

    private static void AppendCheck(string name,Action action)
    {
        var output=Environment.GetEnvironmentVariable("DBTOOL_P2B_OUTPUT")!;
        var path=Path.Combine(output,"checks.json");
        var checks=JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(path))!;
        bool passed=true;string? error=null;
        try { action(); } catch(System.Exception ex) { passed=false;error=ex.ToString(); }
        checks.Add(JsonSerializer.SerializeToElement(new{Name=name,Passed=passed,Error=error}));
        File.WriteAllText(path,JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
    }

    private sealed class RecordingSink : ICadPrimitiveSink
    {
        public int Transforms,Ellipses;
        public List<Point2Mm> TextPositions {get;}=[];
        public List<CadPrimitiveStyle> Styles {get;}=[];
        public bool PatternsAvailable {get;set;}=true;
        public void SetViewTransform(ViewTransform transform,Bounds2Mm bounds) { Transforms++; }
        public void ValidateEntityOutput(IReadOnlyList<string> handles,Bounds2Mm? allowedBounds) { }
        public bool HasBlock(string name)=>true;
        public bool HasLayer(string name)=>true;
        public bool HasHatchPattern(string name)=>PatternsAvailable;
        private string Add(CadPrimitiveStyle style) { Styles.Add(style);return Styles.Count.ToString("X"); }
        public string AppendLine(Point2Mm a,Point2Mm b,CadPrimitiveStyle s)=>Add(s);
        public string AppendPolyline(IReadOnlyList<Point2Mm> p,bool c,CadPrimitiveStyle s,IReadOnlyList<double>? b=null)=>Add(s);
        public string AppendArc(Point2Mm c,double r,double a,double w,CadPrimitiveStyle s)=>Add(s);
        public string AppendEllipse(Point2Mm c,double x,double y,double a,double b,CadPrimitiveStyle s) {Ellipses++;return Add(s);}
        public string AppendText(Point2Mm p,string t,double h,double r,CadPrimitiveStyle s) {TextPositions.Add(p);return Add(s);}
        public string AppendDimension(Point2Mm a,Point2Mm b,double m,double o,CadPrimitiveStyle s,double? rotationRadians = null,Point2Mm? dimensionLinePoint = null,string? textOverride = null)=>Add(s);
        public CadHatchResult AppendHatch(IReadOnlyList<Point2Mm> o,IReadOnlyList<IReadOnlyList<Point2Mm>> i,CadPrimitiveStyle s)=>new(Add(s),[]);
        public CadBlockResult AppendBlock(string n,Point2Mm p,double a,IReadOnlyDictionary<string,string> d,CadPrimitiveStyle s,double rotationRadians=0)=>new(Add(s),[]);
    }
}
