using System.Security.Cryptography;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Generation;
using DBTool.AutoCAD.Template;
using DBTool.Drawing;
using Acad = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DBTool.AutoCAD.Tests;

public sealed class DrawingRegressionCommand
{
    [CommandMethod("DBTOOL_REGRESSION", CommandFlags.Session)]
    public void Run()
    {
        var run = Environment.GetEnvironmentVariable("DBTOOL_REGRESSION_RUN") ?? throw new InvalidOperationException("DBTOOL_REGRESSION_RUN required");
        Directory.CreateDirectory(run);
        var document=Acad.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("CAD_DOCUMENT_MISSING");
        // Require an explicitly staged test copy, never save the user's current production drawing.
        if(!Path.GetFullPath(document.Name).StartsWith(Path.GetFullPath(run)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("REGRESSION_TEST_COPY_REQUIRED");
        var frozen=Path.Combine(run,"drawing.json");
        if(!File.Exists(frozen)) File.WriteAllText(frozen,DrawingPackageJson.Serialize(Session08HostCommands.FixturePackage()));
        var packageResult=DrawingPackageJson.DeserializeResult(File.ReadAllText(frozen));
        if(!packageResult.Success) { Write(run,"regression.json",new { Status="Blocked",packageResult.Diagnostics }); return; }
        var package=packageResult.Value!;
        var expectedContractPath=Environment.GetEnvironmentVariable("DBTOOL_PHYSICAL_CONTRACT")??Path.Combine(run,"expected-physical-contract-r04.json");
        if(!File.Exists(expectedContractPath)) { Write(run,"regression.json",new { Status="Blocked",Diagnostic="SNAPSHOT_EXPECTED_CONTRACT_REQUIRED",ExpectedContractPath=expectedContractPath });return; }
        var expectedContractJson=File.ReadAllText(expectedContractPath);
        try { CanonicalPhysicalValidator.ValidateContract(expectedContractJson); }
        catch(System.Exception ex) { Write(run,"regression.json",new { Status="Blocked",Diagnostic=ex.Message });return; }
        var lockedContractHash=Hash(expectedContractPath);
        File.AppendAllText(Path.Combine(run,"regression-progress.log"),"Frozen package read\n");
        var templatePath=Environment.GetEnvironmentVariable("DBTOOL_TEMPLATE_PATH")!;
        var roles=new[] { new TemplateRequirement("SteelSummary",AssetKind.Block,["TONG"]) };
        var manifest=new TemplateManifestReader().Read(new(templatePath,Environment.GetEnvironmentVariable("DBTOOL_TEMPLATE_SHA256"),roles,"VHGpro 3.2/Template/VHG-Template.dwg"));
        if(!manifest.Success) { Write(run,"regression.json",new { Status="Blocked",manifest.Diagnostics }); return; }
        var template=new TemplateContractValidator().Validate(manifest.Value!,roles,TemplateValidationMode.ProductionRender);
        File.AppendAllText(Path.Combine(run,"regression-progress.log"),"Template validated\n");
        if(!template.Success) { Write(run,"regression.json",new { Status="Blocked",template.Diagnostics }); return; }
        // The guarded run drawing is disposable. Remove model-space template demonstration entities
        // and previous smoke fixtures; keep inspected block/style definitions for the frozen render.
        using(var documentLock=document.LockDocument())
        using(var tr=document.Database.TransactionManager.StartTransaction())
        {
            var space=(BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database),OpenMode.ForRead);
            foreach(ObjectId id in space) tr.GetObject(id,OpenMode.ForWrite).Erase();
            tr.Commit();
        }
        var repo=new GenerationRepository();
        var result=new DBTool.AutoCAD.CadGenerationWriter(document,repo).WriteAsync(package,template.Value!,new(GenerationMode.Create,repo.GetDocumentToken(document),null,package.InputHash,new(0,0)),default).GetAwaiter().GetResult();
        if(!result.Success) { Write(run,"regression.json",new { Status="Blocked",result.Diagnostics }); return; }
        File.AppendAllText(Path.Combine(run,"regression-progress.log"),"Render committed\n");
        var archive=new SemanticSnapshotReader().ReadArchived(document,result.Value!,package);
        Write(run,"archived-native-readback.json",archive);
        var dwg=Path.Combine(run,"regression.dwg");
        using(document.LockDocument()) document.Database.SaveAs(dwg,DwgVersion.Current);
        CanonicalPhysicalSnapshot actual;
        try {
            if(Hash(expectedContractPath)!=lockedContractHash)throw new InvalidOperationException("SNAPSHOT_EXPECTED_CONTRACT_CHANGED: external contract changed during capture");
            actual=new SemanticSnapshotReader().ReadCanonical(result.Value!,expectedContractJson,dwg,Guid.NewGuid().ToString("N"));
        } catch(System.Exception ex) {Write(run,"semanticdiff.json",new {Status="Blocked",Diagnostic=ex.Message});Write(run,"regression.json",new {Status="Blocked",Diagnostic=ex.Message,Scope="Session08 native fixture; acceptance incomplete",DwgHash=Hash(dwg)});return;}
        File.WriteAllText(Path.Combine(run,"actual-semantic.json"),actual.Json);
        File.AppendAllText(Path.Combine(run,"regression-progress.log"),"Snapshot complete\n");
        Write(run,"generation.json",result.Value!);
        var expectedFile=Path.Combine(run,"expected-semantic.json");
        DiffReport? diff=null;
        if(File.Exists(expectedFile))
        {
            try { var expected=CanonicalPhysicalValidator.Validate(File.ReadAllText(expectedFile),expectedContractJson);diff=SemanticDiff.Compare(expected,actual); }
            catch(System.Exception ex) {Write(run,"semanticdiff.json",new {Status="Blocked",Diagnostic=ex.Message});Write(run,"regression.json",new {Status="Blocked",Diagnostic=ex.Message});return;}
        }
        Write(run,"semanticdiff.json",new { Status=diff is null ? "ReferencePending" : diff.NoDifference ? "NoDifference" : "Changed",Report=diff });
        string pdfStatus;
        try { PlotPdf(document,Path.Combine(run,"regression.pdf"),archive); pdfStatus="Created"; }
        catch(System.Exception ex) { pdfStatus="Blocked: "+ex.Message; }
        Write(run,"regression.json",new { Status="ReadyForHumanReview",Scope="Session08 analytic fixture only; production GC/full profiles pending",InputHash=package.InputHash,TemplateHash=manifest.Value!.SourceFileHash,DrawingHash=Hash(frozen),SnapshotHash=Hash(Path.Combine(run,"actual-semantic.json")),DwgHash=Hash(dwg),PdfStatus=pdfStatus,SemanticStatus=diff is null ? "ReferencePending" : diff.NoDifference ? "NoDifference" : "Changed",VisualApproval="Pending" });
    }

    [CommandMethod("DBTOOL_SNAPSHOT_R04",CommandFlags.Session)]
    public void CaptureSavedR04()
    {
        var run=Environment.GetEnvironmentVariable("DBTOOL_REGRESSION_RUN")??throw new InvalidOperationException("DBTOOL_REGRESSION_RUN required");Directory.CreateDirectory(run);
        try {
            var dwg=Environment.GetEnvironmentVariable("DBTOOL_SNAPSHOT_DWG")??Path.Combine(run,"regression.dwg");
            var contractPath=Environment.GetEnvironmentVariable("DBTOOL_PHYSICAL_CONTRACT")??Path.Combine(run,"expected-physical-contract-r04.json");
            var generationPath=Environment.GetEnvironmentVariable("DBTOOL_GENERATION_MAP")??Path.Combine(run,"generation.json");
            var contractJson=File.ReadAllText(contractPath);var contractHash=Hash(contractPath);CanonicalPhysicalValidator.ValidateContract(contractJson);
            var artifact=JsonSerializer.Deserialize<GenerationArtifact>(File.ReadAllText(generationPath))??throw new InvalidOperationException("SNAPSHOT_GENERATION_MAP_REQUIRED");
            var actual=new SemanticSnapshotReader().ReadCanonical(artifact,contractJson,dwg,Guid.NewGuid().ToString("N"));
            if(Hash(contractPath)!=contractHash)throw new InvalidOperationException("SNAPSHOT_EXPECTED_CONTRACT_CHANGED");
            File.WriteAllText(Path.Combine(run,"actual-semantic.json"),actual.Json);
            var expectedPath=Path.Combine(run,"expected-semantic.json");
            var diff=File.Exists(expectedPath)?SemanticDiff.Compare(CanonicalPhysicalValidator.Validate(File.ReadAllText(expectedPath),contractJson),actual):null;
            Write(run,"semanticdiff.json",new{Status=diff is null?"ReferencePending":diff.NoDifference?"NoDifference":"Changed",SchemaVersion=CanonicalPhysicalValidator.SchemaVersion,ExpectedContractHash=contractHash,SourceDwgHash=Hash(dwg),Report=diff});
        }catch(System.Exception ex){Write(run,"semanticdiff.json",new{Status="Blocked",Diagnostic=ex.Message,SchemaVersion=CanonicalPhysicalValidator.SchemaVersion});}
    }

    private static void PlotPdf(Autodesk.AutoCAD.ApplicationServices.Document document,string output,SemanticSnapshot actual)
    {
        using var documentLock=document.LockDocument();
        using var tr=document.Database.TransactionManager.StartTransaction();
        var layout=(Layout)tr.GetObject(LayoutManager.Current.GetLayoutId(LayoutManager.Current.CurrentLayout),OpenMode.ForRead);
        using var settings=new PlotSettings(layout.ModelType); settings.CopyFrom(layout);
        var validator=PlotSettingsValidator.Current;
        validator.SetPlotConfigurationName(settings,"DWG To PDF.pc3",null); validator.RefreshLists(settings);
        var media=validator.GetCanonicalMediaNameList(settings).Cast<string>().FirstOrDefault(name=>name.Contains("A3",StringComparison.Ordinal)&&name.Contains("ISO",StringComparison.Ordinal));
        if(media is not null) validator.SetCanonicalMediaName(settings,media);
        validator.SetPlotRotation(settings,PlotRotation.Degrees090);
        validator.SetPlotType(settings,Autodesk.AutoCAD.DatabaseServices.PlotType.Window);
        // Use actual entity bounds including negative table Y and ATT text. This is an analytic
        // precheck plot; full production page scale/layout approval remains required.
        var minX=actual.Entities.Min(e=>e.Numbers["Bounds.Min.X"])-200;
        var minY=actual.Entities.Min(e=>e.Numbers["Bounds.Min.Y"])-200;
        var maxX=actual.Entities.Max(e=>e.Numbers["Bounds.Max.X"])+200;
        var maxY=actual.Entities.Max(e=>e.Numbers["Bounds.Max.Y"])+200;
        validator.SetPlotWindowArea(settings,new Extents2d(new Point2d(minX,minY),new Point2d(maxX,maxY)));
        validator.SetUseStandardScale(settings,true); validator.SetStdScaleType(settings,StdScaleType.ScaleToFit); validator.SetPlotCentered(settings,true);
        using var info=new PlotInfo { Layout=layout.ObjectId,OverrideSettings=settings };
        using var infoValidator=new PlotInfoValidator { MediaMatchingPolicy=MatchingPolicy.MatchEnabled }; infoValidator.Validate(info);
        using var engine=PlotFactory.CreatePublishEngine();
        engine.BeginPlot(null,null); engine.BeginDocument(info,document.Name,null,1,true,output);
        using var page=new PlotPageInfo(); engine.BeginPage(page,info,true,null); engine.BeginGenerateGraphics(null); engine.EndGenerateGraphics(null); engine.EndPage(null); engine.EndDocument(null); engine.EndPlot(null);
        tr.Commit();
    }
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Write(string run,string name,object value)=>File.WriteAllText(Path.Combine(run,name),JsonSerializer.Serialize(value,new JsonSerializerOptions { WriteIndented=true }));
}
