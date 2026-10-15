using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetTwoDimensionPlannerTests
{
    [Fact]
    public void NativeSheetTwoDimensionsUseTheTemplateStrokeFontWithoutChangingOtherAnnotations()
    {
        var style=SemanticRoleRegistry.Resolve(SheetTwoDimensionPlanner.Role)!;
        Assert.Equal(VhgPresentationStandards.RegularTextStyle,style.TextStyle);
        Assert.Equal(VhgPresentationStandards.DimensionStyle,style.DimStyle);
        Assert.Equal("VHG-Dim",style.Layer);
        Assert.Equal(VhgPresentationStandards.RegularTextStyle,SemanticRoleRegistry.Resolve(SheetTwoBarAnnotations.Role)!.TextStyle);
    }
    private static readonly string[] Views=["BaseTop","BaseBottom","Reinforcement/Wall/T1/Wall/T1/Outer/Top",
        "Reinforcement/Wall/T1/Wall/T1/Inner/Bottom","Reinforcement/Wall/T2/Wall/T2/Outer/Top",
        "Reinforcement/Wall/T2/Wall/T2/Inner/Bottom"];
    [Fact]
    public void NominalSheetTwoGridTilesConcreteUsingInputPitchAndSeparateHookBands()
    {
        var geometry=Geometry();
        var initial=SheetTwoDimensionPlanner.Build(geometry,["BaseTop"]);
        DrawingPrimitive[] bars=[
            new PolylinePrimitive("v0","Rebar/Top","BaseTop","Base","v0","D1",[new(-615,-740),new(-615,740)],false),
            new PolylinePrimitive("v1","Rebar/Top","BaseTop","Base","v1","D1",[new(615,-740),new(615,740)],false),
            new PolylinePrimitive("h0","Rebar/Top","BaseTop","Base","h0","D2",[new(-615,-740),new(615,-740)],false),
            new PolylinePrimitive("h1","Rebar/Top","BaseTop","Base","h1","D2",[new(-615,740),new(615,740)],false)];
        var metadata=bars.ToDictionary(b=>b.OccurrenceId!,b=>(Diameter:10,Pitch:(double?)150));
        var result=SheetTwoDetailDimensions.Build(geometry,initial.Cast<DrawingPrimitive>().Concat(bars),
            metadata.Keys.ToHashSet(),metadata);
        var x=result.Where(d=>d.SemanticId.Contains("/NominalSpacing/X/")).ToArray();
        var y=result.Where(d=>d.SemanticId.Contains("/NominalSpacing/Y/")).ToArray();
        Assert.Equal(new double[]{30,100,90,80,900,80,90,100,30},x.Select(d=>d.LogicalMeasurementMm));
        Assert.Equal(new double[]{30,100,90,130,1050,130,90,100,30},y.Select(d=>d.LogicalMeasurementMm));
        Assert.Equal("6x150=<>",x[4].Label);
        Assert.Equal("7x150=<>",y[4].Label);
        Assert.Equal(new Point2Mm(-750,-845),x[0].Start);
        Assert.Equal(new Point2Mm(-720,875),y[0].Start);
        var ticks=SheetTwoGach.Build(result,metadata.ToDictionary(k=>k.Key,k=>k.Value.Diameter));
        Assert.Equal(27,ticks.Count);
        Assert.All(ticks,t=>{Assert.Equal("Block:Gach",t.BlockRole);Assert.Equal(.3,t.AnnotationScale);});
        Assert.Equal(14,ticks.Count(t=>t.Insertion.X==-440));
        Assert.Equal(13,ticks.Count(t=>t.Insertion.Y==595));
        Assert.All(ticks.Where(t=>t.SemanticId.Contains("/Gach/Y/")),t=>Assert.Equal("D2",t.ScheduleMark));
        Assert.All(ticks.Where(t=>t.SemanticId.Contains("/Gach/X/")),t=>Assert.Equal("D1",t.ScheduleMark));
        var changed=metadata.ToDictionary(k=>k.Key,k=>(Diameter:10,Pitch:(double?)200));
        var alternate=SheetTwoDetailDimensions.Build(geometry,initial.Cast<DrawingPrimitive>().Concat(bars),
            metadata.Keys.ToHashSet(),changed).Where(d=>d.SemanticId.Contains("/NominalSpacing/X/")).ToArray();
        Assert.Equal(1500,alternate.Sum(d=>d.LogicalMeasurementMm));
        Assert.Equal("4x200=<>",alternate[4].Label);
        var split=new PolylinePrimitive("split","Rebar/Top","BaseTop","Base","split","D3",[new(-615,20),new(615,20)],false);
        var clipped=SheetTwoDetailDimensions.Build(geometry,initial.Cast<DrawingPrimitive>().Concat(bars).Append(split),
            metadata.Keys.Append("split").ToHashSet(),metadata);
        Assert.DoesNotContain(clipped,d=>d.SemanticId.Contains("/NominalSpacing/"));
    }
    [Fact]
    public void NominalWallCoverUsesItsPhysicalWallFootprintInsteadOfTheRoofOverhang()
    {
        string view=Views[4];
        var geometry=Geometry();var initial=SheetTwoDimensionPlanner.Build(geometry,[view]);
        DrawingPrimitive[] bars=[
            new PolylinePrimitive("v0","Rebar/Main",view,"Wall/T2","v0","T1",[new(-590,-115),new(-590,1350)],false),
            new PolylinePrimitive("v1","Rebar/Main",view,"Wall/T2","v1","T1",[new(590,-115),new(590,1350)],false),
            new PolylinePrimitive("h0","Rebar/Main",view,"Wall/T2","h0","T2",[new(-590,35),new(590,35)],false),
            new PolylinePrimitive("h1","Rebar/Main",view,"Wall/T2","h1","T2",[new(-590,1350),new(590,1350)],false)];
        var metadata=bars.ToDictionary(b=>b.OccurrenceId!,b=>(Diameter:10,Pitch:(double?)150));
        var result=SheetTwoDetailDimensions.Build(geometry,initial.Cast<DrawingPrimitive>().Concat(bars),metadata.Keys.ToHashSet(),metadata);
        var x=result.Where(d=>d.SemanticId.Contains("/NominalSpacing/X/")).ToArray();
        var y=result.Where(d=>d.SemanticId.Contains("/NominalSpacing/Y/")).ToArray();
        Assert.Equal(new double[]{30,90,130,1050,130,90,30},x.Select(d=>d.LogicalMeasurementMm));
        Assert.Equal(new double[]{45,92.5,1200,92.5,90,30},y.Select(d=>d.LogicalMeasurementMm));
        var ticks=SheetTwoGach.Build(result,metadata.ToDictionary(k=>k.Key,k=>k.Value.Diameter),geometry);
        Assert.Equal(20,ticks.Count);
        Assert.Equal(10,ticks.Count(t=>t.Insertion.X==-430));
        Assert.Equal(10,ticks.Count(t=>t.Insertion.Y==1205));
        Assert.DoesNotContain(ticks,t=>t.Insertion.X is -745 or 745);
    }
    [Fact]
    public void SixPanelsMeasurePhysicalSlabsAndClearWallsAtSourceDimensionLanes()
    {
        var result=SheetTwoDimensionPlanner.Build(Geometry(),Views);
        Assert.Equal(24,result.Count);
        var basis=result.Where(d=>d.ViewId=="BaseTop").ToArray();
        Assert.Equal(new double[]{1300,1500,1550,1750},basis.Select(d=>d.LogicalMeasurementMm).Order().ToArray());
        var width=Assert.Single(basis,d=>d.SemanticId.EndsWith("Outer/Width"));
        Assert.Equal(new Point2Mm(-750,875),width.Start); Assert.Equal(new Point2Mm(750,875),width.End);
        Assert.Equal(new Point2Mm(750,1235),width.DimensionLinePoint);
        var t1=result.Where(d=>d.ViewId==Views[2]).ToArray();
        Assert.Equal(new double[]{1000,1300,1400,1700},t1.Select(d=>d.LogicalMeasurementMm).Order().ToArray());
        var height=Assert.Single(t1,d=>d.SemanticId.EndsWith("Outer/Height"));
        Assert.Equal(new Point2Mm(650,1550),height.Start); Assert.Equal(new Point2Mm(650,-150),height.End);
        Assert.Equal(new Point2Mm(1110,-150),height.DimensionLinePoint);
        Assert.Equal(new double[]{1250,1400,1550,1700},result.Where(d=>d.ViewId==Views[4]).Select(d=>d.LogicalMeasurementMm).Order().ToArray());
        Assert.All(result,d=>{Assert.Equal("<>",d.Label);Assert.Null(d.OccurrenceId);Assert.Equal(SheetTwoDimensionPlanner.Role,d.Role);});
    }
    [Fact]
    public void ChangedPhysicalDimensionsAreMeasuredWithoutCopyingGoldenValues()
    {
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var changed=Geometry().Components.Select(c=>
        {
            var footprint=new Footprint(new Polygon2Mm(c.Footprint.Outer.Vertices
                .Select(p=>new Point2Mm(p.X*1.2,p.Y*1.2))),[]);
            var z=new Interval1D(c.ZRange.Start*1.25,c.ZRange.End*1.25);
            return new ComponentGeometry(c.Id,c.Kind,c.MaterialId,footprint,z,
                new ExtrusionSolid(footprint,frame,z),[],[],[]);
        });
        var result=SheetTwoDimensionPlanner.Build(new GeometryModel(changed,[]),Views);
        Assert.Equal(24,result.Count);
        Assert.Equal(1800,Assert.Single(result,d=>d.SemanticId=="BaseTop/Dimension/Outer/Width").LogicalMeasurementMm);
        Assert.Equal(2100,Assert.Single(result,d=>d.SemanticId=="BaseTop/Dimension/Outer/Height").LogicalMeasurementMm);
        Assert.Equal(2125,Assert.Single(result,d=>d.SemanticId==Views[2]+"/Dimension/Outer/Height").LogicalMeasurementMm);
        Assert.Equal(1500,Assert.Single(result,d=>d.SemanticId==Views[4]+"/Dimension/Inner/Width").LogicalMeasurementMm);
    }
    [Fact]
    public void DetailChainsMeasureSlabEdgesAndWallBaseExtensionsFromGeometry()
    {
        var geometry=Geometry();
        var initial=SheetTwoDimensionPlanner.Build(geometry,Views);
        var details=SheetTwoDetailDimensions.Build(geometry,initial,new HashSet<string>());
        Assert.Equal(32,details.Count);
        var baseEdge=Assert.Single(details,d=>d.SemanticId=="BaseTop/Dimension/Detail/Width/Left");
        Assert.Equal(100,baseEdge.LogicalMeasurementMm);
        Assert.Equal(new Point2Mm(-650,1055),baseEdge.DimensionLinePoint);
        var wallExtension=Assert.Single(details,d=>d.SemanticId==Views[2]+"/Dimension/Detail/BaseExtension/Left");
        Assert.Equal(100,wallExtension.LogicalMeasurementMm);
        Assert.Equal(new Point2Mm(-750,0),wallExtension.End);
        Assert.Empty(SheetTwoDetailDimensions.Build(geometry,[],new HashSet<string>()));
    }
    [Fact]
    public void SpacingMeasuresActualBarCentersAndRetainsOpeningClipsWithoutInventing150()
    {
        var geometry=Geometry();
        var initial=SheetTwoDimensionPlanner.Build(geometry,["BaseTop"]);
        var a=new PolylinePrimitive("a","Rebar/Top","BaseTop","Base","a","D1",
            [new(-615,-740),new(615,-740)],false);
        var b=new PolylinePrimitive("b","Rebar/Top","BaseTop","Base","b","D1",
            [new(-615,-592),new(615,-592)],false);
        var accessory=new PolylinePrimitive("extra","Rebar/Top","BaseTop","Base","extra","D2",
            [new(-615,-700),new(615,-700)],false);
        var details=SheetTwoDetailDimensions.Build(geometry,initial.Cast<DrawingPrimitive>().Concat([a,b,accessory]),
            new HashSet<string>{"a","b"});
        var spacing=Assert.Single(details,d=>d.SemanticId=="BaseTop/Dimension/Spacing/Y/001");
        Assert.Equal(148,spacing.LogicalMeasurementMm);
        Assert.Equal(new Point2Mm(-615,-592),spacing.Start);
        Assert.Equal(new Point2Mm(-615,-740),spacing.End);
        Assert.Equal("<>",spacing.Label);
        Assert.Equal(3,details.Count(d=>d.SemanticId.Contains("/Spacing/Y/")));
        Assert.DoesNotContain(details,d=>d.OccurrenceId=="extra");
    }
    [Fact]
    public void RepeatedSpacingUsesMeasuredCoreSpanAndEveryEdgeBandStillTilesTheWholeSlab()
    {
        var geometry=Geometry();var initial=SheetTwoDimensionPlanner.Build(geometry,["BaseTop"]);
        var bars=Enumerable.Range(0,10).Select(i=>new PolylinePrimitive("v"+i,"Rebar/Top","BaseTop","Base",
            "v"+i,"D1",[new(-615+i*1230d/9,-740),new(-615+i*1230d/9,740)],false)).ToArray();
        var result=SheetTwoDetailDimensions.Build(geometry,initial.Cast<DrawingPrimitive>().Concat(bars),
            bars.Select(b=>b.OccurrenceId!).ToHashSet(StringComparer.Ordinal));
        var chain=result.Where(d=>d.SemanticId.Contains("/Spacing/X/")).ToArray();
        Assert.Equal(9,chain.Length);
        Assert.Equal(1500,chain.Sum(d=>d.LogicalMeasurementMm),7);
        var core=Assert.Single(chain,d=>d.Label!="<>");
        Assert.Equal(410,core.LogicalMeasurementMm,7);
        Assert.Equal("3x136.667\\X=<>",core.Label);
        Assert.DoesNotContain(chain,d=>d.Label.Contains("150="));
        var irregular=bars.Select((bar,i)=>i==5?new PolylinePrimitive(bar.SemanticId,bar.Role,bar.ViewId,
            bar.ComponentId,bar.OccurrenceId,bar.ScheduleMark,
            bar.Points.Select(p=>new Point2Mm(p.X+5,p.Y)),false):bar).ToArray();
        var uneven=SheetTwoDetailDimensions.Build(geometry,initial.Cast<DrawingPrimitive>().Concat(irregular),
            irregular.Select(b=>b.OccurrenceId!).ToHashSet(StringComparer.Ordinal))
            .Where(d=>d.SemanticId.Contains("/Spacing/X/")).ToArray();
        Assert.Equal(11,uneven.Length);
        Assert.All(uneven,d=>Assert.Equal("<>",d.Label));
        Assert.Equal(1500,uneven.Sum(d=>d.LogicalMeasurementMm),7);
    }
    [Fact]
    public void OtherSheetsAndIncompleteGeometryDoNotGainInventedDimensions()
    {
        var geometry=Geometry();
        Assert.Empty(SheetTwoDimensionPlanner.Build(geometry,["Plan","SteelAA","Reinforcement/Wall/T3/Wall/T3/Outer/Top"]));
        Assert.Empty(SheetTwoDimensionPlanner.Build(new GeometryModel(geometry.Components.Where(c=>c.Id!="Wall/T4"),[]),Views));
    }
    private static GeometryModel Geometry()
    {
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        ComponentGeometry Rect(string id,ComponentKind kind,double x0,double y0,double x1,double y1,double z0,double z1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
            return new(id,kind,"Concrete",footprint,new(z0,z1),new ExtrusionSolid(footprint,frame,new(z0,z1)),[],[],[]);
        }
        return new([Rect("Base",ComponentKind.Base,-750,-875,750,875,-150,0),
            Rect("Roof",ComponentKind.Roof,-650,-775,650,775,1400,1550),
            Rect("Wall/T1",ComponentKind.Wall,-650,625,650,775,0,1400),
            Rect("Wall/T2",ComponentKind.Wall,500,-625,650,625,0,1400),
            Rect("Wall/T3",ComponentKind.Wall,-650,-775,650,-625,0,1400),
            Rect("Wall/T4",ComponentKind.Wall,-650,-625,-500,625,0,1400)],[]);
    }
}
