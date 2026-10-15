using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class PlanBarProjectorTests
{
    [Fact]
    public void EdgeOnArcKeepsInteriorExtremumWhenBothEndpointsProjectToTheSamePoint()
    {
        var arc=new ArcSegment3Mm(new(100,200,300),10,new(0,1,0),new(0,0,1),Math.PI);
        var path=new BarPath([arc],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var bar=new BarOccurrence("arc",null,"Wall/T1",ComponentKind.Wall,Layer.Top,Axis.X,6,path,10*Math.PI,10*Math.PI,
            LengthStatus.Known,"test",[],familyId:"Tie");
        var result=PlanBarProjector.Build(bar,"Plan","Rebar/Main","T1");
        Assert.True(result.Success);
        var line=Assert.IsType<PolylinePrimitive>(Assert.Single(result.Value!));
        Assert.Equal(100,line.Points.Min(p=>p.X),8);Assert.Equal(110,line.Points.Max(p=>p.X),8);
        Assert.All(line.Points,p=>Assert.Equal(200,p.Y,8));Assert.Equal("arc",line.OccurrenceId);
    }
    [Fact]
    public void PlanPlaneArcRetainsAnalyticRadiusSweepAndAssociation()
    {
        var arc=new ArcSegment3Mm(new(100,200,300),8,new(0,0,-1),new(1,0,0),Math.PI/2);
        var path=new BarPath([arc],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var bar=new BarOccurrence("arc",null,"Base",ComponentKind.Base,Layer.Top,Axis.X,6,path,4*Math.PI,4*Math.PI,LengthStatus.Known,"test",[]);
        var result=PlanBarProjector.Build(bar,"Plan","Rebar/Main","T1");
        Assert.True(result.Success);var drawing=Assert.IsType<PathPrimitive>(Assert.Single(result.Value!));
        var projected=Assert.IsType<ArcPathSegment>(Assert.Single(drawing.Segments));
        Assert.Equal(8,projected.RadiusMm);Assert.Equal(-Math.PI/2,projected.SweepRadians);Assert.Equal("arc",drawing.OccurrenceId);
    }

    [Fact]
    public void VerticalHookLegIsAnEndOnDotAndKeepsOccurrenceTrace()
    {
        var bar = new BarOccurrence("base-hook", null, "Base", ComponentKind.Base, Layer.Bottom, Axis.X, 10,
            new BarPath([new LineSegment3Mm(new(0, 0, 100), new(0, 0, 0)),
                new LineSegment3Mm(new(0, 0, 0), new(1000, 0, 0))], false,
                HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            1100, 1100, LengthStatus.Known, "TEST", []);
        var result = PlanBarProjector.Build(bar, "BaseBottom", "Rebar/Bottom", "D001");
        Assert.True(result.Success);
        Assert.Equal(5, Assert.Single(result.Value!.OfType<CirclePrimitive>()).RadiusMm);
        var line = Assert.Single(result.Value!.OfType<PolylinePrimitive>());
        Assert.Equal(1000, line.Points[1].X - line.Points[0].X);
        Assert.All(result.Value, primitive => Assert.Equal(bar.Id, primitive.OccurrenceId));
        Assert.Equal(1100, bar.ScheduleLengthMm);
    }
}
