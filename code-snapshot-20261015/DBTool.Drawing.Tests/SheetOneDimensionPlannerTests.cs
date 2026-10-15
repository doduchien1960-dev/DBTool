using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public class SheetOneDimensionPlannerTests
{
    [Fact]
    public void RotatedDimensionPreservesExtensionOriginsAndAxisAcrossJson()
    {
        var dimension = new DimensionIntentPrimitive("d", "Dimension/Linear", "AA", null, null, null,
            new(-975,-250),new(-875,-150),100,"<>",0,0,new(-875,-430));
        var bounds = new Bounds2Mm(-1500,-1000,1500,2500);
        var package = new DrawingPackage(new string('a',64),"test",[new("AA",ViewPurpose.Section,new(0,0),bounds,30)],
            [dimension],bounds,CalculationScope.FullRebar);
        var restored = Assert.IsType<DimensionIntentPrimitive>(Assert.Single(DrawingPackageJson.Deserialize(DrawingPackageJson.Serialize(package)).Primitives));
        Assert.Equal(100,restored.LogicalMeasurementMm);
        Assert.Equal(0,restored.RotationRadians);
        Assert.Equal(dimension.DimensionLinePoint,restored.DimensionLinePoint);
        Assert.Equal(dimension.Start,restored.Start);
        Assert.Equal(dimension.End,restored.End);
    }

    [Fact]
    public void ChainsMeasureExistingGeometryAndDoNotUseTrimmedWallHeightAsClearHeight()
    {
        var geometry = new GeometryModel([
            Component("Pad",ComponentKind.Pad,-750,-875,750,875,-250,-150),
            Component("Base",ComponentKind.Base,-650,-775,650,775,-150,0),
            Component("Roof",ComponentKind.Roof,-650,-775,650,775,1400,1550),
            Component("Neck",ComponentKind.Neck,-325,-750,625,200,1550,2080),
            Component("Collar",ComponentKind.Collar,-325,-750,625,200,2030,2080),
            Component("Wall/T1",ComponentKind.Wall,500,-625,650,625,0,1385),
            Component("Wall/T3",ComponentKind.Wall,-650,-625,-500,625,0,1385),
            Component("Wall/T2",ComponentKind.Wall,-650,-775,650,-625,0,1385),
            Component("Wall/T4",ComponentKind.Wall,-650,625,650,775,0,1385)],[]);
        var dimensions = SheetOneDimensionPlanner.Build(geometry,"AA")!.Cast<DimensionIntentPrimitive>().ToArray();
        Assert.Contains(dimensions,d=>d.Label=="Htb=<>" && d.LogicalMeasurementMm==1400);
        Assert.Contains(dimensions,d=>d.Label=="Htb=<>" && d.LogicalMeasurementMm==2280);
        Assert.Contains(dimensions,d=>d.Label=="Ctb=<>" && d.LogicalMeasurementMm==480);
        Assert.Contains(dimensions,d=>d.SemanticId.Contains("/Base/") && d.LogicalMeasurementMm==1250);
        Assert.Equal(1385,geometry.Components.Single(c=>c.Id=="Wall/T1").ZRange.End);
        Assert.Null(SheetOneDimensionPlanner.Build(geometry,"BaseTop"));
        var plan=SheetOneDimensionPlanner.Build(geometry,"Plan")!.OfType<DimensionIntentPrimitive>().ToArray();
        var firstBottom=Assert.Single(plan,d=>d.SemanticId.EndsWith("/X/2"));
        Assert.Equal(new Point2Mm(-650,-775),firstBottom.Start);
        Assert.Equal(new Point2Mm(-500,-625),firstBottom.End);
        Assert.Equal(new Point2Mm(-500,-955),firstBottom.DimensionLinePoint);
        var leftChain=Assert.Single(plan,d=>d.SemanticId.EndsWith("/Y/2"));
        Assert.Equal(new Point2Mm(-830,625),leftChain.DimensionLinePoint);
        var pipeFrame=WallFrame.Create(WallId.T1,1000,1250);
        var opening=new OpeningProfile("Pipe/T1","Wall/T1",OpeningKind.Round,new CircleProfile(new(70,650),200),
            new CircleProfile(new(70,650),255),new CircleProfile(new(70,650),305),pipeFrame,
            new Frame3Mm(pipeFrame.Origin,pipeFrame.Left,pipeFrame.Up,pipeFrame.Outward),new(345,955),new(0,150),[]);
        var pipedComponents=geometry.Components.Select(c=>c.Id==opening.OwnerId
            ?new ComponentGeometry(c.Id,c.Kind,c.MaterialId,c.Footprint,c.ZRange,c.Occupied,[opening],c.FacePatches,c.Sources):c);
        var pipeDimensions=SheetOneDimensionPlanner.Build(new GeometryModel(pipedComponents,[opening]),"Plan")!
            .OfType<DimensionIntentPrimitive>().Where(d=>d.SemanticId.Contains("/Pipe/")).ToArray();
        Assert.Equal(new double[]{315,55,400,55,175},pipeDimensions.Select(d=>d.LogicalMeasurementMm));
        Assert.Equal(new Point2Mm(500,625),pipeDimensions[0].Start);
        Assert.Equal(new Point2Mm(185,1175),pipeDimensions[0].End);
        Assert.Equal(new Point2Mm(185,1355),pipeDimensions[0].DimensionLinePoint);
        var pipeSection=SheetOneDimensionPlanner.Build(new GeometryModel(pipedComponents,[opening]),"AA")!
            .OfType<DimensionIntentPrimitive>().Where(d=>d.SemanticId.Contains("/Side/1/")).ToArray();
        Assert.Equal(new double[]{100,150,395,55,400,55,495,150,480},pipeSection.Select(d=>d.LogicalMeasurementMm));
        Assert.All(pipeSection,d=>Assert.Equal(1375,d.DimensionLinePoint!.Value.X));
        Assert.DoesNotContain(pipeSection,d=>d.Label=="Htb=<>");
        var steelDimensions=SheetOneDimensionPlanner.Build(geometry,"SteelAA")!.OfType<DimensionIntentPrimitive>().ToArray();
        Assert.DoesNotContain(steelDimensions,d=>d.SemanticId.EndsWith("/BaseOverall"));
        var steelOverall=Assert.Single(steelDimensions,d=>d.SemanticId.EndsWith("/Overall"));
        Assert.Equal(new Point2Mm(0,-610),steelOverall.DimensionLinePoint);
        Assert.Equal(1750,steelOverall.LogicalMeasurementMm);
        Assert.Equal(5,steelDimensions.Count(d=>d.SemanticId.Contains("/Base/"))); // This fixture has no separate base expansion.
        Assert.DoesNotContain(dimensions,d=>d.SemanticId.Contains("/Haunch/"));
        var haunchFootprint=new Footprint(new Polygon2Mm([new(-500,-625),new(500,-625),new(500,625),new(-500,625)]),
            [new Polygon2Mm([new(-425,-550),new(-425,550),new(425,550),new(425,-550)])]);
        var range=new Interval1D(0,90);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var haunch=new ComponentGeometry("Base/Haunch",ComponentKind.Base,"Concrete",haunchFootprint,range,
            new ExtrusionSolid(haunchFootprint,frame,range),[],[],[]);
        var withHaunch=new GeometryModel(geometry.Components.Append(haunch),[]);
        var measured=SheetOneDimensionPlanner.Build(withHaunch,"AA")!.OfType<DimensionIntentPrimitive>().ToArray();
        Assert.Contains(measured,d=>d.SemanticId.EndsWith("Haunch/Width") && d.LogicalMeasurementMm==75);
        Assert.Contains(measured,d=>d.SemanticId.EndsWith("Haunch/Height") && d.LogicalMeasurementMm==90);
        Assert.DoesNotContain(SheetOneDimensionPlanner.Build(withHaunch,"SteelAA")!,d=>d.SemanticId.Contains("/Haunch/"));
        Assert.Equal(90,haunch.ZRange.End);
    }

    private static ComponentGeometry Component(string id,ComponentKind kind,double x0,double y0,double x1,double y1,double z0,double z1)
    {
        var footprint = new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
        var range = new Interval1D(z0,z1);
        var frame = new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        return new(id,kind,"Concrete",footprint,range,new ExtrusionSolid(footprint,frame,range),[],[],[]);
    }
}
