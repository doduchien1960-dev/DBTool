using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public class SheetTwoNominalWallMeshTests
{
    [Fact]
    public void DimensionBandsCannotReplaceActualWallBarsOrBorrowTheirIds()
    {
        const string view="Reinforcement/Wall/T2/Wall/T2/Inner/Bottom";
        var foot=new Footprint(new Polygon2Mm([new(500,-625),new(650,-625),new(650,625),new(500,625)]),[]);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var wall=new ComponentGeometry("Wall/T2",ComponentKind.Wall,"Concrete",foot,new(0,1385),new ExtrusionSolid(foot,frame,new(0,1385)),[],[],[]);
        var g=new GeometryModel([wall],[]);var input=new List<DrawingPrimitive>();
        double[] x=[-775,-745,-655,-525,525,655,745,775],y=[1400,1355,1262.5,62.5,-30,-120,-150];
        for(int i=0;i<x.Length-1;i++) input.Add(new DimensionIntentPrimitive($"{view}/Dimension/NominalSpacing/X/{i:000}",SheetTwoDimensionPlanner.Role,view,"Wall/T2","v","T1",new(x[i],-120),new(x[i+1],-120),x[i+1]-x[i],i==3?"7x150=<>":"<>",0));
        for(int i=0;i<y.Length-1;i++) input.Add(new DimensionIntentPrimitive($"{view}/Dimension/NominalSpacing/Y/{i:000}",SheetTwoDimensionPlanner.Role,view,"Wall/T2","h","T2",new(-745,y[i]),new(-745,y[i+1]),y[i]-y[i+1],i==2?"8x150=<>":"<>",0));
        input.Add(new PolylinePrimitive("v","Rebar/Main",view,"Wall/T2","v","T1",[new(0,-115),new(0,1350)],false));
        input.Add(new PolylinePrimitive("h","Rebar/Main",view,"Wall/T2","h","T2",[new(-600,0),new(600,0)],false));
        var result=SheetTwoNominalWallMesh.Apply(g,input);
        var lines=result.OfType<PolylinePrimitive>().ToArray();
        Assert.Equal(2,lines.Length);
        Assert.Same(input.OfType<PolylinePrimitive>().Single(p=>p.SemanticId=="v"),lines.Single(p=>p.SemanticId=="v"));
        Assert.Same(input.OfType<PolylinePrimitive>().Single(p=>p.SemanticId=="h"),lines.Single(p=>p.SemanticId=="h"));
        Assert.All(lines,p=>Assert.Equal("Rebar/Main",p.Role));
        Assert.Equal(13,result.OfType<DimensionIntentPrimitive>().Count());
        Assert.Equal(input.Count,SheetTwoNominalWallMesh.Apply(new GeometryModel([],[]),input).Count);
        var slab=new ComponentGeometry("Base",ComponentKind.Base,"Concrete",foot,new(-150,0),new ExtrusionSolid(foot,frame,new(-150,0)),[],[],[]);
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",foot,new(1400,1550),new ExtrusionSolid(foot,frame,new(1400,1550)),[],[],[]);
        input.Add(new PolylinePrimitive("base",SheetTwoNominalBaseMesh.Role,"BaseTop","Base","base","D1",[new(0,0),new(0,100)],false));
        input.Add(new PolylinePrimitive("roof","Rebar/Main","Reinforcement/Roof/Roof/Top/Outer/Top","Roof","roof","D2",[new(0,0),new(0,100)],false));
        var adjacent=SheetTwoAdjacentSlabSteel.Build(new GeometryModel([wall,slab,roof],[]),input,new Dictionary<string,int>{{"base",10},{"roof",6}});
        Assert.Equal(4,adjacent.Count);
        Assert.Contains(adjacent,p=>p.OccurrenceId=="roof" && p.Points[0].Y==1466);
        Assert.Contains(adjacent,p=>p.OccurrenceId=="base" && p.Points[0].Y==-120);
        Assert.Empty(SheetTwoAdjacentSlabSteel.Build(g,input,new Dictionary<string,int>()));
    }
}

