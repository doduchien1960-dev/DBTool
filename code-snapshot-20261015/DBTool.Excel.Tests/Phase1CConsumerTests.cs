using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class Phase1CConsumerTests
{
    [Fact]
    public void InvalidActiveLiftingCountReturnsCellDiagnostic()
    {
        var path=TestWorkbookFactory.CopySource(2,b=>b.Worksheet("CF.Ga").Cell("H9").Value="3D12");
        try
        {
            var raw=new WorkbookImporter().Read(new("1C",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var numeric=new NumericPolicy("en-US",false,false);
            var catalogs=new CatalogReader().Read(raw,numeric).Value!;
            var result=new ManholeValidator(new NumericCellReader(),numeric).Validate(raw,catalogs,
                new("1C",[ManholeIdentity.Create("1C",raw.Profile,"G400-1")],CalculationScope.FullRebar,Policy.PolicyId,true),Policy);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics,d=>d.Code=="ACCESSORY_INPUT_INVALID" && d.Source?.CellAddress=="H9");
        }
        finally{File.Delete(path);}
    }

    [Fact]
    public void ActualWorkbookAccessoriesStayTypedAndUnresolvedConsumersStillBlockDrawing()
    {
        var path=TestWorkbookFactory.CopySource(2,b=>TestWorkbookFactory.DeclareSyntheticCover(b));
        try
        {
            var raw=new WorkbookImporter().Read(new("1C",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var numeric=new NumericPolicy("en-US",false,false);var catalogs=new CatalogReader().Read(raw,numeric).Value!;
            var request=new CalculationRequest("1C",[ManholeIdentity.Create("1C",raw.Profile,"G400-1")],CalculationScope.FullRebar,Policy.PolicyId,true);
            var validation=new ManholeValidator(new NumericCellReader(),numeric).Validate(raw,catalogs,request,Policy);
            Assert.True(validation.Success,string.Join(";",validation.Diagnostics.Select(d=>d.Code)));
            var accessories=validation.Value!.SelectedManholes[0].Input.MainRebar!.Accessories!;
            Assert.NotNull(accessories.Anchor);Assert.NotNull(accessories.Dowel);Assert.NotNull(accessories.Lifting);Assert.NotNull(accessories.Ties);
            Assert.Equal(VhgOpeningComposition.BoSquare,accessories.Pipe!.Composition);
            Assert.Equal(VhgOpeningComposition.BoSquare,accessories.Roof!.Composition);
            Assert.Equal(raw.ConfigCells["DowelHoleDia"].Number,accessories.DowelHoleDiameterMm);
            Assert.Contains(accessories.Dowel!.Sources,s=>s.CellAddress=="F9");
            var capability=new FeatureSupportValidator().ValidateS2Rebar(validation.Value.SelectedManholes[0],request.Scope,Policy);
            Assert.DoesNotContain(capability.Diagnostics,d=>d.Code=="FEATURE_UNSUPPORTED" && d.Actual=="Dowels");
            var result=new CalculationPipeline(new GeometryStageAdapter(),new FullRebarStage(),new SteelScheduleStageAdapter(),new DrawingStageAdapter())
                .Calculate(validation.Value,Policy,request,new(30,ElevationMode.Symbolic,null,"Default",true),default);
            Assert.False(result.Success);Assert.Null(result.Value);
            foreach(var feature in new[]{"LiftingHooks","NeckAnchor","Ties","PipeReinforcement","RoofOpeningReinforcement"})
                Assert.Contains(result.Diagnostics,d=>d.Code=="FEATURE_UNSUPPORTED" && d.Actual==feature && d.Severity==DiagnosticSeverity.Block && d.Source is not null);
        }
        finally{File.Delete(path);}
    }

    [Fact]
    public void BoStillActiveWithZeroSideCount()
    {
        WithSource(b=>{DrawingCompatible(b);var c=b.Worksheet("CF.Ga");c.Cell("C30").Value=10;c.Cell("D30").Value="Bo";c.Cell("E30").Value=0;},
            (_,input)=>Assert.Equal(2,Calculate(input).Occurrences.Count(b=>b.FamilyId=="OpeningReinforcement")));
    }

    [Fact]
    public void BothWallMeshesCanBeDisabledWithoutLosingSupportCover()
    {
        WithSource(b=>{DrawingCompatible(b);var sheet=b.Worksheet("GaThamNapRoi");sheet.Cell("J4").Value="";sheet.Cell("K4").Value="";
            b.Worksheet("CF.Ga").Cell("D17").Value=45;},
            (_,input)=>{
                var main=input.SelectedManholes[0].Input.MainRebar!;
                Assert.Null(main.WallOuter);Assert.Null(main.WallInner);Assert.Equal(45,main.WallSupportCoverMm);
                var spec=input.SelectedManholes[0];var geometry=new GeometryStageAdapter().Build(spec,Policy).Value!;
                var result=LadderPlanner.PlanApproved(main.Ladder!,spec,geometry,Policy);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics,d=>d.Code=="LADDER_SUPPORT_INVALID");
            });
    }
    [Theory]
    [InlineData("Bo", "Bo")]
    [InlineData("Vuông", "Square")]
    [InlineData("Xiên", "Diagonal")]
    [InlineData("Bo+Vuông", "BoSquare")]
    [InlineData("Bo+Xiên", "BoDiagonal")]
    public void OpeningCompositionIsTypedAndKeepsItsSource(string text, string expected)
    {
        WithSource(b => { var c=b.Worksheet("CF.Ga"); c.Cell("D28").Value=10; c.Cell("F28").Value=1; c.Cell("E28").Value=text; },
            (raw, validated) =>
            {
                var opening=validated.SelectedManholes[0].Input.MainRebar!.Accessories!.Pipe!;
                Assert.Equal(expected, opening.Composition.ToString());
                Assert.Contains(opening.Sources, s => s.CellAddress=="E28" && s.SheetName=="CF.Ga");
            });
    }
    [Fact]
    public void GeometryRetainsPurchasedCover()
    {
        WithSource(DrawingCompatible, (raw, validated) =>
        {
            var package = Calculate(validated);
            var cover=Assert.Single(package.Geometry.PurchasedCovers);
            Assert.Equal(validated.SelectedManholes[0].Input.PurchasedCoverSpec!.Anchor,cover.Anchor);
            Assert.NotEmpty(cover.Sources);
        });
    }

    [Fact]
    public void LadderWorksWhenInnerMeshIsDisabled()
    {
        WithSource(b => { DrawingCompatible(b); b.Worksheet("GaThamNapRoi").Cell("K4").Value = ""; }, (raw, validated) =>
        {
            Assert.Null(validated.SelectedManholes[0].Input.MainRebar!.WallInner);
            Assert.Contains(Calculate(validated).Occurrences, b => b.FamilyId == "Ladder");
        });
    }

    [Fact]
    public void LadderStillBlocksWhenSupportCannotFit()
    {
        WithSource(b => { DrawingCompatible(b); b.Worksheet("GaThamNapRoi").Cell("K4").Value = ""; }, (raw, validated) =>
        {
            var spec = validated.SelectedManholes[0];
            var geometry = new GeometryStageAdapter().Build(spec, Policy).Value!;
            var ladder = new LadderSpec(300, 300, 0, 300, 90, 150, 18, sources: spec.Sources);
            var result = LadderPlanner.PlanApproved(ladder, spec, geometry, Policy);
            Assert.False(result.Success);
            Assert.Contains(result.Diagnostics, d => d.Code == "LADDER_SUPPORT_INVALID" && d.Severity == DiagnosticSeverity.Block);
        });
    }

    internal static void DrawingCompatible(XLWorkbook b)
    {
        var c = b.Worksheet("CF.Ga"); c.Cell("D5").Value = 0; c.Cell("F11").Value = "Không"; c.Cell("F13").Value = "Không";
        foreach (var col in new[] { "O", "P", "Q", "R", "S", "T", "U", "V" }) b.Worksheet("GaThamNapRoi").Cell(col + "4").Value = "";
    }
    internal static CalculationPolicy Policy => CalculationPolicy.Select("S2Rebar-1", true).Value!;
    internal static CalculationPackage Calculate(ValidatedProjectInput input)
    {
        var result = new CalculationPipeline(new GeometryStageAdapter(), new FullRebarStage(),
            new SteelScheduleStageAdapter(), new DrawingStageAdapter()).Calculate(input, Policy,
            new("1C", [input.SelectedManholes[0].Input.Id], CalculationScope.FullRebar, Policy.PolicyId, true),
            new(30, ElevationMode.Symbolic, null, "Default", false), default);
        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code + ":" + d.Actual)));
        return result.Value!;
    }
    [Theory]
    [InlineData("Bo",2)]
    [InlineData("Vuông",8)]
    [InlineData("Bo+Vuông",10)]
    public void RoofCompositionReachesSchedule(string shape,int count)
    {
        WithSource(b => { DrawingCompatible(b);var c=b.Worksheet("CF.Ga");c.Cell("C30").Value=10;c.Cell("E30").Value=1;c.Cell("D30").Value=shape; },
            (raw,input) => {
                var package=Calculate(input);
                var bars=package.Occurrences.Where(b=>b.FamilyId=="OpeningReinforcement").ToArray();
                Assert.Equal(count,bars.Length);
                Assert.All(bars,b=>Assert.Contains(b.Sources,s=>s.CellAddress=="D30"));
                var marks=package.SteelSchedule!.Rows.Where(r=>r.FamilyId=="OpeningReinforcement").ToArray();
                Assert.Equal(count,marks.Sum(r=>r.Quantity));
                Assert.Equal(bars.Select(b=>b.Id).Order(),marks.SelectMany(r=>r.OccurrenceIds).Order());
            });
    }

    internal static void WithSource(Action<XLWorkbook>? edit, Action<RawProjectInput, ValidatedProjectInput> check)
    {
        var path = TestWorkbookFactory.CopySource(2, b =>
        {
            var c = b.Worksheet("CF.Ga");
            c.Cell("C9").Value = "Không"; c.Cell("H9").Value = "0D12";
            c.Cell("J11").Value = "0"; c.Cell("H23").Value = "0";
            c.Cell("D28").Value = 0; c.Cell("F28").Value = 0; c.Cell("E30").Value = 0;
            TestWorkbookFactory.DeclareSyntheticCover(b);
            edit?.Invoke(b);
        });
        try
        {
            var raw = new WorkbookImporter().Read(new("1C", path, Profile.GaThamNapRoi, "GaThamNapRoi")).Value!;
            var numeric = new NumericPolicy("en-US", false, false);
            var catalogs = new CatalogReader().Read(raw, numeric);
            Assert.True(catalogs.Success);
            var result = new ManholeValidator(new NumericCellReader(), numeric).Validate(raw, catalogs.Value!,
                new("1C", [ManholeIdentity.Create("1C", raw.Profile, "G400-1")], CalculationScope.FullRebar, Policy.PolicyId, true), Policy);
            Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code + ":" + d.Actual)));
            check(raw, result.Value!);
        }
        finally { File.Delete(path); }
    }
}
