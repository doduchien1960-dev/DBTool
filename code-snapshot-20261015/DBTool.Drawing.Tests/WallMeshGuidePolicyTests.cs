using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class WallMeshGuidePolicyTests
{
    [Theory]
    [InlineData(1000,1250,27,0,0)]
    [InlineData(1150,1350,29,250,-100)]
    [InlineData(1200,1450,30,-300,200)]
    public void NominalGridAndFaceAndBaseAxesRetainOriginalGuideTraceLinks(double width,double length,int expected,double x,double y)
    {
        var inputs=Fixture(width,length,x,y);
        var old=new PolylinePrimitive("WallMeshPlan/Guide/physical","Rebar/HiddenSecondary","WallMeshPlan","Wall/T1","physical",null,
            [new(x+50,y-length/2-220+180),new(x+50,y+length/2+220+180)],false);
        var result=WallMeshGuidePolicy.Compose([old],inputs,Presentation).Cast<PolylinePrimitive>().ToArray();
        Assert.Equal(expected+1,result.Length);
        var retained=Assert.Single(result,p=>p.SemanticId==old.SemanticId);
        Assert.Equal(old.OccurrenceId,retained.OccurrenceId); Assert.Equal(old.ComponentId,retained.ComponentId);
        Assert.All(result,p=>{Assert.Equal(WallMeshGuidePolicy.Role,p.Role);Assert.False(p.Closed);Assert.Equal(2,p.Points.Length);});
        var aliases=WallMeshGuidePolicy.RenderAliases(result);
        Assert.Single(aliases);
        var native=result.Where(p=>!aliases.ContainsKey(p.SemanticId)).ToArray();
        Assert.Equal(expected,native.Length);
        var vertical=native.Where(p=>p.Points[0].X==p.Points[1].X).ToArray();
        var horizontal=native.Where(p=>p.Points[0].Y==p.Points[1].Y).ToArray();
        Assert.Equal((int)Math.Round(width/150,MidpointRounding.AwayFromZero)+6,vertical.Length);
        Assert.Equal((int)Math.Round(length/150,MidpointRounding.AwayFromZero)+6,horizontal.Length);
        Assert.All(vertical,p=>{Assert.Equal(y+length/2+220+180,p.Points[0].Y);Assert.Equal(y-length/2-220+180,p.Points[1].Y);});
        Assert.All(horizontal,p=>{Assert.Equal(x-width/2-220,p.Points[0].X);Assert.Equal(x+width/2+220,p.Points[1].X);});
        if(width==1000)
            Assert.Equal(new double[]{-720,-620,-530,-450,-300,-150,0,150,300,450,530,620,720},vertical.Select(p=>p.Points[0].X-x).Order().ToArray());
    }

    [Fact]
    public void MissingDesignOrOtherThemeRetainsExistingGuides()
    {
        var inputs=Fixture(1000,1250,0,0);
        var old=new PolylinePrimitive("guide","Rebar/HiddenSecondary","WallMeshPlan",null,null,null,[new(0,0),new(100,0)],false);
        Assert.Equal(new DrawingPrimitive[]{old},WallMeshGuidePolicy.Compose([old],inputs,new(30,ElevationMode.Symbolic,null,"Default",true)));
        var missing=new DrawingInputs(inputs.Geometry,inputs.RebarLayout,inputs.SteelSchedule,null,inputs.Quantities);
        Assert.Equal(new DrawingPrimitive[]{old},WallMeshGuidePolicy.Compose([old],missing,Presentation));
        var mixed=new DrawingInputs(inputs.Geometry,inputs.RebarLayout,inputs.SteelSchedule,null,inputs.Quantities,
            inputs.WallOuter,new WallOrNeckRebarSpec(10,200,0,0,coverMm:30));
        Assert.Equal(new DrawingPrimitive[]{old},WallMeshGuidePolicy.Compose([old],mixed,Presentation));
        Assert.Empty(WallMeshGuidePolicy.RenderAliases([old]));
    }
    private static readonly PresentationRequest Presentation=new(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
    private static DrawingInputs Fixture(double width,double length,double x,double y)
    {
        ComponentGeometry Rect(string id,ComponentKind kind,double x0,double y0,double x1,double y1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0+x,y0+y),new(x1+x,y0+y),new(x1+x,y1+y),new(x0+x,y1+y)]),[]);
            var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
            return new(id,kind,"Concrete",footprint,new(0,1400),new ExtrusionSolid(footprint,frame,new(0,1400)),[],[],[]);
        }
        double a=width/2,b=length/2;
        var geometry=new GeometryModel([Rect("Wall/T1",ComponentKind.Wall,-a-150,b,a+150,b+150),Rect("Wall/T2",ComponentKind.Wall,a,-b,a+150,b),
            Rect("Wall/T3",ComponentKind.Wall,-a-150,-b-150,a+150,-b),Rect("Wall/T4",ComponentKind.Wall,-a-150,-b,-a,b),
            Rect("Base",ComponentKind.Base,-a-250,-b-250,a+250,b+250)],[]);
        var spec=new WallOrNeckRebarSpec(10,150,0,0,coverMm:30);
        return new(geometry,new RebarLayout([],[],CalculationScope.FullRebar),new SteelSchedule([],[],new MassKg(0),"D2Over162","v1"),
            null,DrawingQuantitySnapshot.NotCalculated,spec,spec);
    }
}
