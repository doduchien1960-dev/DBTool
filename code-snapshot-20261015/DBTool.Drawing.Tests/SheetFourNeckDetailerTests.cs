using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetFourNeckDetailerTests
{
    private static readonly SourceRef Source = new(SourceKind.SyntheticFixture, "NeckTest", new string('a', 64), null, null, null, "NeckTest");

    [Fact]
    public void AbsentNeckCannotProduceTemplateNeckOrSteel()
    {
        var input=new DrawingInputs(new GeometryModel([],[]),new RebarLayout([],[],CalculationScope.FullRebar),
            new SteelSchedule([],[],new MassKg(0),"test","v1"),null,DrawingQuantitySnapshot.NotCalculated);
        var result=SheetFourNeckDetailer.Build(input,new Dictionary<string,string>(),"G500-2",
            new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true));
        Assert.Empty(result.Plan);Assert.Empty(result.AA);Assert.Empty(result.BB);Assert.Empty(result.Schedule);
    }

    [Fact]
    public void GeometryOnlyNeckHasNoInventedSteelOrMarks()
    {
        var seed=CreateInputs(820,570,"CUSTOM");
        var input=new DrawingInputs(seed.Geometry,new RebarLayout([],[],CalculationScope.FullRebar),
            new SteelSchedule([],[],new MassKg(0),"test","v1"),null,DrawingQuantitySnapshot.NotCalculated);
        var result=SheetFourNeckDetailer.Build(input,new Dictionary<string,string>(),"CUSTOM",
            new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true));
        Assert.NotEmpty(result.Plan);
        Assert.DoesNotContain(result.Plan.Concat(result.AA).Concat(result.BB),p=>p.Role.StartsWith("Rebar/",StringComparison.Ordinal));
        Assert.Empty(result.Schedule);
    }

    private static DrawingInputs CreateInputs(double innerW, double innerL, string manholeId)
    {
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var neckFootprint = new Footprint(
            new Polygon2Mm([new(-innerW / 2 - 150, -innerL / 2 - 150), new(innerW / 2 + 150, -innerL / 2 - 150), new(innerW / 2 + 150, innerL / 2 + 150), new(-innerW / 2 - 150, innerL / 2 + 150)]),
            [new Polygon2Mm(new[] { new Point2Mm(-innerW / 2, -innerL / 2), new Point2Mm(innerW / 2, -innerL / 2), new Point2Mm(innerW / 2, innerL / 2), new Point2Mm(-innerW / 2, innerL / 2) }.Reverse())]);
        var neckGeom = new ComponentGeometry("Neck", ComponentKind.Neck, "Concrete", neckFootprint, new(1400, 1600), new ExtrusionSolid(neckFootprint, frame, new(1400, 1600)), [], [], [Source]);
        var geometry = new GeometryModel([neckGeom], []);
        var path = new BarPath([new LineSegment3Mm(new(innerW/2+30, 0, 1400), new(innerW/2+30, 0, 1590))], false, HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var bar = new BarOccurrence("neck-bar-1", null, "Neck", ComponentKind.Neck, Layer.Bottom, Axis.X, 10, path, 190, 190, LengthStatus.Known, "C1", [Source], RebarDirection.Vertical,"Neck/T2",RebarSurface.Inner,"Neck");
        var layout = new RebarLayout([bar], [], CalculationScope.FullRebar);
        double totalLen = 190.0;
        double unitMass = 0.617;
        var row = new SteelScheduleRow("C1", "vert", ComponentKind.Neck, Layer.Bottom, Axis.X, 10, 1, 190, totalLen, unitMass, new MassKg(totalLen / 1000.0 * unitMass), ["neck-bar-1"], "C1", RebarDirection.Vertical);
        var schedule = new SteelSchedule([row], [new KeyValuePair<ComponentKind, MassKg>(ComponentKind.Neck, row.MassKg)], row.MassKg, "D2Over162", "v1");
        var neckSpec = new NeckSpec(innerW, innerL, 150, 200, NeckPosition.Center, [Source]);

        return new DrawingInputs(geometry, layout, schedule, null, DrawingQuantitySnapshot.NotCalculated,
            manholeId: manholeId, neckSpec: neckSpec);
    }

    [Theory]
    [InlineData(820,570,"G500-2")]
    [InlineData(570,820,"CUSTOM-LK")]
    public void ActualNeckBarCountAndStationsSurviveNameAndAxisChanges(double width,double length,string name)
    {
        var input=CreateInputs(width,length,name);
        var result=SheetFourNeckDetailer.Build(input,new Dictionary<string,string>{{"neck-bar-1","C1"}},name,
            new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true));
        var dot=Assert.Single(result.Plan.OfType<CirclePrimitive>(),p=>p.Role=="Rebar/Main");
        Assert.Equal(new Point2Mm(width/2+30,0),dot.Center);
        Assert.Equal("neck-bar-1",dot.OccurrenceId);Assert.Equal(5,dot.RadiusMm);
        Assert.DoesNotContain(result.Plan.Concat(result.AA).Concat(result.BB),p=>p.Role=="Rebar/Main" && p.OccurrenceId is null);
        Assert.Empty(result.Schedule);
    }

    [Fact]
    public void NeckDetailerExpandsParametricallyForG2000_800x800()
    {
        var inputs = CreateInputs(800.0, 800.0, "G2000-LK");
        var marks = new Dictionary<string, string> { { "neck-bar-1", "C1" } };
        var pres = new PresentationRequest(30, ElevationMode.Symbolic, null, SheetOnePresentationPolicy.Family21Theme, true);

        var (plan, aa, bb, _) = SheetFourNeckDetailer.Build(inputs, marks, "G2000-LK", pres);

        // Plan: opening matches 800x800
        var planOpening = Assert.Single(plan.OfType<PolylinePrimitive>(), p => p.SemanticId == "Plan/Poly/InnerOpening");
        double pw = planOpening.Points.Max(p => p.X) - planOpening.Points.Min(p => p.X);
        double pl = planOpening.Points.Max(p => p.Y) - planOpening.Points.Min(p => p.Y);
        Assert.Equal(800.0, pw);
        Assert.Equal(800.0, pl);

        // Section AA: dim of inner neck opening is 800.0
        var aaOpeningDim = Assert.Single(aa.OfType<DimensionIntentPrimitive>(), d => Math.Abs(d.LogicalMeasurementMm - 800.0) < 1e-4);
        Assert.Equal(800.0, aaOpeningDim.LogicalMeasurementMm);

        // Section BB: dim of inner neck opening is 800.0
        var bbOpeningDim = Assert.Single(bb.OfType<DimensionIntentPrimitive>(), d => Math.Abs(d.LogicalMeasurementMm - 800.0) < 1e-4);
        Assert.Equal(800.0, bbOpeningDim.LogicalMeasurementMm);
    }

    [Fact]
    public void NeckDetailerExpandsParametricallyForG800_820x570()
    {
        var inputs = CreateInputs(820.0, 570.0, "G800-NR");
        var marks = new Dictionary<string, string> { { "neck-bar-1", "C1" } };
        var pres = new PresentationRequest(30, ElevationMode.Symbolic, null, SheetOnePresentationPolicy.Family21Theme, true);

        var (plan, aa, bb, _) = SheetFourNeckDetailer.Build(inputs, marks, "G800-NR", pres);

        // Plan: opening matches 820x570
        var planOpening = Assert.Single(plan.OfType<PolylinePrimitive>(), p => p.SemanticId == "Plan/Poly/InnerOpening");
        double pw = planOpening.Points.Max(p => p.X) - planOpening.Points.Min(p => p.X);
        double pl = planOpening.Points.Max(p => p.Y) - planOpening.Points.Min(p => p.Y);
        Assert.Equal(820.0, pw);
        Assert.Equal(570.0, pl);

        // Section AA: inner length is 570.0
        var aaOpeningDim = Assert.Single(aa.OfType<DimensionIntentPrimitive>(), d => Math.Abs(d.LogicalMeasurementMm - 570.0) < 1e-4);
        Assert.Equal(570.0, aaOpeningDim.LogicalMeasurementMm);

        // Section BB: inner width is 820.0
        var bbOpeningDim = Assert.Single(bb.OfType<DimensionIntentPrimitive>(), d => Math.Abs(d.LogicalMeasurementMm - 820.0) < 1e-4);
        Assert.Equal(820.0, bbOpeningDim.LogicalMeasurementMm);
    }
}
