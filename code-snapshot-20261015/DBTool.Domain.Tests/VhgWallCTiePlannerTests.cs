using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Domain.Tests;
public sealed class VhgWallCTiePlannerTests
{
    private static readonly SourceRef Source=new(SourceKind.SyntheticFixture,"C-stations",new string('a',64),null,null,null,"actual mesh");
    private static (ComponentGeometry Wall,RebarLayout Main,VhgTieInput Tie) Input(bool staggered=false,bool both=true,double pitch=150,double tiePitch=300)
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(600,0),new(600,200),new(0,200)]),[]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,new(0,600),
            new ExtrusionSolid(footprint,frame,new(0,600)),[],[],[Source]);
        var bars=new List<BarOccurrence>();
        void Add(string id,Point3Mm a,Point3Mm b,RebarDirection direction,RebarSurface face)
        {
            var path=new BarPath([new LineSegment3Mm(a,b)],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
            var length=BendLengthCalculator.Distance(a,b);
            bars.Add(new(id,null,wall.Id,wall.Kind,face==RebarSurface.Inner?Layer.Bottom:Layer.Top,Axis.X,10,path,length,length,
                LengthStatus.Known,"fixture",[Source],direction,wall.Id,face,"Wall"));
        }
        for(int i=0;75+i*pitch<600;i++)
        {
            double s=75+i*pitch;
            Add("V/"+i,new(s,40,25),new(s,40,575),RebarDirection.Vertical,RebarSurface.Inner);
            Add("I/"+i,new(25,50,s),new(575,50,s),RebarDirection.Horizontal,RebarSurface.Inner);
            if(both) Add("O/"+i,new(25,150,s),new(575,150,s),RebarDirection.Horizontal,RebarSurface.Outer);
        }
        var tie=new VhgTieInput(new(6,tiePitch),staggered,new Dictionary<ComponentKind,VhgTieShape>{{ComponentKind.Wall,VhgTieShape.C}},50,100,200,[Source]);
        return (wall,new(bars,[],CalculationScope.FullRebar),tie);
    }
    [Theory]
    [InlineData(false,75)]
    [InlineData(true,225)]
    public void ActualTwoFaceStationsGenerateCPathsAndStaggerByOneMainStation(bool staggered,double expectedSecondRowX)
    {
        var input=Input(staggered);
        var result=TiePlanner.PlanVhgWallC(input.Wall,input.Main,input.Tie,150,150);
        Assert.True(result.Success);Assert.Equal(LayoutStatus.Complete,result.Value!.Status);
        Assert.Equal(4,result.Value.Occurrences.Length);
        var secondRow=result.Value.Occurrences.Where(b=>b.Id.Contains("/002/")).OrderBy(b=>b.Id).First();
        Assert.Equal(expectedSecondRowX,secondRow.Path.Segments[0].Start.X);
        Assert.All(result.Value.Occurrences,b=> {Assert.Equal("Tie",b.FamilyId);Assert.Equal("Wall/T1",b.ComponentId);
            Assert.Equal(4,b.Path.Segments.OfType<ArcSegment3Mm>().Count());Assert.Equal(LengthStatus.Known,b.LengthStatus);});
    }
    [Fact]
    public void OpeningContactOmitsWholeCandidateAndReportsItsStation()
    {
        var input=Input();
        var frame=WallFrame.Create(WallId.T1,600,200);
        var profile=new CircleProfile(new(-75,78),.5);
        var opening=new OpeningProfile("C-contact",input.Wall.Id,OpeningKind.Round,profile,profile,profile,frame,
            new(frame.Origin,frame.Left,frame.Up,frame.Outward),new(77.5,78.5),new(-100,100),[Source]);
        var wall=new ComponentGeometry(input.Wall.Id,input.Wall.Kind,input.Wall.MaterialId,input.Wall.Footprint,
            input.Wall.ZRange,input.Wall.Occupied,[opening],[],input.Wall.Sources);
        var result=TiePlanner.PlanVhgWallC(wall,input.Main,input.Tie,150,150);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        Assert.Equal(3,result.Value!.Occurrences.Length);
        Assert.DoesNotContain(result.Value.Occurrences,b=>b.Id=="Wall/T1/Tie/C/000/000");
        Assert.Contains(result.Diagnostics,d=>d.Code=="TIE_C_OPENING_EXCLUDED" && d.Actual!.Contains("station=75; z=75"));
        Assert.All(result.Value.Occurrences,b=>Assert.Equal(4,b.Path.Segments.OfType<ArcSegment3Mm>().Count()));
    }

    [Fact]
    public void OneFaceDisablesTiesAndConflictingPitchRequiresRule()
    {
        var input=Input(both:false);
        var off=TiePlanner.PlanVhgWallC(input.Wall,input.Main,input.Tie,150,150);
        Assert.True(off.Success);Assert.Empty(off.Value!.Occurrences);Assert.Equal(LayoutStatus.Disabled,off.Value.Status);
        input=Input(pitch:200,tiePitch:300);
        var conflict=TiePlanner.PlanVhgWallC(input.Wall,input.Main,input.Tie,200,200);
        Assert.Equal(LayoutStatus.Partial,conflict.Value!.Status);Assert.Empty(conflict.Value.Occurrences);
        Assert.Contains(conflict.Diagnostics,d=>d.Code=="TIE_DISTRIBUTION_NEEDS_RULE");
    }
}
