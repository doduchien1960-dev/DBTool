using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class VhgRoofSourceRegressionTests
{
    [Theory]
    [InlineData(1300,1550,"G500-2")]
    [InlineData(2100,2400,"G500-2")]
    [InlineData(2100,2400,"CUSTOM")]
    [InlineData(2100,2400,"CUSTOM-NR")]
    [InlineData(2100,2400,"CUSTOM-LK")]
    public void NameCannotReplaceAssemblyOrInventDowelHoles(double width,double height,string name)
    {
        Polygon2Mm Rect(double w,double h)=>new([new(-w/2,-h/2),new(w/2,-h/2),new(w/2,h/2),new(-w/2,h/2)]);
        var footprint=new Footprint(Rect(width,height),[new Polygon2Mm(Rect(700,700).Vertices.Reverse())]);
        var z=new Interval1D(1400,1550);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var geometry=new GeometryModel([new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,frame,z),[],[],[])],[]);
        string view=SheetThreeRoofViews.TopViewId;
        var bar=new PolylinePrimitive("physical","Rebar/Main",view,"Roof","occ","N1",[new(-100,0),new(100,0)],false);
        var result=SheetThreeRoofDetailer.Build(geometry,[bar],view,manholeCode:name);
        Assert.Same(bar,Assert.Single(result,p=>p.SemanticId=="physical"));
        var outline=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.Role=="Geometry/BaseOutline");
        Assert.Equal(width,outline.Points.Max(p=>p.X)-outline.Points.Min(p=>p.X));
        Assert.Equal(height,outline.Points.Max(p=>p.Y)-outline.Points.Min(p=>p.Y));
        Assert.DoesNotContain(result,p=>p.SemanticId.Contains("DowelHole") || p.Role=="Note/RoofDowel");
        Assert.DoesNotContain(result,p=>p.Role=="Rebar/Secondary" && p.OccurrenceId is null);
        Assert.Empty(result.OfType<BlockPrimitive>()); // No nominal diameter/pitch without actual metadata.
        var annotated=SheetThreeRoofDetailer.Build(geometry,[bar],view,new MeshSpec(12,175),30,new HashSet<string>{"occ"},name);
        var label=Assert.Single(annotated.OfType<BlockPrimitive>(),p=>p.Role==SheetTwoBarAnnotations.Role);
        Assert.Contains(label.Attributes,a=>a.Key=="DK" && a.Value=="%%c12");
        Assert.DoesNotContain(annotated.OfType<TextPrimitive>(),p=>p.Content=="N1");
    }
}
