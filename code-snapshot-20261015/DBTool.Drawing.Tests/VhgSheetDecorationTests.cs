using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class VhgSheetDecorationTests
{
    [Theory]
    [InlineData(-325,325,-200,200)]
    [InlineData(325,-325,200,-200)]
    public void PublishedMarkersFollowTheNeckAndClearActualDimensionRows(double cx,double cy,double axisA,double axisB)
    {
        Polygon2Mm Rect(double x0,double y0,double x1,double y1,bool hole=false)=>new(hole
            ?new Point2Mm[]{new(x0,y0),new(x0,y1),new(x1,y1),new(x1,y0)}
            :new Point2Mm[]{new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)});
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        ComponentGeometry Component(string id,ComponentKind kind,Footprint footprint,Interval1D heights)=>
            new(id,kind,"Concrete",footprint,heights,new ExtrusionSolid(footprint,frame,heights),[],[],[]);
        var geometry=new GeometryModel([Component("Base",ComponentKind.Base,new Footprint(Rect(-825,-925,825,925),[]),new(-150,0)),
            Component("Neck",ComponentKind.Neck,new Footprint(Rect(cx-375,cy-475,cx+375,cy+475),
                [Rect(cx-250,cy-350,cx+250,cy+350,true)]),new(1600,2100))],[]);
        DimensionIntentPrimitive Dim(string name,Point2Mm at,double rotation)=>new(name,"Dimension/Linear","Plan",null,null,null,
            new(0,0),new(100,100),100,"<>",0,rotation,at);
        DrawingPrimitive[] dimensions=[Dim("top",new(0,1405),0),Dim("bottom",new(0,-1285),0),
            Dim("left",new(-1185,0),Math.PI/2),Dim("right",new(1305,0),Math.PI/2)];
        var markers=VhgSheetDecoration.CutMarkers(geometry,dimensions);
        Assert.Contains(markers,m=>m.Insertion==new Point2Mm(axisA,1585));
        Assert.Contains(markers,m=>m.Insertion==new Point2Mm(axisA,-1405));
        Assert.Contains(markers,m=>m.Insertion==new Point2Mm(-1365,axisB));
        Assert.Contains(markers,m=>m.Insertion==new Point2Mm(1425,axisB));
        var expanded=VhgSheetDecoration.CutMarkers(geometry,dimensions.Append(Dim("extra",new(0,1605),0)));
        Assert.Equal(1785,expanded.Single(m=>m.SemanticId.EndsWith("/Top")).Insertion.Y);
    }
    [Fact]
    public void CutMarkersUseNativePairsAtSourceStyleOffsets()
    {
        var markers=VhgSheetDecoration.CutMarkers(new(-750,-875,750,875));
        Assert.Equal(4,markers.Count);
        Assert.Equal(2,markers.Count(m=>m.BlockRole=="Block:MC1"));
        Assert.Equal(2,markers.Count(m=>m.BlockRole=="Block:MC2"));
        Assert.Contains(markers,m=>m.Insertion==new Point2Mm(-25,1055));
        Assert.All(markers,m=>Assert.Equal("Plan",m.ViewId));
    }
    [Fact]
    public void FrameUsesNativeTopLeftAndHasNoInventedAttributes()
    {
        var frame = VhgSheetDecoration.Frame("Presentation/1");
        Assert.Equal("Block:Khung", frame.BlockRole);
        Assert.Equal(new Point2Mm(0, 8910), frame.Insertion);
        Assert.Equal(0.3, frame.AnnotationScale);
        Assert.Empty(frame.Attributes);
    }

    [Fact]
    public void TitleUsesVerifiedNativeAttributeTags()
    {
        var title = VhgSheetDecoration.Title("Presentation/1", "Plan", "MẶT BẰNG", new(2295, 8610));
        Assert.Equal("Block:TenMC", title.BlockRole);
        Assert.Equal(0.3, title.AnnotationScale);
        Assert.Equal(["TIEUDE", "TYLE"], title.Attributes.Select(a => a.Key));
    }

    [Theory]
    [InlineData(20)]
    [InlineData(30)]
    [InlineData(40)]
    public void PackedNeckTitleFollowsItsFrameAndReportsActualViewScale(double scale)
    {
        var logical=new Bounds2Mm(-1500,2800,-500,4300);
        var frame=new SheetViewFrame(SheetFourViews.AAViewId,ViewPurpose.Section,logical,
            new(140,90,210,170),new(new(140,90),scale),"packed",[]);
        var position=SheetFourPlacement.Title(frame,30);
        Assert.Equal(new Point2Mm(175*30,178*30),position);
        var moved=new SheetViewFrame(frame.ViewId,frame.Purpose,logical,new(160,100,230,180),
            new(new(160,100),scale),"packed",[]);
        var movedPosition=SheetFourPlacement.Title(moved,30);
        Assert.Equal(20*30,movedPosition.X-position.X);
        Assert.Equal(10*30,movedPosition.Y-position.Y);
        var title=VhgSheetDecoration.Title("Presentation/4",frame.ViewId,"MẶT CẮT A-A",position,scale);
        Assert.Equal("Tỷ lệ 1:"+scale.ToString("0",System.Globalization.CultureInfo.InvariantCulture),
            Assert.Single(title.Attributes,a=>a.Key=="TYLE").Value);
        Assert.Equal(position,title.Insertion);
    }
}
