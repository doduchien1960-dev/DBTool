using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Template;
using DBTool.AutoCAD.Render;
using DBTool.Domain.Geometry;
using DBTool.Domain;
using DBTool.Application;
using DBTool.Drawing;
using DBTool.AutoCAD.Generation;

namespace DBTool.AutoCAD.Tests;

public sealed class Phase2AHostCommands
{
    [CommandMethod("DBTOOL_TEST_P2A", CommandFlags.Session)]
    public void Run()
    {
        var path = Environment.GetEnvironmentVariable("DBTOOL_P2A_TEMPLATE") ?? throw new InvalidOperationException("Scratch template required.");
        var output = Environment.GetEnvironmentVariable("DBTOOL_P2A_OUTPUT") ?? throw new InvalidOperationException("Evidence directory required.");
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        void Check(string name, Action test)
        {
            try { test(); checks.Add(new { Name = name, Passed = true, Error = (string?)null }); }
            catch (System.Exception ex) { checks.Add(new { Name = name, Passed = false, Error = ex.ToString() }); }
        }
        void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        var read = new TemplateManifestReader().Read(new(path, null, VhgTemplateRoles.Presentation, "scratch-template.dwg"));
        File.WriteAllText(Path.Combine(output, "manifest.json"), JsonSerializer.Serialize(read, new JsonSerializerOptions { WriteIndented = true }));
        Check("InspectedV2ContractAndTongAttribute", () =>
        {
            Assert(read.Success, string.Join(";", read.Diagnostics.Select(d => d.Message)));
            var contract = new TemplateContractValidator().Validate(read.Value!, VhgTemplateRoles.Presentation);
            Assert(contract.Success, string.Join(";", contract.Diagnostics.Select(d => d.Message + " expected=" + d.Expected + " actual=" + d.Actual)));
            Assert(read.Value!.Assets.Single(a => a.Role == "SteelSummary").AttributeTags.Contains("TONG"), "TONG is missing in TK.THKL.");
            Assert(!read.Value.Assets.Any(a => a.AssetKind == AssetKind.Block && a.Name == "TONG"), "TONG must not be a standalone block.");
        });
        if (read.Value is not null)
        {
            var manifest = read.Value;
            using var source = Open(path);
            Check("IsolatedRendererNativeMetricFixtureNotGoldenAcceptance", () =>
            {
                var document = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument!;
                var hash = new string('e', 64);
                // This isolated consumer test checks source-property preservation. The standards gate above
                // remains a separate failing acceptance check when the supplied source is incompatible.
                var validated = new TemplateContractValidator().Validate(manifest, []).Value!;
                var package = new DrawingPackage(hash, "p2a-native-fixture/v1",
                    [new ViewDefinition("style", ViewPurpose.Plan, new(0, 0), new Bounds2Mm(0, -8910, 12600, 2000), 30)],
                    [
                        new PathPrimitive("tie", "Rebar/Tie", "style", null, null, null,
                            [new LinePathSegment(new(0, 0), new(1000, 0))], false, ViewPurpose.Plan, 1000, DrawingMaterialKind.RoundSteel),
                        new TextPrimitive("text", "Schedule/Row", "style", null, null, null, new(0, 1000), "P2A", VhgPresentationStandards.PaperToModel(2), 0),
                        new DimensionIntentPrimitive("dimension", "Dimension/Linear", "style", null, null, null, new(0, 0), new(1000, 0), 1000, "1000", 90),
                        new BlockPrimitive("frame", "Frame", "style", null, "Block:Khung", new(0, 0), VhgPresentationStandards.FrameInsertionScale, []),
                        new HatchPrimitive("hatch", "Geometry/Hatch", "style", null, [new(0, 0), new(100, 0), new(100, 100), new(0, 100)], [], DrawingMaterialKind.Concrete)
                    ], new Bounds2Mm(0, -8910, 12600, 2000), CalculationScope.GeometryOnly);
                var repository = new GenerationRepository();
                var result = new CadGenerationWriter(document, repository).WriteAsync(package, validated,
                    new GenerationRequest(GenerationMode.Create, repository.GetDocumentToken(document), null, hash, new(0, 0), "p2a-metrics"),
                    CancellationToken.None).GetAwaiter().GetResult();
                Assert(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code + ":" + d.Message)));
                using var tr = document.Database.TransactionManager.StartTransaction();
                Entity EntityFor(string semantic) => (Entity)tr.GetObject(document.Database.GetObjectId(false,
                    new Handle(Convert.ToInt64(result.Value!.EntityMap.Entities.Single(e => e.SemanticId == semantic).Handle, 16)), 0), OpenMode.ForRead);
                var tie = (Line)EntityFor("tie"); var text = (DBText)EntityFor("text");
                var dim = (RotatedDimension)EntityFor("dimension"); var frame = (BlockReference)EntityFor("frame");
                Assert(tie.Length == 1000 && tie.Layer == "VHG-Thep3" && tie.ColorIndex == 256 && tie.Linetype == "ByLayer", "Tie was scaled or colour/linetype overridden.");
                Assert(text.Height == 60 && text.WidthFactor == 0.8 && text.ColorIndex == 256, "Native text metrics differ.");
                Assert(dim.Measurement == 1000 && dim.Dimlfac == 1 && dim.Dimscale == 300 && dim.Dimtxt == 0.2, "Native dimension or logical units were changed.");
                Assert(frame.ScaleFactors.X == 0.3 && frame.Layer == "VHG-Dim", "Frame insertion scale differs.");
                Assert(EntityFor("hatch").Layer == "VHG-VL", "Hatch is not on material layer VL.");
                var extents = frame.GeometricExtents;
                File.WriteAllText(Path.Combine(output, "native-metrics.json"), JsonSerializer.Serialize(new
                {
                    GeometryLengthMm = tie.Length, DimensionMeasurementMm = dim.Measurement, TextHeightMm = text.Height,
                    TextWidthFactor = text.WidthFactor, Dimscale = dim.Dimscale, Dimtxt = dim.Dimtxt, Dimlfac = dim.Dimlfac,
                    FrameScale = frame.ScaleFactors.X, FrameWidthMm = extents.MaxPoint.X - extents.MinPoint.X,
                    FrameHeightMm = extents.MaxPoint.Y - extents.MinPoint.Y, Acceptance = "Isolated native metric evidence; PDF comparison NotRun"
                }, new JsonSerializerOptions { WriteIndented = true }));
            });
            Check("EmptyTargetBuiltinConflictRejected", () =>
            {
                using var target = new Database(true, true);
                var result = TemplateAssetImporter.ValidateTarget(target, TemplateAssetImporter.DependencyContract(manifest), allowMissing: true);
                Assert(!result.Success && result.Diagnostics.Any(d => d.Code == "TEMPLATE_TARGET_CONFLICT" && d.Message.Contains("Standard")),
                    "The current source's Standard conflict must be diagnosed without rewriting an empty target.");
            });
            Check("ImportIntoCompatibleScratchPreservesNativeDefinitionsAndIsIdempotent", () =>
            {
                using var target = new Database(true, true);
                // Fixture preparation only: copy the source built-in definitions into a new disposable database.
                // Production importer never rewrites definitions in an existing drawing.
                using (var prepare = source.TransactionManager.StartTransaction())
                using (var prepareTarget = target.TransactionManager.StartTransaction())
                {
                    var table = (TextStyleTable)prepare.GetObject(source.TextStyleTableId, OpenMode.ForRead);
                    var targetTable = (TextStyleTable)prepareTarget.GetObject(target.TextStyleTableId, OpenMode.ForRead);
                    var native = (TextStyleTableRecord)prepare.GetObject(table["Standard"], OpenMode.ForRead);
                    var compatible = (TextStyleTableRecord)prepareTarget.GetObject(targetTable["Standard"], OpenMode.ForWrite);
                    compatible.CopyFrom(native);
                    var sourceLayers = (LayerTable)prepare.GetObject(source.LayerTableId, OpenMode.ForRead);
                    var targetLayers = (LayerTable)prepareTarget.GetObject(target.LayerTableId, OpenMode.ForWrite);
                    var defpoints = (LayerTableRecord)prepare.GetObject(sourceLayers["Defpoints"], OpenMode.ForRead);
                    if (!targetLayers.Has("Defpoints"))
                    {
                        var copy = new LayerTableRecord { Name = "Defpoints", Color = defpoints.Color, IsPlottable = defpoints.IsPlottable };
                        targetLayers.Add(copy); prepareTarget.AddNewlyCreatedDBObject(copy, true);
                    }
                    prepareTarget.Commit();
                }
                using (var inspect = target.TransactionManager.StartTransaction())
                    File.WriteAllText(Path.Combine(output, "prepared-target-assets.json"), JsonSerializer.Serialize(
                        TemplateManifestReader.ReadTargetAssets(target, inspect, TemplateAssetImporter.DependencyContract(manifest)), new JsonSerializerOptions { WriteIndented = true }));
                TemplateAssetImporter.Import(source, target, manifest);
                using (var inspect = target.TransactionManager.StartTransaction())
                    File.WriteAllText(Path.Combine(output, "imported-target-assets.json"), JsonSerializer.Serialize(
                        TemplateManifestReader.ReadTargetAssets(target, inspect, TemplateAssetImporter.DependencyContract(manifest)), new JsonSerializerOptions { WriteIndented = true }));
                TemplateAssetImporter.Import(source, target, manifest);
                Assert(TemplateAssetImporter.ValidateTarget(target, TemplateAssetImporter.Importable(manifest)).Success, "Cloned properties differ.");
                using var tr = target.TransactionManager.StartTransaction();
                var layer = (LayerTableRecord)tr.GetObject(((LayerTable)tr.GetObject(target.LayerTableId, OpenMode.ForRead))["VHG-Thep3"], OpenMode.ForRead);
                Assert(layer.Color.ColorIndex == 6, "Thep3 ACI must be 6.");
                Assert(((LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead)).Name == "DASHED", "Thep3 must be DASHED.");
                var texts = (TextStyleTable)tr.GetObject(target.TextStyleTableId, OpenMode.ForRead);
                var regular = (TextStyleTableRecord)tr.GetObject(texts["VHG-Unicode.Regular"], OpenMode.ForRead);
                var sourceRegular = manifest.Assets.Single(a => a.Name == "VHG-Unicode.Regular" && a.AssetKind == AssetKind.TextStyle).Signature!;
                Assert(regular.FileName.Equals(sourceRegular.Properties["FileName"], StringComparison.OrdinalIgnoreCase) && regular.XScale == 0.8,
                    "Cloning changed the source native Regular font/width.");
                var dims = (DimStyleTable)tr.GetObject(target.DimStyleTableId, OpenMode.ForRead);
                var dim = (DimStyleTableRecord)tr.GetObject(dims["VHG1-30"], OpenMode.ForRead);
                Assert(dim.Dimscale == 300 && dim.Dimtxt == 0.2 && dim.Dimasz == 0.1 && dim.Dimgap == 0.05 && dim.Dimlfac == 1 && dim.Dimdec == 0,
                    "Native VHG1-30 metrics differ.");
                var space = (BlockTableRecord)tr.GetObject(target.CurrentSpaceId, OpenMode.ForWrite);
                var previous = HostApplicationServices.WorkingDatabase;
                try
                {
                    HostApplicationServices.WorkingDatabase = target;
                    var sink = new CadPrimitiveSink(target, tr, space, new Point2Mm(0, 0));
                    var handle = sink.AppendText(new(0, 0), "P2A", 60, 0, new("VHG-Text", TextStyle: "VHG-Unicode.Regular"));
                    var entity = (DBText)tr.GetObject(target.GetObjectId(false, new Handle(Convert.ToInt64(handle, 16)), 0), OpenMode.ForRead);
                    Assert(entity.WidthFactor == 0.8 && entity.Height == 60 && entity.ColorIndex == 256, "Native text metric was not applied.");
                }
                finally { HostApplicationServices.WorkingDatabase = previous; }
            });
            foreach (var mutation in new[] { "font", "font-descriptor", "width", "dimstyle", "layer", "layer-off", "layer-frozen", "linetype", "block-attribute", "block-origin", "block-units", "builtin-font" })
                Check($"SameNameConflict/{mutation}", () =>
                {
                    using var target = Open(path);
                    using (var tr = target.TransactionManager.StartTransaction())
                    {
                        if (mutation is "font" or "font-descriptor" or "width" or "builtin-font")
                        {
                            var table = (TextStyleTable)tr.GetObject(target.TextStyleTableId, OpenMode.ForRead);
                            var record = (TextStyleTableRecord)tr.GetObject(table[mutation == "builtin-font" ? "Standard" : "VHG-Unicode.Regular"], OpenMode.ForWrite);
                            if (mutation == "font") record.FileName = record.FileName.Equals("arial.ttf", StringComparison.OrdinalIgnoreCase) ? "viet buddy.shx" : "arial.ttf";
                            else if (mutation == "font-descriptor")
                            {
                                var font = record.Font;
                                record.Font = new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor(font.TypeFace, !font.Bold, font.Italic, font.CharacterSet, font.PitchAndFamily);
                            }
                            else if (mutation == "builtin-font") record.XScale = record.XScale + 0.1;
                            else record.XScale = 1;
                        }
                        else if (mutation == "dimstyle")
                        {
                            var table = (DimStyleTable)tr.GetObject(target.DimStyleTableId, OpenMode.ForRead);
                            ((DimStyleTableRecord)tr.GetObject(table["VHG1-30"], OpenMode.ForWrite)).Dimlfac = 30;
                        }
                        else if (mutation is "layer" or "layer-off" or "layer-frozen")
                        {
                            var table = (LayerTable)tr.GetObject(target.LayerTableId, OpenMode.ForRead);
                            var record = (LayerTableRecord)tr.GetObject(table["VHG-Thep3"], OpenMode.ForWrite);
                            if (mutation == "layer") record.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1);
                            else if (mutation == "layer-off") record.IsOff = !record.IsOff;
                            else record.IsFrozen = !record.IsFrozen;
                        }
                        else if (mutation == "linetype")
                        {
                            var table = (LinetypeTable)tr.GetObject(target.LinetypeTableId, OpenMode.ForRead);
                            var record = (LinetypeTableRecord)tr.GetObject(table["DASHED"], OpenMode.ForWrite);
                            record.SetDashLengthAt(0, record.DashLengthAt(0) + 1);
                        }
                        else
                        {
                            var table = (BlockTable)tr.GetObject(target.BlockTableId, OpenMode.ForRead);
                            var block = (BlockTableRecord)tr.GetObject(table["TK.THKL"], OpenMode.ForRead);
                            if (mutation == "block-origin") { block.UpgradeOpen(); block.Origin = new Autodesk.AutoCAD.Geometry.Point3d(10, 0, 0); }
                            else if (mutation == "block-units") { block.UpgradeOpen(); block.Units = block.Units == UnitsValue.Inches ? UnitsValue.Millimeters : UnitsValue.Inches; }
                            else
                            foreach (ObjectId id in block)
                                if (tr.GetObject(id, OpenMode.ForRead) is AttributeDefinition att && att.Tag == "TONG")
                                { att.UpgradeOpen(); att.TextString = "changed-content"; break; }
                        }
                        tr.Commit();
                    }
                    var resources = TemplateAssetImporter.DependencyContract(manifest);
                    var before = TemplateAssetImporter.ValidateTarget(target, resources);
                    Assert(!before.Success && before.Diagnostics.Any(d => d.Code == "TEMPLATE_TARGET_CONFLICT"), "No same-name content diagnostic.");
                    var rejected = false;
                    try { TemplateAssetImporter.Import(source, target, manifest); }
                    catch (InvalidOperationException ex) when (ex.Message.Contains("TEMPLATE_TARGET_CONFLICT")) { rejected = true; }
                    Assert(rejected, "Import did not block before clone.");
                    var after = TemplateAssetImporter.ValidateTarget(target, resources);
                    Assert(before.Diagnostics.Select(d => d.Actual).SequenceEqual(after.Diagnostics.Select(d => d.Actual)), "Target changed after rejection.");
                });
        }
        File.WriteAllText(Path.Combine(output, "checks.json"), JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Database Open(string path)
    {
        var database = new Database(false, true);
        database.ReadDwgFile(path, FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
        return database;
    }
}
