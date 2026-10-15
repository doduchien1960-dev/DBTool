using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.AutoCAD.Generation;
using DBTool.AutoCAD.Render;
using DBTool.AutoCAD.Template;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using AutodeskApplication = Autodesk.AutoCAD.ApplicationServices.Application;

namespace DBTool.AutoCAD.Tests;

public sealed class Session08HostCommands
{
    private const string InputHash = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [CommandMethod("DBTOOL_TEST_WRITE_SESSION", CommandFlags.Session)]
    public void TestWriteSession()
    {
        var outputPath = SessionOutputPath("write-session");
        var document = AutodeskApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            Write(outputPath, new { ExitStatus = "Block", Diagnostic = "CAD_DOCUMENT_MISSING" });
            return;
        }

        var template = ReadTemplate();
        if (!template.Success)
        {
            Write(outputPath, new
            {
                ExitStatus = "Block",
                Template = Diagnostics(template.Diagnostics)
            });
            return;
        }

        var package = FixturePackage();
        var repository = new GenerationRepository();
        var writer = new DBTool.AutoCAD.CadGenerationWriter(document, repository);
        var documentToken = repository.GetDocumentToken(document);
        var modelSpaceBefore = CountModelSpace(document);

        var create = writer.WriteAsync(package, template.Value!,
            new GenerationRequest(GenerationMode.Create, documentToken, null, InputHash, new Point2Mm(0, 0), "session08-create"),
            CancellationToken.None).GetAwaiter().GetResult();
        var firstHandles = create.Value is null ? [] : repository.ReadOwnedHandles(document, create.Value.GenerationId);

        var replace = create.Success
            ? writer.WriteAsync(package, template.Value!,
                new GenerationRequest(GenerationMode.Replace, documentToken, create.Value!.GenerationId, InputHash, new Point2Mm(0, 0)),
                CancellationToken.None).GetAwaiter().GetResult()
            : null;
        var secondHandles = replace?.Value is null ? [] : repository.ReadOwnedHandles(document, replace.Value.GenerationId);

        var beforeFault = replace?.Value is null ? [] : repository.ReadOwnedHandles(document, replace.Value.GenerationId);
        var faultRequest = new GenerationRequest(GenerationMode.Create, documentToken, null, InputHash,
            new Point2Mm(0, 0), "session08-fault");
        var faultSession = new CadWriteSession(repository, InputHash, template.Value!.Manifest.SourceFileHash);
        var fault = faultSession.Run(document, faultRequest, context =>
        {
            context.Sink.AppendHatch(
                [new Point2Mm(0, 0), new Point2Mm(100, 0), new Point2Mm(100, 100), new Point2Mm(0, 100)],
                [], new CadPrimitiveStyle("VHG-BT", HatchPattern: "SOLID"));
            throw new InvalidOperationException("SESSION08_INJECTED_FAULT");
        }, CancellationToken.None);
        var afterFault = replace?.Value is null ? [] : repository.ReadOwnedHandles(document, replace.Value.GenerationId);

        var undo = TryUndo(document);
        var undoHandles = replace?.Value is null ? [] : repository.ReadOwnedHandles(document, replace.Value.GenerationId);
        var redo = TryRedo(document);
        var redoHandles = replace?.Value is null ? [] : repository.ReadOwnedHandles(document, replace.Value.GenerationId);

