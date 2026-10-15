using System.Security.Cryptography;
using System.Text.Json;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Generation;
using DBTool.AutoCAD.Template;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Acad = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DBTool.AutoCAD.Tests;

/// <summary>Scratch-only diagnostic rendering; never advertises a production-complete fixture.</summary>
public sealed class Phase6GenerationCommands
{
    [CommandMethod("DBTOOL_P6_GENERATE", CommandFlags.Session)]
    public void Run()
    {
        var run = Path.GetFullPath(Environment.GetEnvironmentVariable("DBTOOL_P6_RUN")
            ?? throw new InvalidOperationException("DBTOOL_P6_RUN required"));
        var document = Acad.DocumentManager.MdiActiveDocument ?? throw new InvalidOperationException("No document");
        if (!Path.GetFullPath(document.Name).StartsWith(run + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Scratch document required");
        void Write(string name, object value) => File.WriteAllText(Path.Combine(run, name),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        try
        {
            var parsed = DrawingPackageJson.DeserializeResult(File.ReadAllText(Path.Combine(run, "drawing.json")));
            if (!parsed.Success) { Write("generation-result.json", new { Status = "Blocked", parsed.Diagnostics }); return; }
            var package = parsed.Value!;
            using(document.LockDocument())
            {
                // Explicit native stroke properties measured in the reference PDF.
                document.Database.EndCaps=EndCap.Round;
                document.Database.JoinStyle=JoinStyle.StyleRound;
            }
            var notesPath = Path.Combine(run, "material-notes.json");
            if (File.Exists(notesPath))
            {
                using var notes = JsonDocument.Parse(File.ReadAllText(notesPath));
                if (notes.RootElement.GetProperty("InputHash").GetString() != package.InputHash)
                    throw new InvalidOperationException("Material notes belong to a different drawing input.");
                var labels = notes.RootElement.GetProperty("Labels").EnumerateObject()
                    .ToDictionary(p=>p.Name,p=>p.Value.GetString()!);
                package = new DrawingPackage(package.InputHash, package.RuleVersion, package.Views,
                    package.Primitives.Concat(SheetOneMaterialLeaders.Build(package, labels)), package.Bounds, package.Scope);
                if(notes.RootElement.TryGetProperty("HatchOrigins",out var origins))
                {
                    var byView=origins.EnumerateObject().ToDictionary(p=>p.Name,p=>new Point2Mm(p.Value[0].GetDouble(),p.Value[1].GetDouble()));
                    package=new DrawingPackage(package.InputHash,package.RuleVersion,package.Views,package.Primitives.Select(p=>
                        p is HatchPrimitive hatch && byView.TryGetValue(p.ViewId,out var origin)
                            ? new HatchPrimitive(hatch.SemanticId,hatch.Role,hatch.ViewId,hatch.ComponentId,hatch.OuterLoop,
                                hatch.InnerLoops.Select(loop=>loop.AsEnumerable()),hatch.MaterialKind,origin,hatch.NativeBoundaryLoops):p),package.Bounds,package.Scope);
                }
                Write("material-notes-receipt.json", new { SHA256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(notesPath))), Source = notes.RootElement.Clone() });
            }
            // Explicit diagnostic annotation envelope, checked again against actual native extents.
            bool isLargePackage = SheetLayoutPlanner.RequiresAdditionalSheets(package);
            double AnnotationEnvelope(ViewDefinition view) =>
                view.Id.StartsWith("Reinforcement/Neck/") || view.Id.StartsWith("Reinforcement/Catch/") || view.Id == "SteelSchedule" || view.Id == SheetFiveViews.ViewId ? 0 :
                isLargePackage ? 50 : view.Id switch
            {
                "WallMeshPlan" => 900,
                "SteelAA" or "SteelBB" => 300,
                "Plan" or "AA" or "BB" => 500,
                _ => view.Id.StartsWith("Base") || view.Id.StartsWith("Reinforcement/Wall/") ? 300 : view.Purpose == ViewPurpose.SteelTable ? 100 : 500
            };
            var diagnosticViews = package.Views.Select(v =>
            {
                double margin = AnnotationEnvelope(v);
                var notePoints=package.Primitives.Where(p=>p.ViewId==v.Id && p.SemanticId.Contains("/MaterialLeader/",StringComparison.Ordinal))
                    .SelectMany(p=>p is PolylinePrimitive line?line.Points.AsEnumerable():p is TextPrimitive text?new[]{text.Position}:Enumerable.Empty<Point2Mm>())
                    .Concat(package.Primitives.OfType<DimensionIntentPrimitive>().Where(p=>p.ViewId==v.Id)
                        .SelectMany(p=>p.DimensionLinePoint is {} at?new[]{p.Start,p.End,at}:new[]{p.Start,p.End})).ToArray();
                var b=v.Bounds!;
                double minX=notePoints.Select(p=>p.X).Append(b.MinX).Min(),minY=notePoints.Select(p=>p.Y).Append(b.MinY).Min();
                double maxX=notePoints.Select(p=>p.X).Append(b.MaxX).Max(),maxY=notePoints.Select(p=>p.Y).Append(b.MaxY).Max();
                return new ViewDefinition(v.Id, v.Purpose, v.Origin,
                    new Bounds2Mm(minX-margin,minY-margin,maxX+margin,maxY+margin), v.ScaleDenominator);
            }).ToArray();
            package = new DrawingPackage(package.InputHash, package.RuleVersion, diagnosticViews, package.Primitives, package.Bounds, package.Scope);
            // Retain the actual intent after source notes and hatch datums are applied,
            // so a PDF discrepancy can be traced to the exact rendered coordinates.
            File.WriteAllText(Path.Combine(run,"render-drawing.json"),DrawingPackageJson.Serialize(package,true));
            var roles = package.Primitives.OfType<BlockPrimitive>().GroupBy(p => p.BlockRole)
                .Select(g => new TemplateRequirement(g.Key, AssetKind.Block, g.SelectMany(p => p.Attributes).Select(a => a.Key).Distinct()))
                .Concat(new[] { new TemplateRequirement("Block:Khung", AssetKind.Block, []), new TemplateRequirement("Block:TenMC", AssetKind.Block, ["TIEUDE", "TYLE"]) })
                .GroupBy(r => r.Role)
                .Select(g => new TemplateRequirement(g.Key, AssetKind.Block, g.SelectMany(r => r.RequiredTags).Distinct()))
                .ToArray();
            var templatePath = Path.Combine(run, "scratch-template.dwg");
            if (Environment.GetEnvironmentVariable("DBTOOL_P6_PREPARE") == "1")
            {
                var fontChanges = new List<object>();
                using (document.LockDocument())
                {
                    var sourceDxf = Path.Combine(run,"presentation-source.dxf");
                    if (File.Exists(sourceDxf))
                    {
                        using var sourceDatabase = new Database(false,true);
                        sourceDatabase.DxfIn(sourceDxf,Path.Combine(run,"dxf-import.log"));
                        NormalizeRegularStyle(sourceDatabase,"scratch DXF template",fontChanges);
                        sourceDatabase.SaveAs(templatePath,DwgVersion.Current);
                        Write("dxf-source-receipt.json",new { Path=sourceDxf, SHA256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourceDxf))) });
                    }
                    else
                    {
                        NormalizeRegularStyle(document.Database, "scratch template", fontChanges);
                        document.Database.SaveAs(templatePath, DwgVersion.Current);
                    }
                }
                Write("scratch-font-policy.json", new { Source = "ACAD-VHG-Template.dxf/VHG-Unicode.Regular", Changes = fontChanges });
                return;
            }
            string hash;
            using (var templateStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                hash = Convert.ToHexString(SHA256.HashData(templateStream));
            var read = new TemplateManifestReader().Read(new(templatePath, hash, roles));
            if (!read.Success) { Write("generation-result.json", new { Status = "Blocked", read.Diagnostics }); return; }
            var validated = new TemplateContractValidator().Validate(read.Value!, roles, TemplateValidationMode.ProductionRender);
            if (!validated.Success) { Write("generation-result.json", new { Status = "Blocked", validated.Diagnostics }); return; }
            // Measure native ink in a rolled-back scratch transaction. This is a layout footprint,
            // not a change to geometry, dimensions, marks or calculated quantities.
            Bounds2Mm? nativeFrameBounds=null;
            using(document.LockDocument())
            using(var measurement = document.Database.TransactionManager.StartTransaction())
            {
                var space=(BlockTableRecord)measurement.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database),OpenMode.ForWrite);
                var sink=new DBTool.AutoCAD.Render.CadPrimitiveSink(document.Database,measurement,space,new Point2Mm(0,0));
                var measured=new DBTool.AutoCAD.Render.CadRenderer().Render(package,validated.Value!,sink);
                if(!measured.Success) { Write("generation-result.json",new {Status="Blocked",measured.Diagnostics});return; }
                var ink=new Dictionary<string,List<Extents3d>>();
                foreach(var entity in measured.Value!.Entities)
                {
                    var viewId=package.Primitives.Single(p=>p.SemanticId==entity.SemanticId).ViewId;
                    if(!ink.TryGetValue(viewId,out var extents)) ink[viewId]=extents=[];
                    foreach(var handle in new[]{entity.Handle}.Concat(entity.ChildHandles))
                    {
                        var objectId=document.Database.GetObjectId(false,new Handle(Convert.ToInt64(handle,16)),0);
                        var native=(Entity)measurement.GetObject(objectId,OpenMode.ForRead);
                        if(native is AttributeReference attribute && (attribute.Invisible || string.IsNullOrWhiteSpace(attribute.TextString))) continue;
                        extents.Add(native.GeometricExtents);
                    }
                }
                var nativeViews=package.Views.Select(v=> {
                    if(!ink.TryGetValue(v.Id,out var bounds) || bounds.Count==0 || v.Id is "Bending/Details" or "SteelSchedule") return v;
                    return new ViewDefinition(v.Id,v.Purpose,v.Origin,new Bounds2Mm(
                        bounds.Min(b=>b.MinPoint.X)-v.Origin.X+v.Bounds!.MinX-30,bounds.Min(b=>b.MinPoint.Y)-v.Origin.Y+v.Bounds!.MinY-30,
                        bounds.Max(b=>b.MaxPoint.X)-v.Origin.X+v.Bounds!.MinX+30,bounds.Max(b=>b.MaxPoint.Y)-v.Origin.Y+v.Bounds!.MinY+30),v.ScaleDenominator);
                }).ToArray();
                Write("native-view-bounds.json",nativeViews);
                package=new DrawingPackage(package.InputHash,package.RuleVersion,nativeViews,package.Primitives,package.Bounds,package.Scope);
                var blocks=(BlockTable)measurement.GetObject(document.Database.BlockTableId,OpenMode.ForRead);
                using var frameReference=new BlockReference(
                    new Autodesk.AutoCAD.Geometry.Point3d(0,VhgPresentationStandards.ModelHeightMm,0),blocks["Khung"])
                {ScaleFactors=new Autodesk.AutoCAD.Geometry.Scale3d(VhgPresentationStandards.FrameInsertionScale)};
                space.AppendEntity(frameReference);measurement.AddNewlyCreatedDBObject(frameReference,true);
                var frameDefinition=(BlockTableRecord)measurement.GetObject(blocks["Khung"],OpenMode.ForRead);
                var frameRectangles=frameDefinition.Cast<ObjectId>()
                    .Select(id=>measurement.GetObject(id,OpenMode.ForRead)).OfType<Polyline>()
                    .Where(p=>p.Closed && p.NumberOfVertices==4)
                    .Select(p=> {var e=p.GeometricExtents;e.TransformBy(frameReference.BlockTransform);return e;})
                    .OrderBy(e=>(e.MaxPoint.X-e.MinPoint.X)*(e.MaxPoint.Y-e.MinPoint.Y)).ToArray();
                Write("native-frame-rectangles.json",frameRectangles.Select(e=>new {
                    MinX=e.MinPoint.X/30,MinY=e.MinPoint.Y/30,MaxX=e.MaxPoint.X/30,MaxY=e.MaxPoint.Y/30}));
                if(frameRectangles.Length==0)throw new InvalidOperationException("Native frame has no rectangular interior border");
                var frameInk=frameRectangles[0];
                nativeFrameBounds=new(frameInk.MinPoint.X/30,frameInk.MinPoint.Y/30,
                    frameInk.MaxPoint.X/30,frameInk.MaxPoint.Y/30);
                Write("native-frame-bounds.json",nativeFrameBounds);
                measurement.Abort();
            }
            var assignments = Enum.GetValues<SheetContent>().ToDictionary(c => c, _ => new List<string>());
            bool catchFamily = Environment.GetEnvironmentVariable("DBTOOL_P6_FAMILY") == "3.1";
            bool isLarge = SheetLayoutPlanner.RequiresAdditionalSheets(package);
            var auxiliary = new List<AuxiliarySheetManifestEntry>();
            foreach (var view in package.Views)
            {
                SheetContent? content = view.Id switch
                {
                    "Plan" or "AA" or "BB" or "WallMeshPlan" => SheetContent.Structure,
                    "SteelAA" or "SteelBB" => isLarge ? SheetContent.StructureSteel : SheetContent.Structure,
                    "SteelSchedule" => SheetContent.Tables,
                    "Bending/Details" => SheetContent.Bending,
                    "BaseTop" or "BaseBottom" => isLarge ? SheetContent.BaseSlab : SheetContent.BaseAndWalls12,
                    _ => view.Id.StartsWith("Bending/",StringComparison.Ordinal)?SheetContent.Bending
                        : view.Id.StartsWith("SteelSchedule/Page/",StringComparison.Ordinal)?SheetContent.Tables
                        : view.Id.StartsWith("Reinforcement/Wall/T1/") || view.Id.StartsWith("Reinforcement/Wall/T2/") ? (isLarge ? SheetContent.Walls12 : SheetContent.BaseAndWalls12)
                        : view.Id.StartsWith("Reinforcement/Wall/T3/") || view.Id.StartsWith("Reinforcement/Wall/T4/") ? (isLarge ? SheetContent.Walls34 : SheetContent.Walls34AndRoof)
                        : view.Id.StartsWith("Reinforcement/Roof/") ? (isLarge ? SheetContent.Roof : SheetContent.Walls34AndRoof)
                        : view.Id.StartsWith("Reinforcement/Neck/") ? (catchFamily ? SheetContent.NeckAndCatch : SheetContent.Tables)
                        : catchFamily && view.Id.StartsWith("Reinforcement/Catch/") ? SheetContent.NeckAndCatch : null
                };
                if (content is {} owner) assignments[owner].Add(view.Id);
                else auxiliary.Add(new($"Diagnostic/{auxiliary.Count+1}","Source-only auxiliary view",[view.Id]));
            }
            var plan = SheetPlan.Create(catchFamily ? VhgSheetFamily.VisitAndCatch : VhgSheetFamily.Visit, auxiliary,
                assignments.ToDictionary(p=>p.Key,p=>(IReadOnlyList<string>)p.Value),isLarge);
            // Keep ink inside the actual supplied frame, including a paper gap from its border.
            var frameBounds=nativeFrameBounds ?? throw new InvalidOperationException("Native frame measurement missing");
            var profile = new PresentationProfile(420,297,
                Math.Max(10,frameBounds.MinX+5),Math.Max(10,frameBounds.MinY+5),
                Math.Max(10,420-frameBounds.MaxX+5),Math.Max(10,297-frameBounds.MaxY+5),
                0,5,[30],30,[2,2.5],"P6-Diagnostic");
            Write("sheet-plan.json",plan);
            var arranged=SheetLayoutPlanner.Arrange(package,profile,plan,useVhgSheetTwoSlots:true);
            if(!arranged.Success) { Write("generation-result.json",new {Status="Blocked",isLarge,arranged.Diagnostics});return; }
            var primaryIds=plan.Pages.Where(p=>p.Content!=SheetContent.Auxiliary).SelectMany(p=>p.ViewIds).ToHashSet(StringComparer.Ordinal);
            Write("source-only-views.json",auxiliary);
            package=new DrawingPackage(package.InputHash,package.RuleVersion,package.Views.Where(v=>primaryIds.Contains(v.Id)),
                package.Primitives.Where(p=>primaryIds.Contains(p.ViewId)),package.Bounds,package.Scope);
            var presentationViews = new List<ViewDefinition>();
            var presentationPrimitives = new List<DrawingPrimitive>();
            var decoratedSheets = new List<SheetLayoutSheet>();
            string manholeCode = "G400-1";
            var receiptPath = Path.Combine(run, "presentation-receipt.json");
            if (File.Exists(receiptPath))
            {
                using var receiptDoc = JsonDocument.Parse(File.ReadAllText(receiptPath));
                if (receiptDoc.RootElement.TryGetProperty("ManholeCode", out var prop) && prop.GetString() is { Length: > 0 } code)
                    manholeCode = code;
            }
            foreach (var arrangedSheet in arranged.Value!.Sheets)
            {
                var sheet = arrangedSheet;
                var content=sheet.SourcePlanPageId is {} planId?plan.Pages.Single(p=>p.Id==planId).Content
                    :plan.Pages.Where(p=>p.Content!=SheetContent.Auxiliary).ElementAt(sheet.PageIndex-1).Content;
                string id = $"Presentation/{sheet.PageIndex}";
                // Full-page native assets are validated against a declared model-space envelope.
                var bounds = new Bounds2Mm(-1, -1, 12601, 8911);
                presentationViews.Add(new(id, ViewPurpose.Schematic, new(0, 0), bounds, 30));
                presentationPrimitives.Add(VhgSheetDecoration.Frame(id));
                foreach (var frame in sheet.Frames)
                {
                    string? title = frame.ViewId switch
                    {
                        "Plan" => $"MẶT BẰNG HỐ GA {manholeCode}",
                        "AA" => "MẶT CẮT A-A", "BB" => "MẶT CẮT B-B",
                        "SteelAA" => "MẶT CẮT  CỐT THÉP A-A",
                        "SteelBB" => "CỐT THÉP MẶT CẮT B-B",
                        "WallMeshPlan" => "MẶT BẰNG LƯỚI THÉP TƯỜNG",
                        "BaseTop" => "BẢN ĐÁY - LỚP TRÊN",
                        "BaseBottom" => "BẢN ĐÁY - LỚP DƯỚI",
                        "Reinforcement/Wall/T1/Wall/T1/Outer/Top" => "TƯỜNG T1 - LỚP NGOÀI",
                        "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom" => "TƯỜNG T1 - LỚP TRONG",
                        "Reinforcement/Wall/T2/Wall/T2/Outer/Top" => "TƯỜNG T2 - LỚP NGOÀI",
                        "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom" => "TƯỜNG T2 - LỚP TRONG",
                        "Reinforcement/Wall/T3/Wall/T3/Outer/Top" => "TƯỜNG T3 - LỚP NGOÀI",
                        "Reinforcement/Wall/T3/Wall/T3/Inner/Bottom" => "TƯỜNG T3 - LỚP TRONG",
                        "Reinforcement/Wall/T4/Wall/T4/Outer/Top" => "TƯỜNG T4 - LỚP NGOÀI",
                        "Reinforcement/Wall/T4/Wall/T4/Inner/Bottom" => "TƯỜNG T4 - LỚP TRONG",
                        "Reinforcement/Roof/Roof/Top/Outer/Top" => "TRẦN - LỚP TRÊN",
                        "Reinforcement/Roof/Roof/Bottom/Inner/Bottom" => "TRẦN - LỚP DƯỚI",
                        "Reinforcement/Neck/Plan" => "MẶT BẰNG THÉP CỔ GA",
                        "Reinforcement/Neck/AA" => "MẶT CẮT A-A",
                        "Reinforcement/Neck/BB" => "MẶT CẮT B-B",
                        "Reinforcement/Catch/Plan" => "MẶT BẰNG CỬA THU",
                        "Reinforcement/Catch/1-1" => "MẶT CẮT 1-1",
                        "Reinforcement/Catch/2-2" => "MẶT CẮT 2-2",
                        _ => null
                    };
                    if (title != null)
                    {
                        Point2Mm titlePos;
                        if (frame.Label != "VHG named slots 1:30" && content == SheetContent.Structure && !isLarge && SheetOnePlacement.Title(frame, package) is { } tp1)
                            titlePos = tp1;
                        else if (content == SheetContent.Structure && isLarge)
                            titlePos = frame.ViewId switch
                            {
                                "Plan" => new(3250, 8610),
                                "WallMeshPlan" => new(9350, 8610),
                                "AA" => new(3250, 4450),
                                "BB" => new(9350, 4450),
                                _ => new((frame.PaperBounds.MinX + frame.PaperBounds.MaxX) * 15, Math.Min(8610, frame.PaperBounds.MaxY * 30 + 400))
                            };
                        else if (content == SheetContent.StructureSteel && isLarge)
                            titlePos = frame.ViewId == "SteelAA" ? new(3250, 8610) : new(9350, 8610);
                        else if (frame.Label != "VHG named slots 1:30" && content == SheetContent.BaseAndWalls12 && SheetTwoPlacement.Title(frame.ViewId, package, manholeCode) is { } tp2)
                            titlePos = tp2;
                        else if (frame.Label != "VHG named slots 1:30" && content == SheetContent.Walls34AndRoof && SheetThreePlacement.Title(frame.ViewId, package, manholeCode) is { } tp3)
                            titlePos = tp3;
                        else if (content is SheetContent.NeckAndCatch or SheetContent.Tables)
                            titlePos = SheetFourPlacement.Title(frame,30);
                        else if (content == SheetContent.Roof && isLarge)
                            titlePos = frame.ViewId.Contains("Top") ? new(3250, 8610) : new(9350, 8610);
                        else
                            titlePos = new((frame.PaperBounds.MinX + frame.PaperBounds.MaxX) * 15, Math.Min(8610, frame.PaperBounds.MaxY * 30 + 400));

                        presentationPrimitives.Add(VhgSheetDecoration.Title(id, frame.ViewId, title, titlePos,frame.Transform.UniformScaleDenominator));
                    }
                }
                var decorationFrame = new SheetViewFrame(id, ViewPurpose.Schematic, bounds,
                    new(-1d/30, -1d/30, 12601d/30, 8911d/30), new(new(-1d/30, -1d/30), 30),
                    "Native presentation", ["Frame", "ViewTitle"]);
                decoratedSheets.Add(new(sheet.PageIndex, sheet.PageCount, sheet.PrintableBounds,
                    sheet.Frames.Append(decorationFrame),sheet.SourcePlanPageId));
            }
            package = new DrawingPackage(package.InputHash, package.RuleVersion,
                package.Views.Concat(presentationViews), package.Primitives.Concat(presentationPrimitives), package.Bounds, package.Scope);
            var decoratedLayout = new SheetLayout(arranged.Value.ThemeId, decoratedSheets);
            if (int.TryParse(Environment.GetEnvironmentVariable("DBTOOL_P6_PAGE"),out var selectedPage) && selectedPage >= 1 && selectedPage <= decoratedLayout.Sheets.Length)
            {
                var first = decoratedLayout.Sheets.Single(s=>s.PageIndex==selectedPage);
                var ids = first.Frames.Select(f => f.ViewId).ToHashSet(StringComparer.Ordinal);
                package = new DrawingPackage(package.InputHash, package.RuleVersion, package.Views.Where(v=>ids.Contains(v.Id)),
                    package.Primitives.Where(p=>ids.Contains(p.ViewId)), package.Bounds, package.Scope);
                // A one-page scratch artifact starts at page1. Keep the requested
                // source page in the receipt/PDF name, not in the contiguous layout index.
                decoratedLayout = new SheetLayout(decoratedLayout.ThemeId, [new SheetLayoutSheet(1,
                    1, first.PrintableBounds, first.Frames)]);
            }
            using (document.LockDocument())
            using (var tr = document.Database.TransactionManager.StartTransaction())
            {
                var space = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(document.Database), OpenMode.ForRead);
                foreach (ObjectId id in space) tr.GetObject(id, OpenMode.ForWrite).Erase();
                tr.Commit();
            }
            var repository = new GenerationRepository();
            var writer = new CadGenerationWriter(document, repository, layout: decoratedLayout);
            var result = writer.WriteAsync(package, validated.Value!,
                new(GenerationMode.Create, repository.GetDocumentToken(document), null, package.InputHash, new(0, 0)), default).GetAwaiter().GetResult();
            if (!result.Success) { Write("generation-result.json", new { Status = "Blocked", result.Diagnostics }); return; }
            using (document.LockDocument()) document.Database.SaveAs(Path.Combine(run, $"{manholeCode}.dwg"), DwgVersion.Current);
            Write("layout.json", decoratedLayout);
            Write("generation-result.json", new { Status = "DiagnosticPartial", Scope = "Diagnostic drawing; fixed 1:30 scale; additional pages retain all assigned views", LargeStructureSheet = isLarge, ScaleDenominator = 30, LargeThresholdMm = 2200, AnnotationEnvelopeMm = new { Structure = 800, WallMeshPlan = 900, Table = 100, Other = 500 }, AuxiliaryViewGroups = auxiliary.Count, PrimaryPages = decoratedLayout.Sheets.Length, AdditionalPages = isLarge ? 1 : 0, TotalPages = decoratedLayout.Sheets.Length, SelectedPage = Environment.GetEnvironmentVariable("DBTOOL_P6_PAGE"), result.Value, result.Diagnostics });
            Write("plot-manifest.json", new
            {
                dwg = Path.Combine(run, $"{manholeCode}.dwg"),
                plot_profile = new { media = "ISO full bleed A3 (420.00 x 297.00 MM)", scale_denominator = 30,
                    plot_style_table = document.Database.PlotStyleMode ? "acad.ctb" : "acad.stb",
                    regular_font_file = "arial.ttf",
                    default_lineweight_hundredths_mm = 0,
                    plot_lineweights = false,
                    evidence = "reference-visit-v7 and reference-sheet2-v71 Model PrintLineweights=false; golden Model-1/Model-2/Model-3 vector widths verified zero; selected Sheet1/2/3 only" },
                pages = decoratedLayout.Sheets.Select(s => new
                {
                    id = $"{(catchFamily ? "3.1" : "2.1")}/{(selectedPage >= 1 ? selectedPage : s.PageIndex)}",
                    output = Path.Combine(run, $"{manholeCode}-{(catchFamily ? "3.1" : "2.1")}-Model-{(selectedPage >= 1 ? selectedPage : s.PageIndex)}.pdf"),
                    window = new double[] { (s.PageIndex - 1) * 12600, 0, s.PageIndex * 12600, 8910 }
                })
            });
        }
        catch (System.Exception ex) { Write("generation-result.json", new { Status = "Failed", Error = ex.ToString() }); }
    }

    private static void NormalizeRegularStyle(Database database, string target, List<object> changes)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var styles = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);
        if (!styles.Has(VhgPresentationStandards.RegularTextStyle))
            throw new InvalidOperationException("Required native regular style missing");
        var style = (TextStyleTableRecord)transaction.GetObject(styles[VhgPresentationStandards.RegularTextStyle], OpenMode.ForWrite);
        changes.Add(new { Target = target, style.Name, Before = style.FileName, After = "arial.ttf",
            Evidence = "Original VHG Unicode regular font; Vietnamese TYLE text must retain every glyph" });
        style.FileName = "arial.ttf";
        style.Font=new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor("Arial",false,false,0,34);
        transaction.Commit();
    }
}
