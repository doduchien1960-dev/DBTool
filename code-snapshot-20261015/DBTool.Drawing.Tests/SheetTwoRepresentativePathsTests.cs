using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class SheetTwoRepresentativePathsTests
{
    [Fact]
    public void RepresentativeRouteUsesNativePolylineAndRetainsCanonicalAssociation()
    {
        const string view="BaseTop";
        var input=new List<DrawingPrimitive>();
        double[] xb=[30,100,90,80,900,80,90,100,30],yb=[30,100,90,130,1050,130,90,100,30];
        double x=-750,y=875;
        for(int i=0;i<9;i++)
        {
            input.Add(new DimensionIntentPrimitive($"{view}/NominalSpacing/X/{i:000}",SheetTwoDimensionPlanner.Role,view,"Base","bar","D001",new(x,0),new(x+xb[i],0),xb[i],"<>",0));
            input.Add(new DimensionIntentPrimitive($"{view}/NominalSpacing/Y/{i:000}",SheetTwoDimensionPlanner.Role,view,"Base","bar","D001",new(0,y),new(0,y-yb[i]),yb[i],"<>",0));
            x+=xb[i];y-=yb[i];
        }
        input.Add(new PolylinePrimitive("mesh",SheetTwoNominalBaseMesh.Role,view,"Base","bar","D001",[new(-450,-845),new(-450,845)],false));
        var label=new BlockPrimitive("label",SheetTwoBarAnnotations.Role,view,"Base","Block:BLT1",new(-410,-1025),.3,[new("SH","D1")],occurrenceId:"bar",scheduleMark:"D001");
        var leader=new PolylinePrimitive("label/Leader","Rebar/Leader",view,"Base","bar","D001",[new(-450,-845),label.Insertion],false);
        input.Add(label);input.Add(leader);
        var result=SheetTwoRepresentativePaths.Apply(input);
        var route=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.Role==SheetTwoRepresentativePaths.Role);
        Assert.Equal(new Point2Mm(-440,845),route.Points[0]);
        Assert.Equal(label.Insertion,route.Points[^1]);
        Assert.Equal(4,route.Points.Length);Assert.Equal(label.OccurrenceId,route.OccurrenceId);Assert.Equal(label.ScheduleMark,route.ScheduleMark);
        Assert.Contains(label,result);
        Assert.Equal("VHG-Dim",SemanticRoleRegistry.Resolve(route.Role)!.Layer);
        var unsupported=new DrawingPrimitive[]{label,leader};
        Assert.Equal(unsupported,SheetTwoRepresentativePaths.Apply(unsupported));
        var withoutMesh=input.Where(p=>p.SemanticId!="mesh").ToArray();
        Assert.Equal(withoutMesh,SheetTwoRepresentativePaths.Apply(withoutMesh));
    }
}
