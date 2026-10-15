using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;

public class SectionRebarLabelsTests
{
    [Fact]
    public void NeckLabelTargetsVisibleSectionDotEvenWhenPhysicalBarMidpointIsInVoid()
    {
        var footprint=new Footprint(new Polygon2Mm([new(-500,-500),new(500,-500),new(500,500),new(-500,500)]),[]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var z=new Interval1D(1500,2100);
        var neck=new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",footprint,z,new ExtrusionSolid(footprint,frame,z),[],[],[]);
        var bar=new BarOccurrence("n",null,"Neck",ComponentKind.Neck,Layer.Bottom,Axis.Y,10,
            new BarPath([new LineSegment3Mm(new(0,-500,1800),new(0,500,1800))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),1000,1000,LengthStatus.Known,"TEST",[]);
        var cut=new Footprint(new Polygon2Mm([new(-500,1500),new(-400,1500),new(-400,2100),new(-500,2100)]),[]);
        var aa=new ViewProfile("AA",ViewPurpose.Section,[new MaterialRegion("n","Neck","Concrete",cut,[])],[],[]);
        var bb=new ViewProfile("BB",ViewPurpose.Section,[],[],[]);
        var geometry=new GeometryModel([neck],[]);var layout=new RebarLayout([bar],[],CalculationScope.FullRebar);
        var dot=new CirclePrimitive("dot","Rebar/Main","SteelAA","Neck","n",null,new(-450,1800),5);
        var result=SectionRebarLabels.Build(geometry,layout,aa,bb,[dot]);
        Assert.Contains(result.OfType<TextPrimitive>(),p=>p.Content=="Cổ ga");
        Assert.Equal(1875,Assert.Single(result.OfType<TextPrimitive>(),p=>p.Content=="Cổ ga").Position.Y);
        var leader=Assert.Single(result.OfType<PolylinePrimitive>());
        Assert.Equal(dot.Center,leader.Points[0]);
        Assert.Equal("Rebar/Leader",leader.Role);
        Assert.Equal(3,leader.Points.Length);
        Assert.Empty(SectionRebarLabels.Build(geometry,layout,aa,bb,[]));
        Assert.Equal(1000,bar.ScheduleLengthMm);
        var vertical=new PolylinePrimitive("visible","Rebar/Main","SteelAA","Neck","n",null,
            [new(-450,1600),new(-450,2000)],false);
        var continuous=SectionRebarLabels.Build(geometry,layout,aa,bb,[vertical]);
        Assert.Equal(new Point2Mm(-450,1800),Assert.Single(continuous.OfType<PolylinePrimitive>()).Points[0]);
        var aligned=Assert.Single(continuous.OfType<TextPrimitive>());
        Assert.Equal("Rebar/MarkRight",aligned.Role);
        Assert.Equal(new Point2Mm(-595,1875),aligned.Position);
    }
}
