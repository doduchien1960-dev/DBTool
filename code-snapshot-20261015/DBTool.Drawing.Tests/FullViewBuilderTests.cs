using System.Security.Cryptography;
using System.Text;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class FullViewBuilderTests
{
    [Fact]
    public void NativeNeckCannotAddBendingShapesFromAnUnrelatedNamedStore()
    {
        var seed=FullInputs();
        var path=new BarPath([new LineSegment3Mm(new(-200,0,1400),new(-200,0,1590))],false,HookPattern.Hook0M,0,false,MeasurementBasis.TangentPath);
        var neck=new BarOccurrence("neck-real",null,"Neck",ComponentKind.Neck,Layer.Bottom,Axis.Y,10,path,190,190,
            LengthStatus.Known,"TEST",[Source],RebarDirection.Vertical,"Neck/T1",RebarSurface.Inner,"Neck");
        var row=new SteelScheduleRow("C001","neck",ComponentKind.Neck,Layer.Bottom,Axis.Y,10,1,190,190,.617,
            new MassKg(.11723),[neck.Id],"TEST",RebarDirection.Vertical);
        var mass=new MassKg(seed.SteelSchedule.TotalMassKg.Value+row.MassKg.Value);
        var schedule=new SteelSchedule(seed.SteelSchedule.Rows.Append(row),[new(ComponentKind.Base,seed.SteelSchedule.TotalMassKg),new(ComponentKind.Neck,row.MassKg)],mass,"D2Over162","v1");
        var inputs=new DrawingInputs(seed.Geometry,new RebarLayout(seed.RebarLayout.Occurrences.Append(neck),[],CalculationScope.FullRebar),schedule,null,seed.Quantities);
        var result=ViewBuilder.Build(inputs,Presentation(30,SheetOnePresentationPolicy.Family21Theme),Identity);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        Assert.DoesNotContain(result.Value!.Views,v=>v.Id==SheetFiveViews.ViewId);
        Assert.Contains(result.Value.Primitives.OfType<PathPrimitive>(),p=>p.Purpose==ViewPurpose.Schematic && p.SourceOccurrenceId==neck.Id);
    }
    [Theory]
    [InlineData(0,30)]
    [InlineData(90,30)]
    [InlineData(180,30)]
    [InlineData(270,30)]
    [InlineData(0,50)]
    [InlineData(90,50)]
    public void OwnerCoverOutlineUsesDomainSectionsWithoutScalingNativeBlock(double angle,double paperScale)
    {
        var seed=S1Inputs();
        var aperture=new RoundedRectangleProfile(new(12,22),new(820,570),20);
        var outlines=CoverSectionOutlineBuilder.Build(aperture,new(100,200),1600,angle,new(125,80,[Source])).Value!;
        var bounds=new Rectangle2Mm(-500,-500,500,500);
        var blocks=new CoverBlockSet(new[]{"MB","AA","BB"}.Select(v=>new CoverBlockView("C."+v,v,new(0,0,0),bounds,
            v=="MB"?new(12,22):v=="AA"?new(17,-3):new(-9,5),[Source])),aperture,[],[Source]);
        var placement=CoverBlockPlacementResolver.Resolve(blocks,new(100,200),1600,angle).Value!;
        var cover=new PurchasedCoverPlacement("C",new(placement.PlanInsertion.X,placement.PlanInsertion.Y,1600),new(1000,1000),1,angle,
            bounds,[Source],aperture,placement,outlines);
        var geometry=new GeometryModel(seed.Geometry.Components,seed.Geometry.Openings,[cover]);
        var result=ViewBuilder.Build(new DrawingInputs(geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities),Presentation(paperScale),Identity);
        Assert.True(result.Success);
        foreach(var view in new[]{"AA","BB"})
        {
            var rect=view=="AA"?outlines.AA:outlines.BB;
            var outline=Assert.Single(result.Value!.Primitives.OfType<PolylinePrimitive>(),p=>p.SemanticId==$"PurchasedCover/0/{view}/Outline");
            Assert.True(outline.Closed);
            Assert.Equal(new[]{new Point2Mm(rect.MinX,rect.MaxY),new Point2Mm(rect.MaxX,rect.MaxY),
                new Point2Mm(rect.MaxX,rect.MinY),new Point2Mm(rect.MinX,rect.MinY)},outline.Points);
            var native=Assert.Single(result.Value.Primitives.OfType<BlockPrimitive>(),p=>p.SemanticId==$"PurchasedCover/0/{view}");
            Assert.Equal(1,native.AnnotationScale);
            Assert.Equal((view=="AA"?placement.SectionAA:placement.SectionBB).Insertion,native.Insertion);
            var extent=Assert.Single(result.Value.Views,v=>v.Id==view).Bounds!;
            Assert.True(extent.MaxY>=rect.MaxY && extent.MinY<=rect.MinY);
        }
        Assert.Equal(seed.Geometry.Components,geometry.Components); // No fabricated concrete component/volume.
        var restored=DrawingPackageJson.Deserialize(DrawingPackageJson.Serialize(result.Value!));
        Assert.Equal(2,restored.Primitives.OfType<PolylinePrimitive>().Count(p=>p.SemanticId.StartsWith("PurchasedCover/0/",StringComparison.Ordinal)));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ResolvedPurchasedCoverUsesIndependentSectionOriginsAndMirroringWithoutFitScaling(double scale)
    {
        var seed=S1Inputs();
        var resolved=new CoverBlockPlacements(new(100,200),90,
            new("BB",new(207,1595),1),new("AA",new(112,1603),-1));
        var cover=new PurchasedCoverPlacement("C",new(100,200,1600),new(200,100),scale,90,
            new(-40,-10,160,90),[Source],nativeViewPlacement:resolved);
        var geometry=new GeometryModel(seed.Geometry.Components,seed.Geometry.Openings,[cover]);
        var result=ViewBuilder.Build(new DrawingInputs(geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities),Presentation(30),Identity);
        if(scale!=1)
        {
            Assert.False(result.Success);Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_NATIVE_SCALE_INVALID" && d.Severity==DiagnosticSeverity.Block);
            return;
        }
        Assert.True(result.Success);
        var aa=Assert.Single(result.Value!.Primitives.OfType<BlockPrimitive>(),p=>p.SemanticId=="PurchasedCover/0/AA");
        var bb=Assert.Single(result.Value.Primitives.OfType<BlockPrimitive>(),p=>p.SemanticId=="PurchasedCover/0/BB");
        Assert.Equal("Block:C.BB",aa.BlockRole);Assert.Equal("Block:C.AA",bb.BlockRole);
        Assert.Equal(new Point2Mm(207,1595),aa.Insertion);Assert.Equal(new Point2Mm(112,1603),bb.Insertion);
        Assert.True(bb.MirrorHorizontal);
        Assert.Equal(0,bb.RotationRadians);Assert.Equal(1,bb.AnnotationScale);
        var roundTrip=DrawingPackageJson.Deserialize(DrawingPackageJson.Serialize(result.Value));
        var restored=Assert.Single(roundTrip.Primitives.OfType<BlockPrimitive>(),p=>p.SemanticId==bb.SemanticId);
        Assert.True(restored.MirrorHorizontal);
    }

    [Fact]
    public void NativePlanHidesBuriedHaunchWhileRetainingItsPhysicalSection()
    {
        var seed=S1Inputs();
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var haunch=Component("Base/Haunch",ComponentKind.Base,"Concrete",-400,-400,400,400,0,90,frame);
        var geometry=new GeometryModel(seed.Geometry.Components.Append(haunch),[]);
        var inputs=new DrawingInputs(geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities);
        var native=ViewBuilder.Build(inputs,Presentation(30,SheetOnePresentationPolicy.Family21Theme),Identity);
        Assert.True(native.Success);
        Assert.DoesNotContain(native.Value!.Primitives,p=>p.ViewId=="Plan" && p.ComponentId==haunch.Id);
        // A same-pour haunch now shares the base hatch and outline, rather than owning a false seam.
        Assert.Contains(native.Value.Primitives.OfType<HatchPrimitive>(),p=>p.ViewId=="AA" &&
            p.OuterLoop.Any(v=>Math.Abs(v.Y-90)<1e-7 && Math.Abs(Math.Abs(v.X)-400)<1e-7));
        Assert.Same(haunch,geometry.Components.Single(c=>c.Id==haunch.Id));
        var generic=ViewBuilder.Build(inputs,Presentation(30),Identity);
        Assert.Contains(generic.Value!.Primitives,p=>p.ViewId=="Plan" && p.ComponentId==haunch.Id);
    }
    [Fact]
    public void NativeVinCoverUsesTheNativeBlockLayersRatherThanAnAbsentCatalogLayer()
    {
        var seed=S1Inputs();
        var cover=new PurchasedCoverPlacement("VIN.TNT.VH.700x700",new(100,200,1600),new(800,800),1,0,new(-400,-400,400,400),[Source]);
        var geometry=new GeometryModel(seed.Geometry.Components,seed.Geometry.Openings,[cover]);
        var result=ViewBuilder.Build(new DrawingInputs(geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities),Presentation(30,SheetOnePresentationPolicy.Family21Theme),Identity);
        Assert.True(result.Success);
        var blocks=result.Value!.Primitives.OfType<BlockPrimitive>().Where(p=>p.SemanticId.StartsWith("PurchasedCover/",StringComparison.Ordinal)).ToArray();
        Assert.Equal(3,blocks.Length);
        Assert.All(blocks,p=>Assert.StartsWith("Block:VIN.TNT.VH.700x700.",p.BlockRole));
        Assert.All(blocks,p=>Assert.Equal("VHG-Nap1",SemanticRoleRegistry.Resolve(p.Role)!.Layer));
    }
    [Fact]
    public void SheetTwoUsesTypedNominalPitchWithoutChangingThePhysicalBarAndReservesCalloutBounds()
    {
        var seed=S1Inputs();
        var inputs=new DrawingInputs(seed.Geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities,
            baseBottom:new MeshSpec(10,175));
        var presentation=new PresentationRequest(30,ElevationMode.Symbolic,null,SheetOnePresentationPolicy.Family21Theme,true);
        var result=ViewBuilder.Build(inputs,presentation,Identity);
        Assert.True(result.Success);
        var block=Assert.Single(result.Value!.Primitives.OfType<BlockPrimitive>(),p=>p.Role==SheetTwoBarAnnotations.Role);
        Assert.Equal("@175",block.Attributes.Single(a=>a.Key=="A").Value);
        Assert.Equal("bar-001",block.OccurrenceId);
        Assert.Equal("D001",block.ScheduleMark);
        var bounds=Assert.Single(result.Value.Views,v=>v.Id=="BaseBottom").Bounds!;
        Assert.True(bounds.MinY<=block.Insertion.Y-120);
        var bar=Assert.Single(result.Value.Primitives.OfType<PolylinePrimitive>(),p=>p.ViewId=="BaseBottom" && p.Role=="Rebar/Bottom");
        Assert.Equal(new Point2Mm(-500,0),bar.Points[0]); Assert.Equal(new Point2Mm(500,0),bar.Points[1]);
        Assert.Equal(1000,bar.LogicalLengthMm);
    }
    [Fact]
    public void FullWallLayoutAddsWallPlanAndBothSteelSectionsWithoutConcreteHatches()
    {
        var seed = S1Inputs();
        var bar = new BarOccurrence("wall-001", null, "Wall/T1", ComponentKind.Wall, Layer.Bottom, Axis.X, 10,
            new BarPath([new LineSegment3Mm(new(-400, 400, 50), new(-400, 400, 1300))], false,
                HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath),
            1250, 1250, LengthStatus.Known, "TEST", [Source], RebarDirection.Vertical);
        var row = new SteelScheduleRow("D002", "wall-line", ComponentKind.Wall, Layer.Bottom, Axis.X, 10, 1,
            1250, 1250, 0.6172839506172839, new MassKg(0.7716049382716049), [bar.Id], "TEST", RebarDirection.Vertical);
        var layout = new RebarLayout(seed.RebarLayout.Occurrences.Append(bar), [], CalculationScope.FullRebar);
        var schedule = new SteelSchedule(seed.SteelSchedule.Rows.Append(row),
            [new(ComponentKind.Base, seed.SteelSchedule.TotalMassKg), new(ComponentKind.Wall, row.MassKg)],
            new MassKg(seed.SteelSchedule.TotalMassKg.Value + row.MassKg.Value), "D2Over162", "v1");
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var geometry = new GeometryModel(seed.Geometry.Components.Append(
            Component("Wall/T1", ComponentKind.Wall, "Concrete", -500, 350, 500, 500, 0, 1400, frame)), []);
        var result = ViewBuilder.Build(new DrawingInputs(geometry, layout, schedule, null, seed.Quantities), Presentation(30), Identity);
        Assert.True(result.Success);
        Assert.All(result.Value!.Primitives.Where(p => p.ComponentId is not null),
            p => Assert.Contains(geometry.Components, c => c.Id == p.ComponentId));
        Assert.Contains(result.Value!.Views, v => v.Id == "WallMeshPlan");
        var faceView = Assert.Single(result.Value.Views, v => v.Id.StartsWith("Reinforcement/Wall/T1/", StringComparison.Ordinal));
        Assert.All(result.Value.Primitives.Where(p => p.ViewId == faceView.Id), p => Assert.Equal("Wall/T1", p.ComponentId));
        var faceBar = Assert.Single(result.Value.Primitives.OfType<PolylinePrimitive>(), p => p.ViewId == faceView.Id && p.OccurrenceId == bar.Id);
        Assert.Equal(1250, faceBar.Points[1].Y - faceBar.Points[0].Y);
        foreach (var view in new[] { "SteelAA", "SteelBB" })
        {
            Assert.Contains(result.Value.Views, v => v.Id == view);
            Assert.DoesNotContain(result.Value.Primitives.OfType<HatchPrimitive>(), h => h.ViewId == view && h.Role == "Material/Concrete");
            Assert.Contains(result.Value.Primitives.OfType<HatchPrimitive>(), h => h.ViewId == view && h.Role == "Material/Bedding");
            if (view == "SteelAA")
            {
                var path = Assert.Single(result.Value.Primitives.OfType<PolylinePrimitive>(), p => p.ViewId == view && p.OccurrenceId == bar.Id);
                Assert.Equal(50, path.Points[0].Y);
                Assert.Equal(1300, path.Points[1].Y);
            }
            else
                Assert.DoesNotContain(result.Value.Primitives, p => p.ViewId == view && p.OccurrenceId == bar.Id);
        }
        var dot = Assert.Single(result.Value.Primitives.OfType<CirclePrimitive>(), p => p.ViewId == "WallMeshPlan" && p.OccurrenceId == bar.Id);
        Assert.Equal(5, dot.RadiusMm);
        Assert.Equal(new Point2Mm(-400,580),dot.Center); // Published wall-mesh graphic is lifted 6 mm on paper.
        var meshOutline=Assert.Single(result.Value.Primitives.OfType<PolylinePrimitive>(),p=>p.SemanticId=="WallMeshPlan/Base/Outline");
        Assert.Equal(new Point2Mm(-500,-320),meshOutline.Points[0]);
        Assert.Equal(new Point2Mm(500,680),meshOutline.Points[2]);
        Assert.Equal(400,bar.Path.Segments.OfType<LineSegment3Mm>().Single().Start.Y);
        var hiddenWalls=result.Value.Primitives.OfType<PolylinePrimitive>().Where(p=>p.ViewId=="Plan" && p.ComponentId=="Wall/T1").ToArray();
        Assert.NotEmpty(hiddenWalls);
        Assert.All(hiddenWalls,p=>{
            Assert.Equal("Geometry/PlanHidden",p.Role);
            Assert.False(p.Closed);
            Assert.Equal(2,p.Points.Length);
            Assert.Equal("VHG-Dashed",SemanticRoleRegistry.Resolve(p.Role)!.Layer);
        });
    }
    [Fact]
    public void StructuralSectionsCarryConcreteAndBeddingHatchesButPlanDoesNot()
    {
        var result = ViewBuilder.Build(S1Inputs(), Presentation(30), Identity);
        Assert.True(result.Success);
        foreach (var view in new[] { "AA", "BB" })
        {
            var hatches = result.Value!.Primitives.OfType<HatchPrimitive>().Where(p => p.ViewId == view).ToArray();
            Assert.Contains(hatches, h => h.ComponentId == "Base" && h.MaterialKind == DrawingMaterialKind.Concrete);
            Assert.Contains(hatches, h => h.ComponentId == "Pad" && h.Role == "Material/Bedding");
        }
        Assert.DoesNotContain(result.Value!.Primitives.OfType<HatchPrimitive>(), h => h.ViewId == "Plan");
    }
    private static readonly SourceRef Source = new(SourceKind.SyntheticFixture, "Session-07.inline",
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("Session-07 synthetic fixture"))), null, null, null,
        "Session-07 synthetic fixture");
    private static readonly PackageIdentity Identity = new(new string('c', 64), "Session-07/Drawing-v2", CalculationScope.FullRebar);

    [Fact]
    public void ProductionBaseTicksUseActualBarIntersectionWithoutNominalDimensionBands()
    {
        var package=ViewBuilder.Build(S1Inputs(),Presentation(30),Identity);
        Assert.True(package.Success);
        var primitives=package.Value!.Primitives;
        var tick=Assert.Single(primitives.OfType<BlockPrimitive>(),p=>p.Role==SheetTwoGach.Role);
        var bar=Assert.Single(primitives.OfType<PolylinePrimitive>(),p=>p.LogicalLengthMm is not null);
        Assert.Equal(bar.OccurrenceId,tick.OccurrenceId);
        Assert.Equal(bar.ScheduleMark,tick.ScheduleMark);
        var a=bar.Points[0];var b=bar.Points[^1];var q=tick.Insertion;
        Assert.InRange(Math.Abs((b.X-a.X)*(q.Y-a.Y)-(b.Y-a.Y)*(q.X-a.X)),0,1e-7);
        Assert.InRange(q.X,Math.Min(a.X,b.X),Math.Max(a.X,b.X));
        Assert.InRange(q.Y,Math.Min(a.Y,b.Y),Math.Max(a.Y,b.Y));
        Assert.Single(primitives.OfType<PolylinePrimitive>(),p=>p.Role==SheetTwoGachCollector.CollectorRole);
        Assert.True(DrawingPackageValidator.ValidateCollectorAssociations(primitives).Success);
    }

    [Fact]
    public void LogicalBarPathLengthIsIndependentOfPresentationScale()
    {
        var inputs = S1Inputs();
        var at20 = ViewBuilder.Build(inputs, Presentation(20), Identity);
        var at30 = ViewBuilder.Build(inputs, Presentation(30), Identity);

        Assert.True(at20.Success);
        Assert.True(at30.Success);
        var bar20 = Assert.Single(at20.Value!.Primitives.OfType<PolylinePrimitive>(),p => p.OccurrenceId is not null && p.LogicalLengthMm is not null);
        var bar30 = Assert.Single(at30.Value!.Primitives.OfType<PolylinePrimitive>(),p => p.OccurrenceId is not null && p.LogicalLengthMm is not null);
        Assert.True(Math.Abs(bar20.LogicalLengthMm!.Value - 1000) <= 1e-6);
        Assert.Equal(bar20.LogicalLengthMm, bar30.LogicalLengthMm);
        Assert.Equal(bar20.Points.Length, bar30.Points.Length);
        for (var index = 0; index < bar20.Points.Length; index++) Assert.Equal(bar20.Points[index], bar30.Points[index]);
    }

    [Fact]
    public void FullFixtureAddsTypedDetailViewsAndAccessoryTags()
    {
        var result = ViewBuilder.Build(FullInputs(), Presentation(20), Identity);

        Assert.True(result.Success);
        Assert.Contains(result.Value!.Views, view => view.Purpose == ViewPurpose.NeckDetail);
        Assert.Contains(result.Value.Views, view => view.Purpose == ViewPurpose.CatchBasinDetail);
        Assert.Contains(result.Value.Views, view => view.Purpose == ViewPurpose.PanelDetail);
        Assert.Contains(result.Value.Views, view => view.Purpose == ViewPurpose.AccessoryDetail);
        Assert.Contains(result.Value.Primitives, primitive => primitive is TextPrimitive tag &&
            tag.Role == "Accessory/Tag" && tag.ComponentId is not null && tag.Content == tag.ComponentId);
    }

    [Fact]
    public void DuplicateSemanticIdentityIsBlocked()
    {
        var view = new ViewDefinition("V1", ViewPurpose.Plan, new Point2Mm(0, 0), new Bounds2Mm(0, 0, 1000, 1000), 20);
        var first = new PolylinePrimitive("same", "Geometry/Material", "V1", null, null, null,
            [new Point2Mm(0, 0), new Point2Mm(1000, 0)], false);
        var second = new PolylinePrimitive("same", "Geometry/Material", "V1", null, null, null,
            [new Point2Mm(0, 10), new Point2Mm(1000, 10)], false);
        var result = DrawingPackageValidator.ValidatePrimitives([view], [first, second]);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DRAWING_SEMANTIC_ID_DUPLICATE" &&
            diagnostic.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void UnknownRequiredRoleIsBlocked()
    {
        var view = new ViewDefinition("V1", ViewPurpose.Plan, new Point2Mm(0, 0), new Bounds2Mm(0, 0, 1000, 1000), 20);
        var primitive = new TextPrimitive("text", "Required/Unknown", "V1", null, null, null,
            new Point2Mm(0, 0), "unknown", 50, 0);
        var result = DrawingPackageValidator.ValidatePrimitives([view], [primitive]);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DRAWING_REQUIRED_ROLE_UNKNOWN");
    }

    [Fact]
    public void JsonCodecReportsUnsupportedSchemaWithoutDiscardingUnknownPrimitive()
    {
        var package = ViewBuilder.Build(S1Inputs(), Presentation(20), Identity).Value!;
        var json = DrawingPackageJson.Serialize(package).Replace("\"$type\":\"polyline\"", "\"$type\":\"futurePrimitive\"");

        var result = DrawingPackageJson.DeserializeResult(json);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "DRAWING_SCHEMA_UNSUPPORTED" &&
            diagnostic.Actual!.Contains("futurePrimitive", StringComparison.Ordinal));
    }

    [Fact]
    public void JsonRoundTripRetainsArcSweepClosedLoopsMarksAndMaterialKind()
    {
        var view = new ViewDefinition("V1", ViewPurpose.AccessoryDetail, new Point2Mm(0, 0), new Bounds2Mm(-20, -20, 20, 20), 20);
        var path = new PathPrimitive("arc-1", "Rebar/Schematic", "V1", "Collar", null, "A001",
            [new ArcPathSegment(new Point2Mm(0, 0), 10, 0, Math.PI / 2)], true, ViewPurpose.Schematic,
            15.7, DrawingMaterialKind.AngleSteel, "angle-1");
        var hatch = new HatchPrimitive("hatch-1", "Geometry/Hatch", "V1", "Collar",
            [new Point2Mm(-10, -10), new Point2Mm(10, -10), new Point2Mm(10, 10)], [], DrawingMaterialKind.PurchasedCover);
        var block = new BlockPrimitive("block-1", "Accessory/Tag", "V1", "Collar", "Cover/Tag",
            new Point2Mm(0, 0), 1, [new BlockAttributeIntent("MaterialKind", "PurchasedCover")]);
        var package = new DrawingPackage(new string('d', 64), "Session-07/Drawing-v2", [view], [path, hatch, block],
            new Bounds2Mm(-20, -20, 20, 20), CalculationScope.FullRebar);

        var restored = DrawingPackageJson.DeserializeResult(DrawingPackageJson.Serialize(package)).Value!;
        var restoredPath = Assert.IsType<PathPrimitive>(restored.Primitives.Single(p => p.SemanticId == "arc-1"));
        var restoredArc = Assert.IsType<ArcPathSegment>(Assert.Single(restoredPath.Segments));
        Assert.Equal(Math.PI / 2, restoredArc.SweepRadians, 12);
        Assert.True(restoredPath.Closed);
        Assert.Equal("A001", restoredPath.ScheduleMark);
        Assert.Equal(DrawingMaterialKind.AngleSteel, restoredPath.MaterialKind);
        Assert.Equal(DrawingMaterialKind.PurchasedCover, Assert.IsType<HatchPrimitive>(restored.Primitives.Single(p => p.SemanticId == "hatch-1")).MaterialKind);
    }

    [Fact]
    public void HundredBarsShareOneSchematicOutsideScheduleWithLegDimensionAndQuantity()
    {
        var seed=S1Inputs();
        var first=seed.RebarLayout.Occurrences[0];
        var bars=Enumerable.Range(0,100).Select(i=>new BarOccurrence($"copy-{i}",null,first.ComponentId,
            first.ComponentKind,first.Layer,first.Axis,first.DiameterMm,first.Path,first.ScheduleLengthMm,
            first.ScheduleLengthMm,LengthStatus.Known,"TEST",[Source])).ToArray();
        var old=seed.SteelSchedule.Rows[0];
        var row=new SteelScheduleRow(old.Mark,"same",old.ComponentKind,old.Layer,old.Axis,old.DiameterMm,100,
            old.SingleLengthMm,100*old.SingleLengthMm,old.UnitMassKgPerM,new MassKg(100*old.MassKg.Value),
            bars.Select(b=>b.Id),"TEST");
        var schedule=new SteelSchedule([row],[new(old.ComponentKind,row.MassKg)],row.MassKg,"D2Over162","v1");
        var inputs=new DrawingInputs(seed.Geometry,new RebarLayout(bars,[],CalculationScope.FullRebar),schedule,null,seed.Quantities);
        var result=ViewBuilder.Build(inputs,Presentation(30),Identity);
        Assert.True(result.Success);
        var shape=Assert.Single(result.Value!.Primitives.OfType<PathPrimitive>(),p=>p.Purpose==ViewPurpose.Schematic);
        Assert.StartsWith("Bending/",shape.ViewId);
        Assert.Contains(result.Value.Primitives.OfType<TextPrimitive>(),p=>p.ViewId==shape.ViewId && p.Content.Contains("SL=100"));
        Assert.Single(result.Value.Primitives.OfType<DimensionIntentPrimitive>(),p=>p.ViewId==shape.ViewId);
    }
    [Fact]
    public void SchematicBarDiagramCarriesScheduleLengthAndPurpose()
    {
        var inputs = S1Inputs();
        var result = ViewBuilder.Build(inputs, Presentation(20), Identity);

        Assert.True(result.Success);
        var schematic = Assert.Single(result.Value!.Primitives.OfType<PathPrimitive>().Where(p => p.Purpose == ViewPurpose.Schematic));
        Assert.Equal(1000, schematic.ScheduleLengthMm, 6);
        Assert.Equal("D001", schematic.ScheduleMark);
    }

    [Fact]
    public void ScaleAndThemeDoNotChangeDrawingIdentityOrLogicalQuantities()
    {
        var first = ViewBuilder.Build(S1Inputs(), Presentation(20, "light"), Identity).Value!;
        var second = ViewBuilder.Build(S1Inputs(), Presentation(30, "dark"), Identity).Value!;

        Assert.Equal(first.InputHash, second.InputHash);
        Assert.Equal(first.Primitives.OfType<PolylinePrimitive>().Select(p => (p.SemanticId, p.LogicalLengthMm)),
            second.Primitives.OfType<PolylinePrimitive>().Select(p => (p.SemanticId, p.LogicalLengthMm)));
        Assert.Equal(first.Primitives.OfType<TableRowPrimitive>().Select(p => p.Cells.Select(c => c.Number).ToArray()),
            second.Primitives.OfType<TableRowPrimitive>().Select(p => p.Cells.Select(c => c.Number).ToArray()));
    }

    private static PresentationRequest Presentation(double scale, string theme = "default") =>
        new(scale, ElevationMode.Absolute, new ElevationM(10), theme, true);

    [Fact]
    public void PurchasedCoverUsesNativeBlockRotationAnchorAndScale()
    {
        var seed=S1Inputs();
        var cover=new PurchasedCoverPlacement("C",new(100,200,1600),new(200,100),1,90,new(-40,-10,160,90),[Source]);
        var geometry=new GeometryModel(seed.Geometry.Components,seed.Geometry.Openings,[cover]);
        var inputs=new DrawingInputs(geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities);
        var result=ViewBuilder.Build(inputs,Presentation(30),Identity);
        Assert.True(result.Success);
        var block=Assert.Single(result.Value!.Primitives.OfType<BlockPrimitive>(),p=>p.Role=="Geometry/PurchasedCover" && p.ViewId=="Plan");
        Assert.Equal("Block:C.MB",block.BlockRole);
        Assert.Equal(new Point2Mm(100,200),block.Insertion);
        Assert.Equal(Math.PI/2,block.RotationRadians);
        Assert.Equal(1,block.AnnotationScale);
        var restored=DrawingPackageJson.Deserialize(DrawingPackageJson.Serialize(result.Value));
        Assert.Equal(block.RotationRadians,Assert.Single(restored.Primitives.OfType<BlockPrimitive>(),p=>p.Role=="Geometry/PurchasedCover" && p.ViewId=="Plan").RotationRadians);
    }

    [Fact]
    public void SuppliedAngleScheduleBecomesDrawingRowsWithoutRecalculation()
    {
        var seed=S1Inputs();
        var angle=new AngleOccurrence("angle","Base","V30",seed.RebarLayout.Occurrences[0].Path,1000,1.36,[Source]);
        var schedule=AngleScheduleCalculator.Calculate([angle]).Value!;
        var layout=new RebarLayout(seed.RebarLayout.Occurrences,[],CalculationScope.FullRebar,angles:[angle]);
        var result=ViewBuilder.Build(new DrawingInputs(seed.Geometry,layout,seed.SteelSchedule,schedule,seed.Quantities),Presentation(30),Identity);
        Assert.True(result.Success);
        var row=Assert.Single(result.Value!.Primitives.OfType<TableRowPrimitive>().Where(p=>p.Role=="Schedule/AngleRow"));
        Assert.Equal(1000,row.Cells.Single(c=>c.ColumnKey=="TotalLengthMm").Number);
        Assert.Equal(1.36,row.Cells.Single(c=>c.ColumnKey=="MassKg").Number);
        Assert.Equal(DrawingMaterialKind.AngleSteel,row.MaterialKind);
        Assert.Contains(result.Value.Primitives,p=>p is PathPrimitive path && path.MaterialKind==DrawingMaterialKind.AngleSteel && path.SourceOccurrenceId==angle.Id);
    }

    [Theory]
    [InlineData(1,0)]
    [InlineData(2,90)]
    public void SheetOne700CoverUsesDirectNativeBlocksWithProjectionSpecificScale(double scale,double rotation)
    {
        var seed=S1Inputs();
        var cover=new PurchasedCoverPlacement("NapGaV.700",new(100,200,1600),new(800,800),scale,rotation,new(-400,-400,400,400),[Source]);
        var geometry=new GeometryModel(seed.Geometry.Components,seed.Geometry.Openings,[cover]);
        var inputs=new DrawingInputs(geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities);
        var conventional=ViewBuilder.Build(inputs,Presentation(30),Identity);
        var native=ViewBuilder.Build(inputs,Presentation(30,SheetOnePresentationPolicy.Family21Theme),Identity);
        Assert.True(conventional.Success);
        Assert.True(native.Success);
        Assert.Same(cover,Assert.Single(inputs.Geometry.PurchasedCovers));
        Assert.Equal(conventional.Value!.Views.Single(v=>v.Id=="Plan").Bounds,native.Value!.Views.Single(v=>v.Id=="Plan").Bounds);
        var original=conventional.Value.Primitives.OfType<BlockPrimitive>().Where(p=>p.SemanticId.StartsWith("PurchasedCover/",StringComparison.Ordinal)).ToArray();
        var actual=native.Value.Primitives.OfType<BlockPrimitive>().Where(p=>p.SemanticId.StartsWith("PurchasedCover/",StringComparison.Ordinal)).ToArray();
        Assert.Equal(3,actual.Length);
        foreach(var block in actual)
        {
            var old=Assert.Single(original,p=>p.SemanticId==block.SemanticId);
            Assert.Equal(old.Insertion,block.Insertion);
            Assert.Equal(old.RotationRadians,block.RotationRadians);
            Assert.Equal(old.Role,block.Role);
            bool plan=block.ViewId=="Plan";
            Assert.Equal(plan?"Block:NapGaV-MB":"Block:NapGaV-MC",block.BlockRole);
            Assert.Equal(scale*(plan?7d/8:8d/9),block.AnnotationScale);
        }
        Assert.All(original,p=>Assert.StartsWith("Block:NapGaV-M",p.BlockRole)); // 2026-10-05 Muse: conventional also uses native blocks; NapGaV.700.* roles have no template asset
        Assert.All(original,p=>Assert.Equal(scale*(p.ViewId=="Plan"?7d/8:8d/9),p.AnnotationScale));
    }

    [Fact]
    public void OtherLegacyCoverCatalogRetainsItsExistingBlockContractOnSheetOne()
    {
        var seed=S1Inputs();
        var cover=new PurchasedCoverPlacement("NapGaV.800",new(100,200,1600),new(800,800),1,0,new(-400,-400,400,400),[Source]);
        var geometry=new GeometryModel(seed.Geometry.Components,seed.Geometry.Openings,[cover]);
        var result=ViewBuilder.Build(new DrawingInputs(geometry,seed.RebarLayout,seed.SteelSchedule,null,seed.Quantities),Presentation(30,SheetOnePresentationPolicy.Family21Theme),Identity);
        Assert.True(result.Success);
        var blocks=result.Value!.Primitives.OfType<BlockPrimitive>().Where(p=>p.SemanticId.StartsWith("PurchasedCover/",StringComparison.Ordinal)).ToArray();
        Assert.Equal(3,blocks.Length);
        Assert.All(blocks,p=>Assert.StartsWith("Block:NapGaV.800.",p.BlockRole));
        Assert.All(blocks,p=>Assert.Equal(1,p.AnnotationScale));
    }

    [Fact]
    public void Family21FirstSheetOmitsSeparatePlanAngleOutlineButRetainsPhysicalAngleAndSchedule()
    {
        var seed=S1Inputs();
        var angle=new AngleOccurrence("angle","Base","V30",seed.RebarLayout.Occurrences[0].Path,1000,1.36,[Source]);
        var schedule=AngleScheduleCalculator.Calculate([angle]).Value!;
        var layout=new RebarLayout(seed.RebarLayout.Occurrences,[],CalculationScope.FullRebar,angles:[angle]);
        var inputs=new DrawingInputs(seed.Geometry,layout,seed.SteelSchedule,schedule,seed.Quantities);
        var conventional=ViewBuilder.Build(inputs,Presentation(30),Identity);
        var sheetOne=ViewBuilder.Build(inputs,Presentation(30,SheetOnePresentationPolicy.Family21Theme),Identity);
        Assert.True(conventional.Success);
        Assert.True(sheetOne.Success);
        Assert.Single(conventional.Value!.Primitives,p=>p.ViewId=="Plan" && p.Role=="Accessory/AngleFrame");
        Assert.DoesNotContain(sheetOne.Value!.Primitives,p=>p.ViewId=="Plan" && p.Role=="Accessory/AngleFrame");
        Assert.Same(angle,Assert.Single(inputs.RebarLayout.Angles));
        Assert.Same(schedule,inputs.AngleSchedule);
        var before=Assert.Single(conventional.Value.Primitives.OfType<TableRowPrimitive>(),p=>p.Role=="Schedule/AngleRow");
        var after=Assert.Single(sheetOne.Value.Primitives.OfType<TableRowPrimitive>(),p=>p.Role=="Schedule/AngleRow");
        Assert.Equal(before.Cells.ToArray(),after.Cells.ToArray());
        Assert.Equal(1000,after.Cells.Single(c=>c.ColumnKey=="TotalLengthMm").Number);
        Assert.Equal(1.36,after.Cells.Single(c=>c.ColumnKey=="MassKg").Number);
    }

    private static DrawingInputs S1Inputs()
    {
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var components = new[]
        {
            Component("Pad", ComponentKind.Pad, "Bedding", -600, -600, 600, 600, -250, -150, frame),
            Component("Base", ComponentKind.Base, "Concrete", -500, -500, 500, 500, -150, 0, frame),
            Component("Roof", ComponentKind.Roof, "Concrete", -500, -500, 500, 500, 1400, 1550, frame)
        };
        var geometry = new GeometryModel(components, []);
        var path = new BarPath([new LineSegment3Mm(new(-500, 0, -100), new(500, 0, -100))], false,
            HookPattern.Hook0M, 0, false, MeasurementBasis.TangentPath);
        var occurrence = new BarOccurrence("bar-001", null, "Base", ComponentKind.Base, Layer.Bottom, Axis.X, 10,
            path, 1000, 1000, LengthStatus.Known, "TEST", [Source], RebarDirection.Horizontal);
        var layout = new RebarLayout([occurrence], [], CalculationScope.FullRebar);
        var row = new SteelScheduleRow("D001", "line", ComponentKind.Base, Layer.Bottom, Axis.X, 10, 1,
            1000, 1000, 0.6172839506172839, new MassKg(0.6172839506172839), [occurrence.Id], "TEST",
            RebarDirection.Horizontal);
        var schedule = new SteelSchedule([row], [new KeyValuePair<ComponentKind, MassKg>(ComponentKind.Base, row.MassKg)],
            row.MassKg, "D2Over162", "v1");
        return new DrawingInputs(geometry, layout, schedule, null, DrawingQuantitySnapshot.NotCalculated);
    }

    private static DrawingInputs FullInputs()
    {
        var baseInputs = S1Inputs();
        var frame = new Frame3Mm(new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
        var components = baseInputs.Geometry.Components.ToList();
        components.Add(Component("Neck", ComponentKind.Neck, "Concrete", -250, -250, 250, 250, 1400, 1600, frame));
        components.Add(Component("CatchBasin", ComponentKind.CatchBasin, "Concrete", -300, -200, 300, 200, 0, 400, frame));
        components.Add(Component("Panel", ComponentKind.Panel, "Concrete", -150, -150, 150, 150, 1600, 1700, frame));
        components.Add(Component("Collar", ComponentKind.Collar, "Steel", -100, -100, 100, 100, 1700, 1750, frame));
        return new DrawingInputs(new GeometryModel(components, []), baseInputs.RebarLayout, baseInputs.SteelSchedule,
            new AngleSchedule([], new MassKg(0)), DrawingQuantitySnapshot.NotCalculated);
    }

    private static ComponentGeometry Component(string id, ComponentKind kind, string material, double x0, double y0,
        double x1, double y1, double z0, double z1, Frame3Mm frame)
    {
        var footprint = new Footprint(new Polygon2Mm([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1)]), []);
        var z = new Interval1D(z0, z1);
        return new ComponentGeometry(id, kind, material, footprint, z, new ExtrusionSolid(footprint, frame, z), [], [], [Source]);
    }
}



