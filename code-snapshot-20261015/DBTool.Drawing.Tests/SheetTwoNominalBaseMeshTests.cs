using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;

public class SheetTwoNominalBaseMeshTests
{
    [Fact]
    public void DimensionBandsCannotManufactureBarsOrMoveTheirLeader()
    {
        var input=new List<DrawingPrimitive>();
        double[] x=[-750,-720,-620,-530,-450,450,530,620,720,750],
            y=[875,845,745,655,525,-525,-655,-745,-845,-875];
        for(int i=0;i<9;i++)
        {
            input.Add(new DimensionIntentPrimitive($"BaseTop/Dimension/NominalSpacing/X/{i:000}",SheetTwoDimensionPlanner.Role,
                "BaseTop","Base","x","D2",new(x[i],-845),new(x[i+1],-845),x[i+1]-x[i],i==4?"6x150=<>":"<>",0));
            input.Add(new DimensionIntentPrimitive($"BaseTop/Dimension/NominalSpacing/Y/{i:000}",SheetTwoDimensionPlanner.Role,
                "BaseTop","Base","y","D1",new(-720,y[i]),new(-720,y[i+1]),y[i]-y[i+1],i==4?"7x150=<>":"<>",0));
        }
        var horizontal=new PolylinePrimitive("h","Rebar/Top","BaseTop","Base","y","D1",[new(-615,0),new(615,0)],false);
        var vertical=new PolylinePrimitive("v","Rebar/Top","BaseTop","Base","x","D2",[new(0,-740),new(0,740)],false);
        input.Add(horizontal);input.Add(vertical);
        input.Add(new PolylinePrimitive("leader","Rebar/Leader","BaseTop","Base","y","D1",[new(0,0),new(0,-1025)],false));
        var result=SheetTwoNominalBaseMesh.Apply(input);
        var lines=result.OfType<PolylinePrimitive>().Where(p=>p.Role=="Rebar/Top").ToArray();
        Assert.Equal(2,lines.Length);
        Assert.Same(horizontal,lines.Single(p=>p.SemanticId=="h"));
        Assert.Same(vertical,lines.Single(p=>p.SemanticId=="v"));
        var leader=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.Role=="Rebar/Leader");
        Assert.Same(input.Single(p=>p.SemanticId=="leader"),leader);
        Assert.Equal(new Point2Mm(0,0),leader.Points[0]);
        Assert.Equal(new Point2Mm(0,-1025),leader.Points[^1]);
        Assert.Equal(18,result.OfType<DimensionIntentPrimitive>().Count());
        Assert.Equal(new Point2Mm(-615,0),horizontal.Points[0]);
    }
    [Fact]
    public void MissingNominalChainsRetainPhysicalProjection()
    {
        var p=new PolylinePrimitive("bar","Rebar/Top","BaseTop","Base","x","D1",[new(0,0),new(100,0)],false);
        Assert.Same(p,Assert.Single(SheetTwoNominalBaseMesh.Apply([p])));
    }
}

