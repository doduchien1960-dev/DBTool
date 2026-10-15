using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class VhgCastUnionTests
{
    [Theory]
    [InlineData(false,2)]
    [InlineData(true,1)]
    public void ConnectedConcreteKeepsRealJointAndVoid(bool monolithic,int expected)
    {
        MaterialRegion R(string id,double x0,double y0,double x1,double y1)=>new(id,id,"Concrete",
            new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]),[]);
        var profile=new ViewProfile("AA",ViewPurpose.Section,[R("Base",-500,-150,500,0),
            R("Wall/T4",-500,0,-350,1000),R("Wall/T2",350,0,500,1000),
            R("Roof",-500,1000,500,1150)],[],[]);
        var result=SheetOneHatchRegions.Build(profile,monolithic);
        Assert.Equal(expected,result.Count);
        Assert.Equal(4,profile.Regions.Length);
        // The room must remain an empty inner loop in the monolithic section.
        if(monolithic)
        {
            var region=Assert.Single(result);
            Assert.Single(region.Footprint.InnerLoops);
            Assert.Equal(700,region.Footprint.InnerLoops[0].Vertices.Max(p=>p.X)-region.Footprint.InnerLoops[0].Vertices.Min(p=>p.X));
        }
        else Assert.Contains(result,r=>r.SourceComponentId=="Roof");
    }
}