        Write(outputPath, new
        {
            ExitStatus = create.Success && replace?.Success == true && !fault.Success ? "Success" : "Block",
            DocumentToken = documentToken,
            ModelSpaceCountBefore = modelSpaceBefore,
            Create = Result(create),
            Replace = replace is null ? null : Result(replace),
            CreateHandles = firstHandles,
            ReplaceHandles = secondHandles,
            Fault = Result(fault),
            FaultPreservedPreviousGeneration = beforeFault.SequenceEqual(afterFault, StringComparer.Ordinal),
            Undo = undo,
            UndoHandles = undoHandles,
            Redo = redo,
            RedoHandles = redoHandles,
            TemplateHash = template.Value!.Manifest.SourceFileHash,
            InputHash,
            SnapshotVersion = GenerationRepository.SnapshotVersion
        });
    }

    [CommandMethod("DBTOOL_TEST_RENDER", CommandFlags.Session)]
    public void TestRender()
    {
        var outputPath = SessionOutputPath("render");
        var document = AutodeskApplication.DocumentManager.MdiActiveDocument;
        if (document is null)
        {
            Write(outputPath, new { ExitStatus = "Block", Diagnostic = "CAD_DOCUMENT_MISSING" });
            return;
        }

        var template = ReadTemplate();
        if (!template.Success)
        {
            Write(outputPath, new { ExitStatus = "Block", Template = Diagnostics(template.Diagnostics) });
            return;
        }

        var package = FixturePackage();
        var repository = new GenerationRepository();
        var writer = new DBTool.AutoCAD.CadGenerationWriter(document, repository);
        var documentToken = repository.GetDocumentToken(document);
        var before = CountModelSpace(document);
        var result = writer.WriteAsync(package, template.Value!,
            new GenerationRequest(GenerationMode.Create, documentToken, null, InputHash, new Point2Mm(15000, 0), "session08-render"),
            CancellationToken.None).GetAwaiter().GetResult();
        var after = CountModelSpace(document);
        var generationId = result.Value?.GenerationId;
        var actualAttributes = generationId is null
            ? new Dictionary<string, string>()
            : repository.ReadOwnedAttributeValues(document, generationId);
        var missingBlock = ProbePreflight(document, repository, template.Value!,
            PackageWithBlockRole(package, "MissingBlock"),
            AddAsset(template.Value!.Manifest, new TemplateAsset("MissingBlock", AssetKind.Block,
                "SESSION08_MISSING_BLOCK", true, [], template.Value!.Manifest.Assets.First().VerificationSource)),
            [new TemplateRequirement("SteelSummary", AssetKind.Block, ["TONG"]),
             new TemplateRequirement("MissingBlock", AssetKind.Block, [])],
            "missing-block");
        var missingPattern = ProbePreflight(document, repository, template.Value!, package,
            AddAsset(template.Value!.Manifest, new TemplateAsset("Hatch:Missing", AssetKind.Hatch,
                "SESSION08_MISSING_HATCH", true, [], template.Value!.Manifest.Assets.First().VerificationSource)),
            [new TemplateRequirement("SteelSummary", AssetKind.Block, ["TONG"]),
             new TemplateRequirement("Hatch:Missing", AssetKind.Hatch, [])],
            "missing-pattern");
        var missingAttribute = ProbePreflight(document, repository, template.Value!,
            PackageWithoutAttribute(package, "TONG"), template.Value!.Manifest,
            [new TemplateRequirement("SteelSummary", AssetKind.Block, ["TONG"])], "missing-attribute");

        Write(outputPath, new
        {
            ExitStatus = result.Success ? "Success" : "Block",
            ModelSpaceCountBefore = before,
            ModelSpaceCountAfter = after,
            Result = Result(result),
            EntityCount = result.Value?.EntityMap.Entities.Length ?? 0,
            ActualSummaryTag = actualAttributes.GetValueOrDefault("TONG"),
            MissingBlock = missingBlock,
            MissingPattern = missingPattern,
            MissingRequiredAttribute = missingAttribute,
            SummaryTagExpected = "TONG",
            SummaryTagValue = "46.062",
            HatchAreaExpectedMm2 = 840000d,
            TemplateHash = template.Value!.Manifest.SourceFileHash,
            InputHash
        });
    }

    private static OperationResult<ValidatedTemplateManifest> ReadTemplate()
    {
        var workspace = FindWorkspace();
        var path = Environment.GetEnvironmentVariable("DBTOOL_TEMPLATE_PATH")
            ?? Path.Combine(workspace, "VHGpro 3.2", "Template", "VHG-Template.dwg");
        var expected = Environment.GetEnvironmentVariable("DBTOOL_TEMPLATE_SHA256");
        var writeProfileRoles = new[]
        {
            new TemplateRequirement("SteelSummary", AssetKind.Block, ["TONG"])
        };
        var read = new TemplateManifestReader().Read(new TemplateReadRequest(path, expected,
            writeProfileRoles, "VHGpro 3.2/Template/VHG-Template.dwg"));
        if (!read.Success) return new(false, null, read.Diagnostics);
        return new TemplateContractValidator().Validate(read.Value!, writeProfileRoles,
            TemplateValidationMode.ProductionRender);
    }

    internal static DrawingPackage FixturePackage() => new(
        InputHash,
        "session-08-render/v1",
        [new ViewDefinition("plan", ViewPurpose.Plan, new Point2Mm(0, 0), new Bounds2Mm(0, 0, 13000, 13000), 20)],
        [
            new PathPrimitive("line-1", "VHG-BT", "plan", null, null, null,
                [new LinePathSegment(new Point2Mm(0, 0), new Point2Mm(100, 0))], false, ViewPurpose.Plan, 100, DrawingMaterialKind.Concrete),
            new PathPrimitive("line-2", "VHG-BT", "plan", null, null, null,
                [new LinePathSegment(new Point2Mm(100, 0), new Point2Mm(100, 100))], false, ViewPurpose.Plan, 100, DrawingMaterialKind.Concrete),
            new CirclePrimitive("arc-1", "VHG-BT", "plan", null, null, null, new Point2Mm(500, 500), 100),
            new TextPrimitive("text-1", "VHG-Text", "plan", null, null, null, new Point2Mm(10, 10), "SESSION08", 2.5, 0),
            new HatchPrimitive("hatch-1", "VHG-BT", "plan", null,
                [new Point2Mm(0, 0), new Point2Mm(1000, 0), new Point2Mm(1000, 1000), new Point2Mm(0, 1000)],
                [[new Point2Mm(300, 300), new Point2Mm(700, 300), new Point2Mm(700, 700), new Point2Mm(300, 700)]],
                DrawingMaterialKind.Concrete),
            new BlockPrimitive("summary-1", "VHG-Thep", "plan", null, "SteelSummary", new Point2Mm(2000, 0), 1,
                [new("CO1", "0"), new("CO2", "0"), new("DAY1", "0"), new("DAY2", "0"),
                 new("TONG", "46.062"), new("TRAN1", "0"), new("TRAN2", "0"), new("TUONG1", "0"), new("TUONG2", "0")]),
            new DimensionIntentPrimitive("dimension-1", "VHG-Dim", "plan", null, null, null,
                new Point2Mm(0, 1500), new Point2Mm(1000, 1500), 1000, "1000", 100)
        ],
        new Bounds2Mm(0, 0, 13000, 13000),
        CalculationScope.GeometryOnly);

    private static DrawingPackage PackageWithBlockRole(DrawingPackage package, string blockRole) => new(
        package.InputHash, package.RuleVersion,
        package.Views,
        package.Primitives.Select(primitive => primitive is BlockPrimitive block
            ? new BlockPrimitive(block.SemanticId, block.Role, block.ViewId, block.ComponentId, blockRole,
                block.Insertion, block.AnnotationScale, block.Attributes)
            : primitive),
        package.Bounds, package.Scope);

    private static DrawingPackage PackageWithoutAttribute(DrawingPackage package, string attributeTag) => new(
        package.InputHash, package.RuleVersion,
        package.Views,
        package.Primitives.Select(primitive => primitive is BlockPrimitive block
            ? new BlockPrimitive(block.SemanticId, block.Role, block.ViewId, block.ComponentId, block.BlockRole,
                block.Insertion, block.AnnotationScale,
                block.Attributes.Where(attribute => !string.Equals(attribute.Key, attributeTag, StringComparison.OrdinalIgnoreCase)))
            : primitive),
        package.Bounds, package.Scope);

    private static TemplateManifest AddAsset(TemplateManifest manifest, TemplateAsset asset) => new(
        manifest.SchemaVersion, manifest.SourceFileHash, manifest.InspectionStatus,
        manifest.Assets.Concat([asset]), manifest.Inventory);

    private static object ProbePreflight(Document document, GenerationRepository repository,
        ValidatedTemplateManifest baseTemplate, DrawingPackage package, TemplateManifest manifest,
        IReadOnlyList<TemplateRequirement> requirements, string id)
    {
        var validation = new TemplateContractValidator().Validate(manifest, requirements,
            TemplateValidationMode.ProductionRender);
        if (!validation.Success)
            return new { Success = false, Value = (string?)null, Diagnostics = Diagnostics(validation.Diagnostics), WritesPreserved = true };

        var before = CountModelSpace(document);
        var writer = new DBTool.AutoCAD.CadGenerationWriter(document, repository);
        var result = writer.WriteAsync(package, validation.Value!,
            new GenerationRequest(GenerationMode.Create, repository.GetDocumentToken(document), null, InputHash,
                new Point2Mm(30000, 0), $"session08-{id}"), CancellationToken.None).GetAwaiter().GetResult();
        var after = CountModelSpace(document);
        return new
        {
            result.Success,
            Value = result.Value is null ? null : result.Value.GenerationId,
            Diagnostics = Diagnostics(result.Diagnostics),
            WritesPreserved = before == after
        };
    }

    private static object TryUndo(Document document)
    {
        if (string.Equals(System.Diagnostics.Process.GetCurrentProcess().ProcessName, "accoreconsole", StringComparison.OrdinalIgnoreCase))
            return new { Status = "UnsupportedInCoreConsole" };
        try
        {
            document.Editor.Command("_.UNDO", "_One");
            return new { Status = "Executed" };
        }
        catch (System.Exception ex)
        {
            return new { Status = "Unavailable", Error = ex.Message };
        }
    }

    private static object TryRedo(Document document)
    {
        if (string.Equals(System.Diagnostics.Process.GetCurrentProcess().ProcessName, "accoreconsole", StringComparison.OrdinalIgnoreCase))
            return new { Status = "UnsupportedInCoreConsole" };
        try
        {
            document.Editor.Command("_.REDO", "_One");
            return new { Status = "Executed" };
        }
        catch (System.Exception ex)
        {
            return new { Status = "Unavailable", Error = ex.Message };
        }
    }

    private static int CountModelSpace(Document document)
    {
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        var count = modelSpace.Cast<ObjectId>().Count();
        transaction.Commit();
        return count;
    }

    private static object Result<T>(OperationResult<T> result) where T : class => new
    {
        result.Success,
        Value = result.Value is null ? null : result.Value.GetType().Name,
        Diagnostics = Diagnostics(result.Diagnostics)
    };

    private static object[] Diagnostics(IEnumerable<Diagnostic> diagnostics) => diagnostics.Select(d => new
    {
        d.Code,
        Severity = d.Severity.ToString(),
        Scope = d.Scope.ToString(),
        d.Message
    }).Cast<object>().ToArray();

    private static string SessionOutputPath(string name)
    {
        var workspace = FindWorkspace();
        var root = Path.Combine(Environment.GetEnvironmentVariable("DBTOOL_SESSION_OUTPUT_ROOT") ?? Path.Combine(workspace, "DBTool", "TestOutput", "Session-08"),
            $"host-{DateTime.UtcNow:yyyyMMddTHHmmssfff}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return Path.Combine(root, $"{name}.json");
    }

    private static void Write(string path, object payload) => File.WriteAllText(path,
        JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } }));

    private static string FindWorkspace()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(Session08HostCommands).Assembly.Location)!);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "templates", "template-manifest.production.json")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("DBTool workspace could not be located from the harness assembly.");
    }
}
