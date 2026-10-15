using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CWrapOpeningCheckTests
{
    [Theory]
    [InlineData(WallId.T1,false)]
    [InlineData(WallId.T1,true)]
    [InlineData(WallId.T2,false)]
    [InlineData(WallId.T2,true)]
    [InlineData(WallId.T3,false)]
    [InlineData(WallId.T3,true)]
    [InlineData(WallId.T4,false)]
    [InlineData(WallId.T4,true)]
    public void EdgeOnCArcsKeepFarWrapAndRejectArcContactOnEveryWall(WallId id,bool box)
    {
        var frame=WallFrame.Create(id,600,200);
        string owner="Wall/"+id;
        var source=new SourceRef(SourceKind.SyntheticFixture,"C-opening",new string('a',64),null,null,null,"exact arc contact");
        var path=TiePlanner.BuildCWrap(frame.ToGlobal(75,50,75),10,frame.ToGlobal(75,150,75),10,6,50,source).Value!;
        double length=BendLengthCalculator.Calculate(path,0,source).Value!.Value;
        var bar=new BarOccurrence("C",null,owner,ComponentKind.Wall,Layer.Top,Axis.X,6,path,length,length,
            LengthStatus.Known,"C",[source]);
        ComponentGeometry Wall(double u,double z,double radius,double depth=0)
        {
            Profile2Mm profile=box?new RectangleProfile(new(u-radius,z-radius,u+radius,z+radius)):new CircleProfile(new(u,z),radius);
            var opening=new OpeningProfile("hole",owner,box?OpeningKind.Box:OpeningKind.Round,profile,profile,profile,frame,
                new(frame.Origin,frame.Left,frame.Up,frame.Outward),new(z-radius,z+radius),new(depth,depth+200),[source]);
            var footprint=new Footprint(new Polygon2Mm([new(-1000,-1000),new(1000,-1000),new(1000,1000),new(-1000,1000)]),[]);
            return new(owner,ComponentKind.Wall,"Concrete",footprint,new(0,600),
                new ExtrusionSolid(footprint,new(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1)),new(0,600)),[opening],[],[source]);
        }
        var far=CWrapOpeningCheck.Intersects(bar,Wall(500,75,20));
        Assert.True(far.Success,string.Join(";",far.Diagnostics.Select(d=>d.Code)));
        Assert.False(far.Value!.Intersects);
        var hit=CWrapOpeningCheck.Intersects(bar,Wall(75,78,.5));
        Assert.True(hit.Success,string.Join(";",hit.Diagnostics.Select(d=>d.Code)));
        Assert.True(hit.Value!.Intersects);
        var outsideDepth=CWrapOpeningCheck.Intersects(bar,Wall(75,78,.5,300));
        Assert.True(outsideDepth.Success);
        Assert.False(outsideDepth.Value!.Intersects);
        Assert.Equal(4,bar.Path.Segments.OfType<ArcSegment3Mm>().Count());
        Assert.Equal(length,bar.ScheduleLengthMm);
    }
}


