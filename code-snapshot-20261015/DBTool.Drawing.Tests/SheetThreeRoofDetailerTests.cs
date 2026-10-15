using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetThreeRoofDetailerTests
{
    [Theory]
    [InlineData(1450,1650,150,30,8,9)]
    [InlineData(1300,1550,150,30,7,8)]
    [InlineData(1700,1950,200,40,7,8)]
    public void NominalDimensionChainsConsumeMeshAndCoverWithoutMovingPhysicalBars(double width,double length,
        double pitch,double cover,int nx,int ny)
    {
        var f=new Footprint(new Polygon2Mm([new(-width/2,-length/2),new(width/2,-length/2),
            new(width/2,length/2),new(-width/2,length/2)]),[]);
        var z=new Interval1D(1450,1600);var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var g=new GeometryModel([new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",f,z,
            new ExtrusionSolid(f,frame,z),[],[],[])],[]);
        string view=SheetThreeRoofViews.TopViewId;
        var bars=new DrawingPrimitive[]{
            new PolylinePrimitive("h","Rebar/Main",view,"Roof","h","N001",[new(-width/2+cover+5,0),new(width/2-cover-5,0)],false),
            new PolylinePrimitive("v","Rebar/Main",view,"Roof","v","N002",[new(0,-length/2+cover+5),new(0,length/2-cover-5)],false)};
        var result=SheetThreeRoofDetailer.Build(g,bars,view,new MeshSpec(10,pitch),cover,new HashSet<string>{"h","v"});
        var chains=result.OfType<DimensionIntentPrimitive>().Where(p=>p.SemanticId.Contains("/NominalSpacing/")).ToArray();
        Assert.Equal(10,chains.Length);
        foreach(var axis in new[]{"X","Y"})
        {
            var dims=chains.Where(p=>p.SemanticId.Contains("/NominalSpacing/"+axis+"/")).ToArray();
            double span=axis=="X"?width:length;int n=axis=="X"?nx:ny;
            Assert.Equal(span,dims.Sum(p=>p.LogicalMeasurementMm),7);
            Assert.Equal(cover,dims[0].LogicalMeasurementMm);
            Assert.Equal(cover,dims[^1].LogicalMeasurementMm);
            Assert.Equal(n*pitch,dims[2].LogicalMeasurementMm);
            Assert.Equal(n+"x"+pitch+"=<>",dims[2].Label);
            Assert.All(dims,p=>Assert.Contains(p.OccurrenceId,new[]{"h","v"}));
        }
        Assert.All(bars,p=>Assert.Same(p,result.Single(q=>q.SemanticId==p.SemanticId)));
        Assert.DoesNotContain(SheetThreeRoofDetailer.Build(g,bars,view),p=>p.SemanticId.Contains("/NominalSpacing/"));
        Assert.DoesNotContain(SheetThreeRoofDetailer.Build(g,bars,view,new MeshSpec(10,pitch),cover,new HashSet<string>()),
            p=>p.SemanticId.Contains("/NominalSpacing/"));
    }
    [Theory]
    [InlineData(1450,1650,-325,325,500,700,false)]
    [InlineData(1700,1950,200,-250,600,800,false)]
    [InlineData(1450,1650,-325,325,500,700,true)]
    [InlineData(1700,1950,200,-250,600,800,true)]
    public void RoofAndOpeningDimensionsFollowActualGeometryWithoutReplacingPhysicalBars(double width,double length,
        double cx,double cy,double holeWidth,double holeLength,bool bottomLayer)
    {
        Polygon2Mm Rectangle(double x0,double y0,double x1,double y1)=>new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
        var hole=new Polygon2Mm(Rectangle(cx-holeWidth/2,cy-holeLength/2,cx+holeWidth/2,cy+holeLength/2).Vertices.Reverse());
        var footprint=new Footprint(Rectangle(-width/2,-length/2,width/2,length/2),[hole]);
        var interval=new Interval1D(1450,1600);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",footprint,interval,
            new ExtrusionSolid(footprint,frame,interval),[],[],[]);
        ComponentGeometry Component(string id,ComponentKind kind,Polygon2Mm polygon)
        {
            var f=new Footprint(polygon,[]);
            return new(id,kind,"Concrete",f,new(0,1450),new ExtrusionSolid(f,frame,new(0,1450)),[],[],[]);
        }
        var geometry=new GeometryModel([roof,
            Component("Wall/T1",ComponentKind.Wall,Rectangle(-width/2,length/2-150,width/2,length/2)),
            Component("Wall/T2",ComponentKind.Wall,Rectangle(width/2-150,-length/2+150,width/2,length/2-150)),
            Component("Wall/T3",ComponentKind.Wall,Rectangle(-width/2,-length/2,width/2,-length/2+150)),
            Component("Wall/T4",ComponentKind.Wall,Rectangle(-width/2,-length/2+150,-width/2+150,length/2-150)),
            Component("Neck",ComponentKind.Neck,Rectangle(cx-holeWidth/2-125,cy-holeLength/2-125,
                cx+holeWidth/2+125,cy+holeLength/2+125))],[]);
        string view=bottomLayer?SheetThreeRoofViews.BottomViewId:SheetThreeRoofViews.TopViewId;
        var bars=new DrawingPrimitive[]{
            new PolylinePrimitive("bar/left","Rebar/Main",view,"Roof","occurrence/left","N001",
                [new(-width/2+30,cy),new(cx-holeWidth/2-15,cy)],false),
            new PolylinePrimitive("bar/right","Rebar/Main",view,"Roof","occurrence/right","N002",
                [new(cx+holeWidth/2+15,cy),new(width/2-30,cy)],false)};
        var result=SheetThreeRoofDetailer.Build(geometry,bars,view);
        Assert.All(bars,p=>Assert.Same(p,result.Single(q=>q.SemanticId==p.SemanticId)));
        var dims=result.OfType<DimensionIntentPrimitive>().ToArray();
        Assert.Equal(width,Assert.Single(dims,p=>p.SemanticId.EndsWith("/Dimension/Width")).LogicalMeasurementMm);
        Assert.Equal(length,Assert.Single(dims,p=>p.SemanticId.EndsWith("/Dimension/Height")).LogicalMeasurementMm);
        Assert.Equal(holeWidth,Assert.Single(dims,p=>p.SemanticId.EndsWith("Hole/0/Width")).LogicalMeasurementMm);
        Assert.Equal(holeLength,Assert.Single(dims,p=>p.SemanticId.EndsWith("Hole/0/Height")).LogicalMeasurementMm);
        var drawnHole=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.Role=="Geometry/RoofOpening");
        Assert.Equal(hole.Vertices.ToArray(),drawnHole.Points.ToArray());
        // SH belongs to the callout block; a second mark Text at its insertion is a duplicate.
        Assert.Empty(result.OfType<TextPrimitive>());
        Assert.Empty(result.OfType<BlockPrimitive>()); // Actual metadata was not supplied to this geometry-only case.
        Assert.Equal(2,result.OfType<PolylinePrimitive>().Count(p=>p.Role=="Rebar/Main"));
        var chamber=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId.EndsWith("/Hidden/Chamber"));
        Assert.Equal(new Point2Mm(-width/2+150,length/2-150),chamber.Points[0]);
        Assert.Equal(chamber.Points[0],chamber.Points[^1]);
        var neck=Assert.Single(result.OfType<PolylinePrimitive>(),p=>p.SemanticId.EndsWith("/Hidden/Neck"));
        Assert.Equal(geometry.Components.Single(c=>c.Id=="Neck").Footprint.Outer.Vertices.ToArray(),neck.Points.ToArray());
        Assert.Equal(width-300,Assert.Single(dims,p=>p.SemanticId.EndsWith("/Dimension/Chamber/X/Width")).LogicalMeasurementMm);
        Assert.Equal(length-300,Assert.Single(dims,p=>p.SemanticId.EndsWith("/Dimension/Chamber/Y/Height")).LogicalMeasurementMm);
        Assert.Equal(3,dims.Count(p=>p.SemanticId.Contains("/Dimension/Chamber/X/")));
        Assert.Equal(3,dims.Count(p=>p.SemanticId.Contains("/Dimension/Chamber/Y/")));
        Assert.Equal(width/2+(bottomLayer?360:660),
            Assert.Single(dims,p=>p.SemanticId.EndsWith("/Dimension/Height")).DimensionLinePoint!.Value.X);
    }

    [Fact]
    public void RoofDetailerCannotInventDowelHolesFromRemovableCoverName()
    {
        double width = 1450, length = 1650, holeW = 820, holeL = 570;
        var holePts = new[] { new Point2Mm(-holeW/2, -holeL/2), new Point2Mm(holeW/2, -holeL/2), new Point2Mm(holeW/2, holeL/2), new Point2Mm(-holeW/2, holeL/2) };
        var hole = new Polygon2Mm(holePts.Reverse());
        var footprint = new Footprint(new Polygon2Mm([new(-width/2, -length/2), new(width/2, -length/2), new(width/2, length/2), new(-width/2, length/2)]), [hole]);
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var roof = new ComponentGeometry("Roof", ComponentKind.Roof, "Concrete", footprint, new(1400, 1600), new ExtrusionSolid(footprint, frame, new(1400, 1600)), [], [], []);
        var geometry = new GeometryModel([roof], []);
        var bar = new PolylinePrimitive("bar1", "Rebar/Main", SheetThreeRoofViews.TopViewId, "Roof", "occ1", "N1", [new(-width/2 + 50, 0), new(width/2 - 50, 0)], false);

        var result = SheetThreeRoofDetailer.Build(geometry, [bar], SheetThreeRoofViews.TopViewId,
            new MeshSpec(10, 150), 30.0, new HashSet<string> { "occ1" }, manholeCode: "G800-NR");

        // No physical collar occurrence was supplied; geometry detailer cannot invent reinforcement.
        Assert.DoesNotContain(result,p=>p.Role=="Rebar/Secondary");

        // No dowel configuration or physical placements were supplied. NR is identity only.
        var dowelCircles = result.OfType<CirclePrimitive>().Where(c => c.SemanticId.Contains("DowelHole")).ToArray();
        Assert.Empty(dowelCircles);
        Assert.DoesNotContain(result.OfType<TextPrimitive>(),t=>t.Role=="Note/RoofDowel");

        // 3. Verify callout block with attributes
        var calloutBlock = Assert.Single(result.OfType<BlockPrimitive>(), b => b.Role == SheetTwoBarAnnotations.Role);
        Assert.Contains(calloutBlock.Attributes, a => a.Key == "SH" && a.Value == "N1");
        Assert.Contains(calloutBlock.Attributes, a => a.Key == "DK" && a.Value == "%%c10");
        Assert.Contains(calloutBlock.Attributes, a => a.Key == "A" && a.Value == "@150");
    }
}
