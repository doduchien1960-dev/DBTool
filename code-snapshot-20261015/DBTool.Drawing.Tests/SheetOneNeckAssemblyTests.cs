using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;

public sealed class SheetOneNeckAssemblyTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(450)]
    [InlineData(350)]
    public void SourceGroupHasBodyPlus100PlateWithoutMutatingPhysicalSeat(double body)
    {
        var (geometry,presentation,profile)=Fixture(body);
        var assembly=SheetOneNeckAssembly.Resolve(geometry,presentation);
        Assert.NotNull(assembly);
        Assert.Equal(1550+body,assembly!.BodyTopMm);
        Assert.Equal(1550+body+100,assembly.PlateTopMm);
        var result=assembly.Section(profile);
        var plates=result.Regions.Where(r=>r.MaterialId=="Presentation/NapGaVSupportPlate").ToArray();
        Assert.Equal(2,plates.Length);
        Assert.Equal(950,plates.SelectMany(r=>r.Footprint.Outer.Vertices).Max(p=>p.X)-plates.SelectMany(r=>r.Footprint.Outer.Vertices).Min(p=>p.X));
        Assert.Equal(100,plates[0].Footprint.Outer.Vertices.Max(p=>p.Y)-plates[0].Footprint.Outer.Vertices.Min(p=>p.Y));
        Assert.Empty(SectionMaterialPolicy.ForSheetOneMaterial(plates[0].MaterialId));
        Assert.Equal(4,result.Regions.Length);
        Assert.Same(profile.Regions[0],result.Regions[0]);
        Assert.Same(profile.Regions[1],result.Regions[1]);
        Assert.Equal(5,profile.Regions.Length);
        Assert.Equal(1550+body+50,geometry.Components[0].ZRange.End);
        Assert.Equal(1550+body,geometry.PurchasedCovers[0].Anchor.Z);
    }
    [Fact]
    public void RectangularNativeCoverUsesTheSameBodyDatumWithoutExtendingSteelToTheCap()
    {
        var (geometry,presentation,profile)=Fixture(450,"NapGaCN.600x800");
        var assembly=SheetOneNeckAssembly.Resolve(geometry,presentation);
        Assert.NotNull(assembly);
        Assert.Equal(2000,assembly!.BodyTopMm);
        Assert.Equal(2100,assembly.PlateTopMm);
    }
    [Fact]
    public void OtherCoverOrDefaultProfileCannotUse80Convention()
    {
        var (geometry,presentation,_)=Fixture(400,"UNKNOWN.COVER");
        Assert.Null(SheetOneNeckAssembly.Resolve(geometry,presentation));
        var known=Fixture(400).Geometry;
        Assert.Null(SheetOneNeckAssembly.Resolve(known,new PresentationRequest(30,ElevationMode.Symbolic,null,"Default",true)));
    }
    private static (GeometryModel Geometry,PresentationRequest Presentation,ViewProfile Profile) Fixture(double body,string catalog="NapGaV.700")
    {
        Footprint Rect(double x0,double y0,double x1,double y1)=>new(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
        var ring=new Footprint(Rect(-750,-325,200,625).Outer,[new Polygon2Mm(Rect(-625,-200,75,500).Outer.Vertices.Reverse())]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var neck=new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",ring,new(1550,1550+body+50),new ExtrusionSolid(ring,frame,new(1550,1550+body+50)),[],[],[]);
        var collar=new ComponentGeometry("Collar",ComponentKind.Collar,"Concrete",ring,new(1550+body,1550+body+50),new ExtrusionSolid(ring,frame,new(1550+body,1550+body+50)),[],[],[]);
        var cover=new PurchasedCoverPlacement(catalog,new(150,-275,1550+body),new Dimensions2Mm(800,800),1,0,new(-400,-400,400,400),[]);
        var geometry=new GeometryModel([neck,collar],[],[cover]);
        MaterialRegion Region(string id,string owner,double x0,double x1,double z0,double z1)=>new(id,owner,"Concrete",Rect(x0,z0,x1,z1),[]);
        var profile=new ViewProfile("AA",ViewPurpose.Section,[Region("lower1","Neck",-750,-625,1550,1550+body),Region("lower2","Neck",75,200,1550,1550+body),
            Region("seat1","Neck",-750,-700,1550+body,1550+body+50),Region("seat2","Neck",150,200,1550+body,1550+body+50),
            Region("collar","Collar",-700,-625,1550+body,1550+body+50)],[],[]);
        return (geometry,new PresentationRequest(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true),profile);
    }
}
