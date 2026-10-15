using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class DowelSectionPresentationTests
{
    private static MaterialRegion Region(string id,string material,double bottom,double top) =>
        new(id,id,material,new Footprint(new Polygon2Mm([new(-775,bottom),new(-625,bottom),new(-625,top),new(-775,top)]),[]),[]);
    private static BarOccurrence Dowel() => new("d",null,"Wall/T2",ComponentKind.Wall,Layer.Bottom,Axis.Y,18,
        new BarPath([new LineSegment3Mm(new(0,-700,1550),new(0,-700,1050))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath),
        500,500,LengthStatus.Known,"DOWEL-VHG-WALL-1",[],RebarDirection.Supplementary,"Wall/T2",null,"Dowel",MeasurementBasisKind.Centerline);

    [Fact]
    public void PhysicalDowelCrossingMortarJointHasOneUnbrokenFiveHundredMillimetreSilhouette()
    {
        var layout=new RebarLayout([Dowel()],[],CalculationScope.FullRebar);
        var section=new ViewProfile("AA",ViewPurpose.Section,
            [Region("Wall/T2","Concrete",0,1385),Region("Wall/Mortar","BeddingMortar",1385,1400),Region("Roof","Concrete",1400,1550)],[],[]);
        var result=StructuralRebarProjector.Build(layout,new Dictionary<string,string>{{"d","C"}},section,
            new ViewProfile("BB",ViewPurpose.Section,[],[],[]),true);
        Assert.True(result.Success);
        var line=Assert.Single(result.Value!.OfType<PolylinePrimitive>(),p=>p.ViewId=="SteelAA");
        Assert.Equal(500,Math.Abs(line.Points[1].Y-line.Points[0].Y));
        Assert.Equal("d",line.OccurrenceId);
        Assert.DoesNotContain(result.Value!,p=>p.ViewId=="WallMeshPlan");
    }

    [Fact]
    public void DowelCalloutRequiresVisiblePhysicalDowelAndRetainsItsDiameter()
    {
        var section=new ViewProfile("AA",ViewPurpose.Section,[Region("Wall/T2","Concrete",0,1385)],[],[]);
        var empty=new ViewProfile("BB",ViewPurpose.Section,[],[],[]);
        var geometry=new GeometryModel([],[]);
        var layout=new RebarLayout([Dowel()],[],CalculationScope.FullRebar);
        var line=new PolylinePrimitive("p","Rebar/Main","SteelAA","Wall/T2","d","C",[new(-700,1550),new(-700,1050)],false);
        var primitives=SectionRebarLabels.Build(geometry,layout,section,empty,[line]);
        Assert.Single(primitives.OfType<TextPrimitive>(),p=>p.Content=="Chốt D18");
        var leader=Assert.Single(primitives.OfType<PolylinePrimitive>(),p=>p.SemanticId.Contains("Dowel/Leader"));
        Assert.Equal(-700,leader.Points[0].X);Assert.InRange(leader.Points[0].Y,1050,1550);
        Assert.DoesNotContain(SectionRebarLabels.Build(geometry,layout,section,empty,[]).OfType<TextPrimitive>(),p=>p.Content=="Chốt D18");
    }
}
