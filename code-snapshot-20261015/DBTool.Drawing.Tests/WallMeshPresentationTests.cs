using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class WallMeshPresentationTests
{
    [Theory]
    [InlineData(0,0)]
    [InlineData(200,300)]
    public void FaceLabelsUseSourceAlignmentAndWallRelativeAnchors(double dx,double dy)
    {
        ComponentGeometry Wall(string id,double x0,double y0,double x1,double y1)
        {
            var fp=new Footprint(new Polygon2Mm([new(x0+dx,y0+dy),new(x1+dx,y0+dy),new(x1+dx,y1+dy),new(x0+dx,y1+dy)]),[]);
            var z=new Interval1D(0,1400);
            var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
            return new(id,ComponentKind.Wall,"Concrete",fp,z,new ExtrusionSolid(fp,frame,z),[],[],[]);
        }
        var geometry=new GeometryModel([Wall("Wall/T1",-650,625,650,775),Wall("Wall/T2",500,-625,650,625),
            Wall("Wall/T3",-650,-775,650,-625),Wall("Wall/T4",-650,-625,-500,625)],[]);
        var labels=WallMeshPresentation.Build(geometry,new RebarLayout([],[],CalculationScope.FullRebar)).OfType<TextPrimitive>().ToArray();
        Assert.Equal(8,labels.Length);
        var expected=new[]{("T1",true,-240d,1123d,false),("T1",false,-240d,717d,false),
            ("T3",true,-240d,-783d,false),("T3",false,-240d,-377d,false),
            ("T4",true,-798d,260d,true),("T4",false,-452d,260d,false),
            ("T2",true,440d,-10d,true),("T2",false,452d,410d,true)};
        foreach(var (wall,outside,x,y,right) in expected)
        {
            var label=Assert.Single(labels,t=>t.SemanticId==$"WallMeshPlan/Label/Wall/{wall}/{outside}");
            Assert.Equal(new Point2Mm(x+dx,y+dy),label.Position);
            Assert.Equal(right?"Rebar/MarkRight":"Rebar/Mark",label.Role);
            Assert.Equal(60,label.HeightMm);
            Assert.Equal("VHG-Unicode.Regular",SemanticRoleRegistry.Resolve(label.Role)!.TextStyle);
        }
        var leaders=WallMeshPresentation.Build(geometry,new RebarLayout([],[],CalculationScope.FullRebar)).OfType<PolylinePrimitive>().ToArray();
        Assert.Equal(8,leaders.Length);
        Assert.All(leaders,p=>{Assert.Equal("Rebar/Leader",p.Role);Assert.Equal(3,p.Points.Length);});
    }
}
