using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class VhgDowelPlannerTests
{
    private static readonly CalculationPolicy Policy = CalculationPolicy.Select("S2Rebar-1", true).Value!;
    private static ValidatedManholeSpec Spec(double l = 1000, double b = 1250, double t = 150, Profile profile = Profile.GaThamNapRoi)
    {
        var input = new ManholeInput("P6/GaThamNapRoi/test", "test", profile,
            l,b,1400,150,t,150,100,100,100,15,0,0,0,new Rectangle2Mm(-350,-350,350,350),
            [],new MeshSpec(10,150),new MeshSpec(10,150),25,ElevationMode.Symbolic,null,PipeInvertReference.InternalInvert,
            HookPattern.Hook0M,HookPattern.Hook0M,DistributionMode.Equalized,MeshJunctionPolicy.UniformBaseTestOnly,
            LayerStackPolicy.TwoDirectionsTouchingTestOnly,[],new Dictionary<string,SourceRef>(),new Dictionary<string,RawCell>());
        var catalogs = new CatalogSet(new Dictionary<string,PipeCatalogEntry>(),new Dictionary<string,SteelAngleEntry>(),
            new Dictionary<string,HatchEntry>(),new Dictionary<string,string>());
        return new ValidatedManholeSpec(input,catalogs,Policy.Version,[],[]);
    }

    [Theory]
    [InlineData(Profile.GaThamNapRoi)]
    [InlineData(Profile.GaThamThuNapRoi)]
    public void JointDowelPreservesEnteredLengthWithoutDensityQuantization(Profile profile)
    {
        var spec=Spec(profile:profile);var geometry=ChamberGeometryBuilder.Build(spec,Policy).Value!;
        var result=DowelPlanner.PlanVhg(new(500,18,413,Round50Mode.ResolvedVhgRule),spec,geometry,Policy,25,25);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        Assert.NotEmpty(result.Value!.Occurrences);
        Assert.All(result.Value.Occurrences,b=>Assert.Equal(413,b.FabricationLengthMm));
    }

    [Theory]
    [InlineData(1000,1250,150,10)]
    [InlineData(1150,1350,150,12)]
    [InlineData(1200,1450,150,12)]
    [InlineData(1150,1450,200,12)]
    [InlineData(1200,1550,200,14)]
    public void EachWallConsumesA500AndStartsAtActualRoofTop(double l,double b,double t,int count)
    {
        var spec=Spec(l,b,t);
        var geometry=ChamberGeometryBuilder.Build(spec,Policy).Value!;
        var result=DowelPlanner.PlanVhg(new(500,18,500,Round50Mode.ResolvedVhgRule),spec,geometry,Policy,25,25);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(x=>x.Code)));
        Assert.Equal(count,result.Value!.Occurrences.Length);
        Assert.Equal(count*500,result.Value.Occurrences.Sum(x=>x.ScheduleLengthMm));
        foreach(var wall in Enum.GetValues<WallId>())
        {
            var bars=result.Value.Occurrences.Where(x=>x.ComponentId==$"Wall/{wall}").ToArray();
            Assert.NotEmpty(bars);
            var frame=WallFrame.Create(wall,l,b);
            var stations=bars.Select(x=>frame.ToLocal(Assert.IsType<LineSegment3Mm>(Assert.Single(x.Path.Segments)).Start).U).Order().ToArray();
            Assert.Equal(0,stations.Average(),8);
            Assert.All(stations.Zip(stations.Skip(1)),pair=>Assert.InRange(pair.Second-pair.First,0,500));
            foreach(var bar in bars)
            {
                var line=Assert.IsType<LineSegment3Mm>(Assert.Single(bar.Path.Segments));
                Assert.Equal(1550,line.Start.Z); Assert.Equal(1050,line.End.Z);
                Assert.Equal(t/2,frame.ToLocal(line.Start).V,8);
                Assert.Equal("Dowel",bar.FamilyId); Assert.Equal(18,bar.DiameterMm);
                Assert.Equal(500,bar.FabricationLengthMm);
                Assert.Equal(line.Start.X,line.End.X);Assert.Equal(line.Start.Y,line.End.Y);
            }
        }
        Assert.Equal(count,result.Value.Occurrences.Select(x=>x.Id).Distinct().Count());
    }

    [Fact]
    public void InvalidHoleCoverOrLengthCannotBeReportedAsConsumed()
    {
        var spec=Spec();var geometry=ChamberGeometryBuilder.Build(spec,Policy).Value!;
        Assert.False(DowelPlanner.PlanVhg(new(500,18,500),spec,geometry,Policy,18,25).Success);
        Assert.False(DowelPlanner.PlanVhg(new(500,18,500),spec,geometry,Policy,140,25).Success);
        Assert.False(DowelPlanner.PlanVhg(new(500,18,2000),spec,geometry,Policy,25,25).Success);
    }

    [Fact]
    public void PipeCollisionBlocksTheWholeConsumerInsteadOfDroppingADowel()
    {
        var spec=Spec();var original=ChamberGeometryBuilder.Build(spec,Policy).Value!;
        var frame=WallFrame.Create(WallId.T2,spec.Input.L,spec.Input.B);
        var circle=new CircleProfile(new Point2Mm(0,1200),100);
        var opening=new OpeningProfile("Pipe/T2","Wall/T2",OpeningKind.Round,circle,circle,circle,frame,
            new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),new Interval1D(1100,1300),new Interval1D(0,150),[]);
        var geometry=new GeometryModel(original.Components,original.Openings.Append(opening),original.PurchasedCovers);
        var result=DowelPlanner.PlanVhg(new(500,18,500),spec,geometry,Policy,25,25);
        Assert.False(result.Success);Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="DOWEL_OPENING_COLLISION" && d.Severity==DiagnosticSeverity.Block);
    }
}
