using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class HaunchRebarPlannerTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;

    private static ValidatedManholeSpec CreateSpec(double baseHaunchW, double baseHaunchH, double spacing = 150.0,double cover=30,HaunchRebarSpec? detailing=null,bool roof=false)
    {
        var seed = SyntheticTestFactory.Create();
        var baseHaunchSpec = baseHaunchW > 0 && baseHaunchH > 0
            ? new HaunchSpec(baseHaunchW, baseHaunchH, roof?HaunchOwner.Roof:HaunchOwner.Base, seed.Sources,detailing)
            : null;

        var old = seed.Input;
        var input = new ManholeInput(old.Id, old.Name, old.Profile, old.L, old.B, old.H,
            old.BaseThicknessMm, old.WallThicknessMm, old.RoofThicknessMm, old.PadThicknessMm,
            old.BaseExpansionMm, old.PadExpansionMm, old.BeddingMortarMm,
            roof?0:baseHaunchW, roof?baseHaunchW:old.TopHaunch, old.NeckHeight, old.RoofOpening,
            old.PipeConnections, old.BaseBottomMesh, old.BaseTopMesh, cover,
            old.ElevationMode, old.BaseElevationM, old.PipeInvertReference,
            old.HookBaseBottom, old.HookBaseTop, old.DistributionMode,
            old.MeshJunctionPolicy, old.LayerStackPolicy, old.FeatureRequests,
            old.SourceRefs, old.UnsupportedRawFields,
            baseHaunchSpec: roof?null:baseHaunchSpec,topHaunchSpec:roof?baseHaunchSpec:null);

        return new ValidatedManholeSpec(input, seed.Catalogs, "S2Geometry-1/v1", seed.Sources, []);
    }

    [Fact]
    public void RoofOnlyHaunchMirrorsBaseGeometryAtActualRoofUnderside()
    {
        var baseSpec=CreateSpec(150,100);
        var roofSpec=CreateSpec(150,100,roof:true);
        var geometryPolicy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
        var baseBars=HaunchRebarPlanner.Plan(baseSpec,ChamberGeometryBuilder.Build(baseSpec,geometryPolicy).Value!,Policy).Value!.Occurrences;
        var roofResult=HaunchRebarPlanner.Plan(roofSpec,ChamberGeometryBuilder.Build(roofSpec,geometryPolicy).Value!,Policy);
        Assert.True(roofResult.Success);
        var roofBars=roofResult.Value!.Occurrences;
        Assert.NotEmpty(roofBars);
        Assert.Equal(baseBars.Length,roofBars.Length);
        foreach(var bar in baseBars)
        {
            var mirrored=roofBars.Single(b=>b.Id==bar.Id.Replace("/Base/Haunch/","/Roof/Haunch/"));
            Assert.Equal(ComponentKind.Roof,mirrored.ComponentKind);
            Assert.Equal("Roof/Haunch",mirrored.ComponentId);
            Assert.Equal(bar.ScheduleLengthMm,mirrored.ScheduleLengthMm,6);
            Assert.Equal(LengthStatus.NeedsRule,mirrored.LengthStatus);
            for(int i=0;i<bar.Path.Segments.Length;i++)
            {
                Assert.Equal(bar.Path.Segments[i].Start.X,mirrored.Path.Segments[i].Start.X,6);
                Assert.Equal(bar.Path.Segments[i].Start.Y,mirrored.Path.Segments[i].Start.Y,6);
                Assert.Equal(roofSpec.Input.H-bar.Path.Segments[i].Start.Z,mirrored.Path.Segments[i].Start.Z,6);
                Assert.Equal(roofSpec.Input.H-bar.Path.Segments[i].End.Z,mirrored.Path.Segments[i].End.Z,6);
            }
        }
    }

    [Fact]
    public void IndependentDetailingControlsDiametersPitchAndEachAnchor()
    {
        var spec=CreateSpec(150,150,detailing:new HaunchRebarSpec(12,100,8,240,360));
        var geometry=ChamberGeometryBuilder.Build(spec,CalculationPolicy.Select("S2Geometry-1",true).Value!).Value!;
        var layout=HaunchRebarPlanner.Plan(spec,geometry,Policy).Value!;
        var inclined=layout.Occurrences.Where(b=>b.Id.Contains("/Inclined/")).ToArray();
        Assert.All(inclined,b=>
        {
            Assert.Equal(12,b.DiameterMm);
            Assert.Equal(240,BendLengthCalculator.Distance(b.Path.Segments[0].Start,b.Path.Segments[0].End),6);
            Assert.Equal(360,BendLengthCalculator.Distance(b.Path.Segments[2].Start,b.Path.Segments[2].End),6);
            Assert.Equal(LengthStatus.NeedsRule,b.LengthStatus);
            Assert.Null(b.FabricationLengthMm);
        });
        Assert.All(layout.Occurrences.Where(b=>b.Id.Contains("/Dist/")),b=>Assert.Equal(8,b.DiameterMm));
        var south=inclined.Where(b=>b.Id.Contains("/South/")).OrderBy(b=>b.Path.Segments[0].Start.X).ToArray();
        Assert.True(south.Length>1);
        for(int i=1;i<south.Length;i++) Assert.InRange(south[i].Path.Segments[0].Start.X-south[i-1].Path.Segments[0].Start.X,0.001,100.000001);
    }

    [Fact]
    public void ZeroCoverIsNotReplacedByThirty()
    {
        var spec=CreateSpec(150,100,cover:0);
        var geometry=ChamberGeometryBuilder.Build(spec,CalculationPolicy.Select("S2Geometry-1",true).Value!).Value!;
        var result=HaunchRebarPlanner.Plan(spec,geometry,Policy);
        Assert.True(result.Success);
        Assert.All(result.Value!.Occurrences.Where(b=>b.Id.Contains("/Inclined/")),
            b=>Assert.Equal(-b.DiameterMm/2d,b.Path.Segments[0].Start.Z,6));
    }

    [Theory]
    [InlineData(150,150)]
    [InlineData(150,100)]
    public void DistributionBarsTouchInclinedBarEnvelopeWithoutCrossingItsCenterline(double w,double h)
    {
        var spec=CreateSpec(w,h);
        var geometry=ChamberGeometryBuilder.Build(spec,CalculationPolicy.Select("S2Geometry-1",true).Value!).Value!;
        var bars=HaunchRebarPlanner.Plan(spec,geometry,Policy).Value!.Occurrences;
        foreach(string side in new[]{"North","South","East","West"})
        {
            var inclined=bars.First(b=>b.Id.Contains("/"+side+"/Inclined/"));
            var distribution=bars.Single(b=>b.Id.Contains("/"+side+"/Dist/"));
            var slope=inclined.Path.Segments[1];var point=distribution.Path.Segments[0].Start;
            bool alongX=side is "North" or "South";
            double a=alongX?slope.Start.Y:slope.Start.X,b=alongX?slope.End.Y:slope.End.X;
            double x=alongX?point.Y:point.X,dx=b-a,dz=slope.End.Z-slope.Start.Z;
            double distance=Math.Abs(dx*(point.Z-slope.Start.Z)-dz*(x-a))/Math.Sqrt(dx*dx+dz*dz);
            Assert.Equal((inclined.DiameterMm+distribution.DiameterMm)/2d,distance,6);
            Assert.True(point.Z<(slope.Start.Z+slope.End.Z)/2);
        }
    }

    [Fact]
    public void MissingRequiredSideCannotReportComplete()
    {
        var spec=CreateSpec(500,100);
        var geometry=ChamberGeometryBuilder.Build(spec,CalculationPolicy.Select("S2Geometry-1",true).Value!).Value!;
        var result=HaunchRebarPlanner.Plan(spec,geometry,Policy);
        Assert.True(result.Success);
        Assert.Equal(LayoutStatus.Partial,result.Value!.Status);
        Assert.Contains(result.Diagnostics,d=>d.Code=="HAUNCH_SIDE_NO_ROOM");
    }

    [Fact]
    public void ZeroHaunchGeneratesNoBars()
    {
        var spec = CreateSpec(0, 0);
        var geom = ChamberGeometryBuilder.Build(spec, CalculationPolicy.Select("S2Geometry-1", true).Value!).Value!;
        var result = HaunchRebarPlanner.Plan(spec, geom, Policy);

        Assert.True(result.Success);
        Assert.Empty(result.Value!.Occurrences);
    }

    [Fact]
    public void ActiveHaunchGeneratesBarsWithProperWallOwnersAndAnchorage()
    {
        var spec = CreateSpec(150, 150);
        var geom = ChamberGeometryBuilder.Build(spec, CalculationPolicy.Select("S2Geometry-1", true).Value!).Value!;
        var result = HaunchRebarPlanner.Plan(spec, geom, Policy);

        Assert.True(result.Success);
        var bars = result.Value!.Occurrences;
        Assert.NotEmpty(bars);
        Assert.Equal(LayoutStatus.Partial,result.Value.Status);
        Assert.Contains(result.Diagnostics,d=>d.Code=="HAUNCH_REINFORCEMENT_NEEDS_RULE");
        Assert.All(bars,b=>
        {
            Assert.Equal(LengthStatus.NeedsRule,b.LengthStatus);
            Assert.Null(b.FabricationLengthMm);
        });

        // Check that South bars own Wall/T3 and North bars own Wall/T1
        var southBars = bars.Where(b => b.Id.Contains("/South/")).ToArray();
        var northBars = bars.Where(b => b.Id.Contains("/North/")).ToArray();
        var eastBars = bars.Where(b => b.Id.Contains("/East/")).ToArray();
        var westBars = bars.Where(b => b.Id.Contains("/West/")).ToArray();

        Assert.NotEmpty(southBars);
        Assert.NotEmpty(northBars);
        Assert.NotEmpty(eastBars);
        Assert.NotEmpty(westBars);

        Assert.All(southBars, b => Assert.Equal("Wall/T3", b.FrameId));
        Assert.All(northBars, b => Assert.Equal("Wall/T1", b.FrameId));
        Assert.All(eastBars, b => Assert.Equal("Wall/T2", b.FrameId));
        Assert.All(westBars, b => Assert.Equal("Wall/T4", b.FrameId));

        // Base anchor legs must be embedded into the base slab (Z < 0)
        var inclinedSouth = southBars.Where(b => b.Id.Contains("/Inclined/")).ToArray();
        Assert.NotEmpty(inclinedSouth);
        foreach (var bar in inclinedSouth)
        {
            var pStart = bar.Path.Segments[0].Start;
            Assert.True(pStart.Z < 0, "Base anchorage must be inside base slab (Z < 0).");
            var pEnd = bar.Path.Segments[^1].End;
            Assert.True(pEnd.Z > 150, "Wall anchorage must extend upwards inside wall (Z > haunch height).");
        }
    }

    [Fact]
    public void AsymmetricHaunchCalculatesCorrectNormalAndStaysBounded()
    {
        // w = 150, h = 100
        var spec = CreateSpec(150, 100);
        var geom = ChamberGeometryBuilder.Build(spec, CalculationPolicy.Select("S2Geometry-1", true).Value!).Value!;
        var result = HaunchRebarPlanner.Plan(spec, geom, Policy);

        Assert.True(result.Success);
        var bars = result.Value!.Occurrences;
        Assert.NotEmpty(bars);
        Assert.All(bars, b => Assert.True(b.ScheduleLengthMm > 0));
    }
}
