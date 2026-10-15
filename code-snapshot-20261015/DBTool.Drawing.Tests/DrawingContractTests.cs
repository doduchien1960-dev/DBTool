using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public class DrawingContractTests
{
    [Fact] public void PolylineCopiesCallerPoints()
    {
        var points=new List<Point2Mm>{new(0,0),new(10,0)};
        var line=new PolylinePrimitive("L1","Outline","V1",null,null,null,points,false); points.Clear();
        Assert.Equal(2,line.Points.Length);
    }
    [Fact] public void DrawingRejectsUnresolvedView()
    {
        Assert.Throws<ArgumentException>(()=>new DrawingPackage(new string('a',64),"v1",[],[new CirclePrimitive("C1","Outline","missing",null,null,null,new(0,0),10)],new Bounds2Mm(-10,-10,10,10),CalculationScope.BaseRebarOnly));
    }
    [Fact] public void SymbolicDatumRemainsUnknown()
    {
        Assert.Null(new PresentationRequest(20,ElevationMode.Symbolic,null,"default",true).BaseElevationM);
        Assert.Throws<ArgumentException>(()=>new PresentationRequest(20,ElevationMode.Absolute,null,"default",true));
    }
}
