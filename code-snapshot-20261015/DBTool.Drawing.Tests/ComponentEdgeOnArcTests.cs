using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class ComponentEdgeOnArcTests
{
    [Theory]
    [InlineData("Wall/T1",false)]
    [InlineData("Wall/T2",true)]
    [InlineData("Wall/T3",false)]
    [InlineData("Wall/T4",true)]
    public void CoincidentArcEndpointsDoNotHideTheInteriorProjectionExtremum(string owner,bool alongY)
    {
        var arc=new ArcSegment3Mm(new(0,0,100),10,alongY?new(0,-1,0):new(1,0,0),
            alongY?new(1,0,0):new(0,1,0),Math.PI);
        var path=new BarPath([arc],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var bar=new BarOccurrence("arc",null,owner,ComponentKind.Wall,Layer.Top,Axis.X,6,path,10*Math.PI,10*Math.PI,
            LengthStatus.Known,"projection",[],RebarDirection.Supplementary,owner,RebarSurface.Outer,"Tie");
        var result=ComponentRebarProjector.Build([bar],new Dictionary<string,string>{{bar.Id,"T001"}},"view",owner,owner,RebarSurface.Outer,Layer.Top);
        Assert.True(result.Success);
        var line=Assert.IsType<PolylinePrimitive>(Assert.Single(result.Value!));
        Assert.Equal(new Point2Mm(0,100),line.Points[0]);
        Assert.Equal(new Point2Mm(0,110),line.Points[1]);
        Assert.Equal(bar.Id,line.OccurrenceId);
        Assert.Equal(owner,line.ComponentId);
        Assert.Equal("T001",line.ScheduleMark);
        Assert.Same(arc,Assert.Single(bar.Path.Segments));
    }
}
