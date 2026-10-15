using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class SheetOneHatchRegionTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(123, -89, false)]
    [InlineData(0, 0, true)]
    public void BaseHaunchAndWallFeetFormOneRegionWithoutChangingArea(double dx, double dy,bool opening)
    {
        Point2Mm P(double x,double y) => new(x+dx,y+dy);
        MaterialRegion R(string id,string owner,params Point2Mm[] points) =>
            new(id,owner,"Concrete",new Footprint(new Polygon2Mm(points),[]),[]);
        var regions = new[] {
            R("Base","Base",P(-500,-150),P(500,-150),P(500,0),P(-500,0)),
            R("Left","Wall/T3",P(-400,0),P(-300,0),P(-300,1000),P(-400,1000)),
            R("Right","Wall/T1",P(300,0),P(400,0),P(400,opening?200:1000),P(300,opening?200:1000)),
            R("HaunchL","Base/Haunch",P(-300,0),P(-250,0),P(-300,50)),
            R("HaunchR","Base/Haunch",P(250,0),P(300,0),P(300,50)) };
        if(opening) regions=[..regions,R("RightAboveOpening","Wall/T1",P(300,500),P(400,500),P(400,1000),P(300,1000))];
        var profile = new ViewProfile("AA",ViewPurpose.Section,regions,[],[]);
        var actual = SheetOneHatchRegions.Build(profile);
        Assert.Equal(opening?4:3,actual.Count);
        Assert.Equal(Area(regions),Area(actual),6);
        var merged=Assert.Single(actual,r=>r.SourceComponentId=="Base");
        Assert.Equal(162500,Area([merged]),6);
        Assert.Equal(50+dy,merged.Footprint.Outer.Vertices.Max(p=>p.Y));
        Assert.All(actual.Where(r=>r.SourceComponentId.StartsWith("Wall/") && r.Id!="RightAboveOpening"),
            r=>Assert.Equal(50+dy,r.Footprint.Outer.Vertices.Min(p=>p.Y)));
        Assert.Equal(opening?6:5,profile.Regions.Length);
        if(opening) Assert.Equal(regions[^1],Assert.Single(actual,r=>r.Id=="RightAboveOpening"));
        Assert.Equal(dy,regions[1].Footprint.Outer.Vertices.Min(p=>p.Y));
    }

    private static double Area(IEnumerable<MaterialRegion> regions) => regions.Sum(r =>
        Math.Abs(r.Footprint.Outer.Vertices.Select((p,i)=>
            p.X*r.Footprint.Outer.Vertices[(i+1)%r.Footprint.Outer.Vertices.Length].Y-
            p.Y*r.Footprint.Outer.Vertices[(i+1)%r.Footprint.Outer.Vertices.Length].X).Sum())/2);
}
