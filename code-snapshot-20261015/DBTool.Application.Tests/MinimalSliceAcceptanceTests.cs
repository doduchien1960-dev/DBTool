using System.Text.Json;
using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class MinimalSliceAcceptanceTests
{
    private const string Id = "S1/GaThamLienKhoi/S1-MONO-001";

    [Fact]
    public async Task PortOrchestrationPreservesFrozenExpectedAndPartialScope()
    {
        using var expected = LoadExpected();
        var harness = new Harness();
        var result = await harness.UseCase.CalculateAsync(Read, Request(), Presentation(20), default);

        Assert.True(result.Success, string.Join("; ", result.Diagnostics.Select(d => d.Code)));
        var package = result.Value!;
        var expectedRoot = expected.RootElement;
        var expectedBars = expectedRoot.GetProperty("bars");
        var expectedSchedule = expectedRoot.GetProperty("steelSchedule");

        Assert.Equal(expectedBars.GetArrayLength(), package.Occurrences.Length);
        Assert.Equal(expectedSchedule.GetProperty("totalLengthMm").GetDouble(),
            package.Occurrences.Sum(bar => bar.ScheduleLengthMm), 6);
        Assert.Equal(expectedSchedule.GetProperty("totalMassKg").GetDouble(),
            package.SteelSchedule!.TotalMassKg.Value, 12);
        Assert.Equal(CalculationScope.BaseRebarOnly, package.Scope);
        Assert.Equal(CalculationStatus.NotCalculated, package.ConcreteStatus);
        Assert.Null(package.ConcreteLines);
        Assert.Equal(CalculationStatus.NotCalculated, package.FormworkStatus);
        Assert.Null(package.FormworkLines);
        Assert.Equal(6, package.DrawingPackage!.Views.Length);
        Assert.Equal((1, 1, 1, 1, 1),
            (harness.Reads, harness.Catalogs, harness.Geometries, harness.Rebars, harness.Schedules));
        Assert.DoesNotContain(package.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public async Task GlobalCfGaBlockStopsAllPortStagesAndReturnsNullPackage()
    {
        var source = ManholeValidatorTests.Cell(null, "C17", "CF.Ga").Source;
        var globalBlock = new Diagnostic("CF_GA_GLOBAL_BLOCK", DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, source, null, null, "Synthetic CF.Ga block for T42.a acceptance.");
        var harness = new Harness(ManholeValidatorTests.Raw(diagnostics: [globalBlock]));

        var result = await harness.UseCase.CalculateAsync(Read, Request(), Presentation(20), default);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "CF_GA_GLOBAL_BLOCK" &&
            diagnostic.Scope == DiagnosticScope.Global && diagnostic.Severity == DiagnosticSeverity.Block);
        Assert.Equal((1, 0, 0, 0, 0),
            (harness.Reads, harness.Catalogs, harness.Geometries, harness.Rebars, harness.Schedules));
    }

    [Fact]
    public async Task PresentationScaleDoesNotChangeCalculationIdentityOrMass()
    {
        var first = new Harness();
        var second = new Harness();
        var at20 = (await first.UseCase.CalculateAsync(Read, Request(), Presentation(20), default)).Value!;
        var at30 = (await second.UseCase.CalculateAsync(Read, Request(), Presentation(30), default)).Value!;

        Assert.Equal(at20.InputHash, at30.InputHash);
        Assert.Equal(at20.SourceWorkbookHash, at30.SourceWorkbookHash);
        Assert.Equal(at20.SteelSchedule!.TotalMassKg, at30.SteelSchedule!.TotalMassKg);
        Assert.Equal(20, at20.DrawingPackage!.Views[0].ScaleDenominator);
        Assert.Equal(30, at30.DrawingPackage!.Views[0].ScaleDenominator);
    }

    private static CalculationRequest Request() =>
        new("S1", [Id], CalculationScope.BaseRebarOnly, "S1-Analytic-1", true);

    private static PresentationRequest Presentation(double scale) =>
        new(scale, ElevationMode.Absolute, new ElevationM(10), "S1", true);

    private static WorkbookReadRequest Read =>
        new("S1", "synthetic-s1.xlsx", Profile.GaThamLienKhoi, "GaThamLienKhoi");

    private static JsonDocument LoadExpected() =>
        JsonDocument.Parse(File.ReadAllText(WorkspacePath("DBTool/Tests/Fixtures/s1-mono-001.expected.json")));

    private static string WorkspacePath(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }
        throw new FileNotFoundException(relativePath);
    }

    private sealed class Harness : IWorkbookImporter, ICatalogReader, IGeometryStage, IBaseRebarStage,
        ISteelScheduleStage, IDrawingStage
    {
        private readonly RawProjectInput raw;
        internal int Reads;
        internal int Catalogs;
        internal int Geometries;
        internal int Rebars;
        internal int Schedules;
        internal CalculateManholeUseCase UseCase { get; }

        internal Harness(RawProjectInput? raw = null)
        {
            this.raw = raw ?? ManholeValidatorTests.Raw();
            var numeric = new NumericPolicy("en-US", false, false);
            UseCase = new(this, this, new ManholeValidator(new NumericCellReader(), numeric), numeric,
                new CalculationPipeline(this, this, this, this));
        }

        public OperationResult<RawProjectInput> Read(WorkbookReadRequest request)
        {
            Reads++;
            return new(true, raw, []);
        }

        public OperationResult<CatalogSet> Read(RawProjectInput input, NumericPolicy policy)
        {
            Catalogs++;
            return new(true, ManholeValidatorTests.Catalogs(), []);
        }

        public OperationResult<GeometryModel> Build(ValidatedManholeSpec spec, CalculationPolicy policy)
        {
            Geometries++;
            return new GeometryStageAdapter().Build(spec, policy);
        }

        public OperationResult<RebarLayout> Plan(ValidatedManholeSpec spec, GeometryModel geometry,
            CalculationPolicy policy)
        {
            Rebars++;
            return new BaseRebarStageAdapter().Plan(spec, geometry, policy);
        }

        public OperationResult<SteelSchedule> Calculate(RebarLayout layout, CalculationPolicy policy)
        {
            Schedules++;
            return new SteelScheduleStageAdapter().Calculate(layout, policy);
        }

        public OperationResult<DrawingPackage> Build(ValidatedManholeSpec spec, DrawingInputs inputs,
            PresentationRequest presentation, PackageIdentity identity) =>
            new DrawingStageAdapter().Build(spec, inputs, presentation, identity);
    }
}
