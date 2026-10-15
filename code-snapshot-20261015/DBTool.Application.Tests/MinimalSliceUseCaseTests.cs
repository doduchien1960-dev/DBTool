using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class MinimalSliceUseCaseTests
{
    private const string Id = "S1/GaThamLienKhoi/S1-MONO-001";
    private static CalculationRequest Request(CalculationScope scope = CalculationScope.BaseRebarOnly, string id = Id) => new("S1", [id], scope, "S1-Analytic-1", true);
    private static PresentationRequest Presentation(double scale = 20) => new(scale, ElevationMode.Absolute, new ElevationM(10), "S1", true);
    private static WorkbookReadRequest Read => new("S1", "synthetic.xlsx", Profile.GaThamLienKhoi, "GaThamLienKhoi");

    [Fact]
    public async Task CalculateAndTwoExportsComputeEachStageOnceAndPreservePackage()
    {
        var harness = new Harness();
        var result = await harness.UseCase.CalculateAsync(Read, Request(), Presentation(), default);
        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code)));
        var package = result.Value!;
        Assert.Equal(48, package.Occurrences.Length);
        Assert.Equal(74620, package.Occurrences.Sum(o => o.ScheduleLengthMm), 6);
        Assert.InRange(package.SteelSchedule!.TotalMassKg.Value, 46.06172839406173, 46.06172839606173);
        Assert.Equal(6, package.DrawingPackage!.Views.Length);
        Assert.Equal(package.InputHash, package.DrawingPackage.InputHash);
        Assert.Equal(CalculationStatus.NotCalculated, package.ConcreteStatus);
        var export = new ExportCalculationUseCase(harness);
        Assert.True((await export.ExportAsync(package, new("unused", null, "steel", "run"), default)).Success);
        Assert.True((await export.ExportAsync(package, new("unused", null, "steel", "run2"), default)).Success);
        Assert.Equal((1, 1, 1, 1, 1, 2), (harness.Reads, harness.Geometries, harness.Rebars, harness.Schedules, harness.Drawings, harness.Writes));
        Assert.Same(package, harness.LastWritten);
    }

    [Fact]
    public async Task GlobalConfigBlockPreventsAllEnginesAndWriter()
    {
        var d = new Diagnostic("CONFIG_BAD", DiagnosticSeverity.Block, DiagnosticScope.Global, null, null,
            ManholeValidatorTests.Cell(null, "C17", "CF.Ga").Source, null, null, "Bad config");
        var h = new Harness(ManholeValidatorTests.Raw(diagnostics: [d]));
        var result = await h.UseCase.CalculateAsync(Read, Request(), Presentation(), default);
        Failed(result, "CONFIG_BAD"); Assert.Equal((0,0,0,0), (h.Geometries,h.Rebars,h.Schedules,h.Writes));
    }

    [Theory]
    [InlineData(CalculationScope.Full, Id, "S1_SCOPE_UNSUPPORTED")]
    [InlineData(CalculationScope.BaseRebarOnly, "S1/GaThamLienKhoi/MISSING", "MANHOLE_NOT_FOUND")]
    public async Task UnsupportedScopeAndMissingSelectionNeverCompute(CalculationScope scope, string id, string code)
    {
        var h = new Harness(); Failed(await h.UseCase.CalculateAsync(Read, Request(scope,id), Presentation(), default), code);
        Assert.Equal(0,h.Geometries);
    }

    [Fact]
    public async Task MultipleSelectedIdsAreNotSilentlyDropped()
    {
        var h=new Harness();
        Failed(await h.UseCase.CalculateAsync(Read,new("S1",[Id,"S1/GaThamLienKhoi/B"],CalculationScope.BaseRebarOnly,"S1-Analytic-1",true),Presentation(),default),"S1_SINGLE_MANHOLE_ONLY");
        Assert.Equal(0,h.Geometries);
    }

    [Fact]
    public async Task PresentationScaleDoesNotChangeCalculationOrSourceHash()
    {
        var h=new Harness();
        var a=(await h.UseCase.CalculateAsync(Read,Request(),Presentation(20),default)).Value!;
        var b=(await h.UseCase.CalculateAsync(Read,Request(),Presentation(30),default)).Value!;
        Assert.Equal(a.InputHash,b.InputHash); Assert.Equal(a.SourceWorkbookHash,b.SourceWorkbookHash);
        Assert.Equal(a.SteelSchedule!.TotalMassKg,b.SteelSchedule!.TotalMassKg);
        Assert.Equal(20,a.DrawingPackage!.Views[0].ScaleDenominator); Assert.Equal(30,b.DrawingPackage!.Views[0].ScaleDenominator);
    }

    [Fact]
    public async Task CancellationBeforeComputeAndAfterGeometryReturnsNoPackage()
    {
        using var cts=new CancellationTokenSource(); cts.Cancel(); var h=new Harness();
        Failed(await h.UseCase.CalculateAsync(Read,Request(),Presentation(),cts.Token),"OPERATION_CANCELLED");
        Assert.Equal(0,h.Reads);
        using var during=new CancellationTokenSource(); h.AfterGeometry=()=>during.Cancel();
        Failed(await h.UseCase.CalculateAsync(Read,Request(),Presentation(),during.Token),"OPERATION_CANCELLED");
        Assert.Equal(0,h.Rebars);
    }

    [Fact]
    public async Task WriterFailureLeavesCompletedPackageForRetryWithoutCompute()
    {
        var h=new Harness(); var package=(await h.UseCase.CalculateAsync(Read,Request(),Presentation(),default)).Value!;
        var exporter=new ExportCalculationUseCase(h); h.ThrowWrite=true;
        Failed(await exporter.ExportAsync(package,new("unused",null,"steel","one"),default),"EXPORT_WRITE_FAILED");
        Assert.Equal(48,package.Occurrences.Length); h.ThrowWrite=false;
        Assert.True((await exporter.ExportAsync(package,new("unused",null,"steel","two"),default)).Success);
        Assert.Equal(1,h.Geometries); Assert.Equal(1,h.Rebars); Assert.Equal(1,h.Schedules);
    }

    private static void Failed<T>(OperationResult<T> result,string code) where T:class
    { Assert.False(result.Success); Assert.Null(result.Value); Assert.Contains(result.Diagnostics,d=>d.Code==code&&d.Severity==DiagnosticSeverity.Block); }

    private sealed class Harness : IWorkbookImporter,ICatalogReader,IGeometryStage,IBaseRebarStage,ISteelScheduleStage,IDrawingStage,ISteelWorkbookWriter
    {
        private readonly RawProjectInput raw;
        internal int Reads,Geometries,Rebars,Schedules,Drawings,Writes;
        internal Action? AfterGeometry; internal bool ThrowWrite; internal CalculationPackage? LastWritten;
        internal CalculateManholeUseCase UseCase {get;}
        internal Harness(RawProjectInput? raw=null)
        {
            this.raw=raw??ManholeValidatorTests.Raw(); var numeric=new NumericPolicy("en-US",false,false);
            UseCase=new(this,this,new ManholeValidator(new NumericCellReader(),numeric),numeric,new CalculationPipeline(this,this,this,this));
        }
        public OperationResult<RawProjectInput> Read(WorkbookReadRequest r){Reads++;return new(true,raw,[]);}
        public OperationResult<CatalogSet> Read(RawProjectInput r,NumericPolicy n)=>new(true,ManholeValidatorTests.Catalogs(),[]);
        public OperationResult<GeometryModel> Build(ValidatedManholeSpec s,CalculationPolicy p){Geometries++;var result=new GeometryStageAdapter().Build(s,p);AfterGeometry?.Invoke();return result;}
        public OperationResult<RebarLayout> Plan(ValidatedManholeSpec s,GeometryModel g,CalculationPolicy p){Rebars++;return new BaseRebarStageAdapter().Plan(s,g,p);}
        public OperationResult<SteelSchedule> Calculate(RebarLayout l,CalculationPolicy p){Schedules++;return new SteelScheduleStageAdapter().Calculate(l,p);}
        public OperationResult<DrawingPackage> Build(ValidatedManholeSpec s,DrawingInputs inputs,PresentationRequest p,PackageIdentity id){Drawings++;return new DrawingStageAdapter().Build(s,inputs,p,id);}
        public OperationResult<ExportArtifact> Write(CalculationPackage p,WorkbookWriteRequest r){Writes++;LastWritten=p;if(ThrowWrite)throw new IOException("injected");return new(true,new ExportArtifact("not-a-real-file.xlsx",p.InputHash,p.RuleVersion,p.Scope,RecalculationStatus.NotRun),[]);}
    }
}
