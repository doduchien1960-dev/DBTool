using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.Drawing;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class FullSectionProjectionTests
{
    [Fact]
    public void ObliqueRoundWallSectionHasExactEllipticVoidAndClosedMaterialLoops()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var inner=new CircleProfile(new Point2Mm(0,700),80);
        var outer=new CircleProfile(new Point2Mm(0,700),90);
        var cut=new CircleProfile(new Point2Mm(0,700),100);
        var opening=new OpeningProfile("Pipe","Wall/T1",OpeningKind.Round,
            inner,outer,cut,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(600,800),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(2);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SectionPlane("Diagonal",new Point3Mm(0,700,0),new Vector3(q,q,0)),
            CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        Assert.Empty(section.Value!.Regions);
        var voids=section.Value.AnalyticVoids;
        Assert.Equal(3,voids.Length);
        Assert.Equal(new[]{ProjectedVoidRole.Inner,ProjectedVoidRole.PipeOuter,
            ProjectedVoidRole.ConcreteCutout},voids.Select(v=>v.Role).ToArray());
        var concrete=voids.Single(v=>v.Role==ProjectedVoidRole.ConcreteCutout);
        double expectedCut=2*Math.Sqrt(2)*(75*Math.Sqrt(10000-75*75)+
            10000*Math.Asin(0.75));
        Assert.InRange(Math.Abs(concrete.AreaMm2-expectedCut),0,1e-6);
        Assert.True(concrete.Boundary.Closed);
        Assert.Equal("Pipe",concrete.SourceOpeningId);
        Assert.Equal(2,section.Value.AnalyticRegions.Length);
        Assert.All(section.Value.AnalyticRegions,r=>Assert.True(r.Boundary.Closed));
        double expectedMaterial=150*Math.Sqrt(2)*1400-expectedCut;
        Assert.InRange(Math.Abs(section.Value.AnalyticRegions.Sum(r=>r.AreaMm2)-
            expectedMaterial),0,1e-6);
        var boundaries=SectionProfilePrimitiveBuilder.Build(section.Value);
        Assert.Equal(5,boundaries.OfType<SectionBoundaryPrimitive>().Count());
        var cutPrimitive=boundaries.OfType<SectionBoundaryPrimitive>()
            .Single(p=>p.SourceOpeningId=="Pipe" && p.Role=="ConcreteCutout");
        Assert.Equal(BoundaryWinding.Clockwise,cutPrimitive.Boundary.Winding);
        Assert.Equal(2,cutPrimitive.Boundary.Edges.OfType<SectionEllipseEdge>().Count());
        Assert.All(boundaries.OfType<SectionBoundaryPrimitive>()
            .Where(p=>p.SourceOpeningId is null),p=>
                Assert.Equal(BoundaryWinding.CounterClockwise,p.Boundary.Winding));
        var package=new DrawingPackage(new string('a',64),"S2Geometry-1/v1",
            [new ViewDefinition(section.Value.Id,ViewPurpose.Section,new Point2Mm(0,0),
                new Bounds2Mm(-1000,0,1000,1400),1)],boundaries,
            new Bounds2Mm(-1000,0,1000,1400),CalculationScope.BaseRebarOnly);
        var restored=DrawingPackageJson.Deserialize(DrawingPackageJson.Serialize(package));
        var restoredCut=restored.Primitives.OfType<SectionBoundaryPrimitive>()
            .Single(p=>p.SemanticId==cutPrimitive.SemanticId);
        Assert.Equal("Pipe",restoredCut.SourceOpeningId);
        Assert.Equal(BoundaryWinding.Clockwise,restoredCut.Boundary.Winding);
        Assert.Equal(2,restoredCut.Boundary.Edges.OfType<SectionEllipseEdge>().Count());
    }

    [Fact]
    public void ExplicitCutterRoundWallSectionKeepsPhysicalStationAndEllipseArea()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var inner=new CircleProfile(new Point2Mm(0,700),80);
        var outer=new CircleProfile(new Point2Mm(0,700),90);
        var cut=new CircleProfile(new Point2Mm(0,700),100);
        var opening=new OpeningProfile("Pipe","Wall/T1",OpeningKind.Round,
            inner,outer,cut,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(600,800),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var start=frame.ToGlobal(-75,0,0);
        var end=frame.ToGlobal(75,150,0);
        var cutter=new SteppedSectionDefinition("WallDiagonal",[
            new Point2Mm(start.X,start.Y),new Point2Mm(end.X,end.Y)]);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            cutter,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        Assert.Empty(section.Value!.Regions);
        var band=section.Value.AnalyticVoids.Single(v=>v.Role==ProjectedVoidRole.ConcreteCutout);
        Assert.InRange(Math.Abs(band.Band.StationStartMm),0,1e-6);
        Assert.InRange(Math.Abs(band.Band.StationEndMm-150*Math.Sqrt(2)),0,1e-6);
        double expectedCut=2*Math.Sqrt(2)*(75*Math.Sqrt(10000-75*75)+
            10000*Math.Asin(0.75));
        Assert.InRange(Math.Abs(band.AreaMm2-expectedCut),0,1e-6);
        Assert.Equal(2,section.Value.AnalyticRegions.Length);
        Assert.InRange(Math.Abs(section.Value.AnalyticRegions.Sum(r=>r.AreaMm2)-
            (150*Math.Sqrt(2)*1400-expectedCut)),0,1e-6);
    }

    [Fact]
    public void ObliqueRoundCutAcrossPartOfWallKeepsIntactSideStrip()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new CircleProfile(new Point2Mm(0,700),100);
        var opening=new OpeningProfile("Pipe","Wall/T1",OpeningKind.Round,
            cut,cut,cut,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(600,800),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(2);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SectionPlane("Partial",new Point3Mm(80,700,0),new Vector3(q,q,0)),
            CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        var strip=Assert.Single(section.Value!.Regions);
        var p=strip.Footprint.Outer.Vertices;
        double stripArea=(p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        Assert.InRange(Math.Abs(stripArea-55*Math.Sqrt(2)*1400),0,1e-6);
        var band=section.Value.AnalyticVoids.Single(v=>v.Role==ProjectedVoidRole.ConcreteCutout);
        static double Primitive(double u)=>u*Math.Sqrt(10000-u*u)+10000*Math.Asin(u/100);
        double cutArea=Math.Sqrt(2)*(Primitive(-5)-Primitive(-100));
        Assert.InRange(Math.Abs(band.AreaMm2-cutArea),0,1e-6);
        Assert.Equal(2,section.Value.AnalyticRegions.Length);
        Assert.InRange(Math.Abs(stripArea+section.Value.AnalyticRegions.Sum(r=>r.AreaMm2)-
            (150*Math.Sqrt(2)*1400-cutArea)),0,1e-6);
    }

    [Fact]
    public void ExplicitRoundCutAcrossPartOfWallKeepsIntactSideStrip()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new CircleProfile(new Point2Mm(0,700),100);
        var opening=new OpeningProfile("Pipe","Wall/T1",OpeningKind.Round,
            cut,cut,cut,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(600,800),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var a=frame.ToGlobal(-155,0,0);
        var b=frame.ToGlobal(-5,150,0);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SteppedSectionDefinition("PartialStep",[
                new Point2Mm(a.X,a.Y),new Point2Mm(b.X,b.Y)]),
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        var strip=Assert.Single(section.Value!.Regions);
        var p=strip.Footprint.Outer.Vertices;
        double stripArea=(p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        Assert.InRange(Math.Abs(stripArea-55*Math.Sqrt(2)*1400),0,1e-6);
        Assert.Equal(2,section.Value.AnalyticRegions.Length);
        Assert.True(section.Value.AnalyticVoids.Single(v=>
            v.Role==ProjectedVoidRole.ConcreteCutout).Boundary.Closed);
    }

    [Theory]
    [InlineData(1350)]
    [InlineData(50)]
    public void RoundCutCrossingWallEdgeHasExactClippedAreaAndClosedLoops(double centerHeight)
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new CircleProfile(new Point2Mm(0,centerHeight),100);
        var opening=new OpeningProfile("TopPipe","Wall/T1",OpeningKind.Round,
            cut,cut,cut,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(centerHeight-100,centerHeight+100),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(5);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SectionPlane("TopClip",new Point3Mm(0,700,0),new Vector3(q,2*q,0)),
            CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double a=Math.Sqrt(7500);
        double cutArea=Math.Sqrt(5)/2*(Math.PI*10000-(10000*Math.Asin(a/100)-50*a));
        Assert.InRange(Math.Abs(section.Value!.AnalyticVoids
            .Where(v=>v.Role==ProjectedVoidRole.ConcreteCutout).Sum(v=>v.AreaMm2)-cutArea),0,1e-6);
        double material=section.Value.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        })+section.Value.AnalyticRegions.Sum(r=>r.AreaMm2);
        Assert.InRange(Math.Abs(material-(150*Math.Sqrt(5)*1400-cutArea)),0,1e-6);
        Assert.All(section.Value.AnalyticVoids,v=>Assert.True(v.Boundary.Closed));
        Assert.All(section.Value.AnalyticRegions,r=>Assert.True(r.Boundary.Closed));
    }

    [Fact]
    public void ExplicitRoundCutCrossingWallTopKeepsPhysicalStationAndClippedVoid()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new CircleProfile(new Point2Mm(0,1350),100);
        var opening=new OpeningProfile("TopPipe","Wall/T1",OpeningKind.Round,
            cut,cut,cut,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(1250,1450),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var a=frame.ToGlobal(-150,0,0);
        var b=frame.ToGlobal(150,150,0);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SteppedSectionDefinition("TopStep",[
                new Point2Mm(a.X,a.Y),new Point2Mm(b.X,b.Y)]),
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double chord=Math.Sqrt(7500);
        double cutArea=Math.Sqrt(5)/2*(Math.PI*10000-
            (10000*Math.Asin(chord/100)-50*chord));
        Assert.InRange(Math.Abs(section.Value!.AnalyticVoids
            .Where(v=>v.Role==ProjectedVoidRole.ConcreteCutout).Sum(v=>v.AreaMm2)-cutArea),0,1e-6);
        var cutSegments=section.Value.AnalyticVoids
            .Where(v=>v.Role==ProjectedVoidRole.ConcreteCutout).ToArray();
        Assert.Equal(3,cutSegments.Length);
        Assert.InRange(Math.Abs(cutSegments.Min(v=>v.Band.StationStartMm)-50*Math.Sqrt(5)/2),0,1e-6);
        Assert.InRange(Math.Abs(cutSegments.Max(v=>v.Band.StationEndMm)-250*Math.Sqrt(5)/2),0,1e-6);
    }

    [Fact]
    public void ObliqueSectionThatMissesRoundOpeningKeepsWholeWallMaterial()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var circle=new CircleProfile(new Point2Mm(0,700),100);
        var opening=new OpeningProfile("Pipe","Wall/T1",OpeningKind.Round,
            circle,circle,circle,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(600,800),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(2);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SectionPlane("Miss",new Point3Mm(500,700,0),new Vector3(q,q,0)),
            CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        var region=Assert.Single(section.Value!.Regions);
        var p=region.Footprint.Outer.Vertices;
        double area=(p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        Assert.InRange(Math.Abs(area-150*Math.Sqrt(2)*1400),0,1e-6);
        Assert.Empty(section.Value.AnalyticVoids);
    }

    [Fact]
    public void ExplicitCutterThatMissesRoundOpeningKeepsWholeWallMaterial()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var circle=new CircleProfile(new Point2Mm(0,700),100);
        var opening=new OpeningProfile("Pipe","Wall/T1",OpeningKind.Round,
            circle,circle,circle,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(600,800),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var start=frame.ToGlobal(400,0,0);
        var end=frame.ToGlobal(550,150,0);
        var cutter=new SteppedSectionDefinition("Miss",[
            new Point2Mm(start.X,start.Y),new Point2Mm(end.X,end.Y)]);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            cutter,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        Assert.Single(section.Value!.Regions);
        Assert.Empty(section.Value.AnalyticVoids);
    }



    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObliqueSectionSubtractsPhysicalRectangularWallCut(bool openTop)
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var box=new RectangleProfile(new Rectangle2Mm(-50,500,50,900));
        var cut=openTop ? (Profile2Mm)new OpenTopProfile(new Polygon2Mm([
            new(-50,500),new(50,500),new(50,900),new(-50,900)])) : box;
        var opening=new OpeningProfile("Wall/Box","Wall/T1",
            openTop ? OpeningKind.OpenTop : OpeningKind.Box,
            cut,openTop ? null : cut,cut,frame,
            new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(500,900),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(2);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SectionPlane("Diagonal",new Point3Mm(0,700,0),new Vector3(q,q,0)),
            CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double area=section.Value!.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.InRange(Math.Abs(area-170_000*Math.Sqrt(2)),0,1e-6);
        var voidProfile=Assert.Single(section.Value.Voids);
        Assert.Equal("Wall/Box",voidProfile.SourceOpeningId);
        Assert.Equal(ProjectedVoidRole.ConcreteCutout,voidProfile.Role);
        Assert.Equal(500,voidProfile.Boundary.Vertices.Min(p=>p.Y));
        Assert.Equal(900,voidProfile.Boundary.Vertices.Max(p=>p.Y));
    }

    [Fact]
    public void ExplicitCutterSubtractsPhysicalRectangularWallCut()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var box=new RectangleProfile(new Rectangle2Mm(-50,500,50,900));
        var opening=new OpeningProfile("Wall/Box","Wall/T1",OpeningKind.Box,
            box,box,box,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(500,900),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var cutter=new SteppedSectionDefinition("WallStep",
            [new Point2Mm(-100,700),new Point2Mm(100,700)]);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),cutter,
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double area=section.Value!.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.Equal(240_000,area,6);
        var voidProfile=Assert.Single(section.Value.Voids);
        Assert.Equal("Wall/Box",voidProfile.SourceOpeningId);
        Assert.Equal(ProjectedVoidRole.ConcreteCutout,voidProfile.Role);
        Assert.Equal(50,voidProfile.Boundary.Vertices.Min(p=>p.X));
        Assert.Equal(150,voidProfile.Boundary.Vertices.Max(p=>p.X));
    }

    [Theory]
    [InlineData(WallId.T1)]
    [InlineData(WallId.T2)]
    [InlineData(WallId.T3)]
    [InlineData(WallId.T4)]
    public void ObliqueWallCutUsesSamePhysicalFrameOnAllFourWalls(WallId wallId)
    {
        var frame=WallFrame.Create(wallId,1000,1250);
        Point2Mm XY(double u,double v)
        {
            var p=frame.ToGlobal(u,v,0);
            return new Point2Mm(p.X,p.Y);
        }
        var corners=new[]{XY(-400,0),XY(400,0),XY(400,150),XY(-400,150)};
        double x0=corners.Min(p=>p.X),x1=corners.Max(p=>p.X);
        double y0=corners.Min(p=>p.Y),y1=corners.Max(p=>p.Y);
        var footprint=new Footprint(new Polygon2Mm([
            new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var box=new RectangleProfile(new Rectangle2Mm(-50,500,50,900));
        string owner=$"Wall/{wallId}";
        var opening=new OpeningProfile($"{owner}/Box",owner,OpeningKind.Box,
            box,box,box,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(500,900),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry(owner,ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(2);
        var direction=new Vector3((frame.Left.X+frame.Outward.X)*q,
            (frame.Left.Y+frame.Outward.Y)*q,0);
        var normal=new Vector3(direction.Y,-direction.X,0);
        var center=frame.ToGlobal(0,75,0);
        var section=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SectionPlane($"Diagonal/{wallId}",center,normal),CutMode.TrueSection,
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double area=section.Value!.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.InRange(Math.Abs(area-170_000*Math.Sqrt(2)),0,1e-6);
        Assert.Equal(ProjectedVoidRole.ConcreteCutout,Assert.Single(section.Value.Voids).Role);
    }



    [Fact]
    public void ObliqueVerticalPlaneUsesTrueDiagonalOfRectangularPrism()
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(100,0),
            new(100,100),new(0,100)]),[]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var component=new ComponentGeometry("Block",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[],[],[]);
        double q=1/Math.Sqrt(2);
        var plane=new SectionPlane("Diagonal",new Point3Mm(50,50,0),new Vector3(q,q,0));
        var result=SectionProjector.BuildSection(new GeometryModel([component],[]),
            plane,CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(result.Success);
        var region=Assert.Single(result.Value!.Regions);
        Assert.Equal("Block",region.SourceComponentId);
        Assert.InRange(Math.Abs(region.Footprint.Outer.Vertices.Min(p=>p.X)+50*Math.Sqrt(2)),0,1e-6);
        Assert.InRange(Math.Abs(region.Footprint.Outer.Vertices.Max(p=>p.X)-50*Math.Sqrt(2)),0,1e-6);
        Assert.Equal(0,region.Footprint.Outer.Vertices.Min(p=>p.Y));
        Assert.Equal(100,region.Footprint.Outer.Vertices.Max(p=>p.Y));
    }

    [Fact]
    public void ObliqueSectionSplitsRectangularRingAndOmitsCornerTangency()
    {
        var outer=new Polygon2Mm([new(0,0),new(100,0),new(100,100),new(0,100)]);
        var hole=new Polygon2Mm([new(30,30),new(30,70),new(70,70),new(70,30)]);
        var footprint=new Footprint(outer,[hole]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var ring=new ComponentGeometry("Ring",ComponentKind.Neck,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[],[],[]);
        double q=1/Math.Sqrt(2);
        var policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
        var section=SectionProjector.BuildSection(new GeometryModel([ring],[]),
            new SectionPlane("RingDiagonal",new Point3Mm(50,50,0),new Vector3(q,q,0)),
            CutMode.TrueSection,policy);
        Assert.True(section.Success);
        var spans=section.Value!.Regions.Select(r=>(
            Min:r.Footprint.Outer.Vertices.Min(p=>p.X),
            Max:r.Footprint.Outer.Vertices.Max(p=>p.X))).OrderBy(s=>s.Min).ToArray();
        Assert.Equal(2,spans.Length);
        Assert.InRange(Math.Abs(spans[0].Max-spans[0].Min-30*Math.Sqrt(2)),0,1e-6);
        Assert.InRange(Math.Abs(spans[1].Max-spans[1].Min-30*Math.Sqrt(2)),0,1e-6);
        var tangent=SectionProjector.BuildSection(new GeometryModel([ring],[]),
            new SectionPlane("Tangent",new Point3Mm(0,0,0),new Vector3(q,q,0)),
            CutMode.TrueSection,policy);
        Assert.True(tangent.Success);
        Assert.Empty(tangent.Value!.Regions);
    }

    [Fact]
    public void ObliqueSectionSubtractsFullDepthRectangularRoofOpening()
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(100,0),
            new(100,100),new(0,100)]),[]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new RectangleProfile(new Rectangle2Mm(30,30,70,70));
        var opening=new OpeningProfile("Roof/Hole","Roof",OpeningKind.Box,
            cut,null,cut,null,world,z,z,[]);
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(2);
        var section=SectionProjector.BuildSection(new GeometryModel([roof],[opening]),
            new SectionPlane("RoofDiagonal",new Point3Mm(50,50,0),new Vector3(q,q,0)),
            CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        var spans=section.Value!.Regions.Select(r=>(
            Min:r.Footprint.Outer.Vertices.Min(p=>p.X),
            Max:r.Footprint.Outer.Vertices.Max(p=>p.X))).OrderBy(s=>s.Min).ToArray();
        Assert.Equal(2,spans.Length);
        Assert.InRange(Math.Abs((spans[0].Max-spans[0].Min)-30*Math.Sqrt(2)),0,1e-6);
        Assert.InRange(Math.Abs((spans[1].Max-spans[1].Min)-30*Math.Sqrt(2)),0,1e-6);
        var voidProfile=Assert.Single(section.Value.Voids);
        Assert.Equal("Roof/Hole",voidProfile.SourceOpeningId);
        Assert.InRange(Math.Abs((voidProfile.Boundary.Vertices.Max(p=>p.X)-
            voidProfile.Boundary.Vertices.Min(p=>p.X))-40*Math.Sqrt(2)),0,1e-6);
    }

    [Fact]
    public void ObliqueSectionKeepsMaterialAboveAndBelowPartialDepthRectangularCut()
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(100,0),
            new(100,100),new(0,100)]),[]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new RectangleProfile(new Rectangle2Mm(30,30,70,70));
        var opening=new OpeningProfile("Roof/Recess","Roof",OpeningKind.Box,
            cut,null,cut,null,world,new Interval1D(25,75),new Interval1D(25,75),[]);
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        double q=1/Math.Sqrt(2);
        var section=SectionProjector.BuildSection(new GeometryModel([roof],[opening]),
            new SectionPlane("RoofDiagonal",new Point3Mm(50,50,0),new Vector3(q,q,0)),
            CutMode.TrueSection,CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double area=section.Value!.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.InRange(Math.Abs(area-8000*Math.Sqrt(2)),0,1e-6);
        var voidProfile=Assert.Single(section.Value.Voids);
        Assert.Equal(25,voidProfile.Boundary.Vertices.Min(p=>p.Y));
        Assert.Equal(75,voidProfile.Boundary.Vertices.Max(p=>p.Y));
    }

    [Fact]
    public void ExplicitSteppedCutterUsesPhysicalPathStations()
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(100,0),
            new(100,100),new(0,100)]),[]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var block=new ComponentGeometry("Block",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[],[],[]);
        var cutter=new SteppedSectionDefinition("Step",
            [new Point2Mm(0,50),new Point2Mm(50,50),new Point2Mm(50,60),new Point2Mm(100,60)]);
        var result=SectionProjector.BuildSection(new GeometryModel([block],[]),cutter,
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(result.Success);
        Assert.Equal("SteppedExplicit-v1",cutter.Version);
        var intervals=result.Value!.Regions.Select(r=>(
            Min:r.Footprint.Outer.Vertices.Min(p=>p.X),
            Max:r.Footprint.Outer.Vertices.Max(p=>p.X))).OrderBy(s=>s.Min).ToArray();
        Assert.Equal(new[]{(0d,50d),(50d,60d),(60d,110d)},intervals);
        Assert.Equal(110,intervals.Sum(i=>i.Max-i.Min),6);
    }

    [Fact]
    public void ExplicitCutterSubtractsFullDepthRectangularRoofOpening()
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(100,0),
            new(100,100),new(0,100)]),[]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new RectangleProfile(new Rectangle2Mm(30,30,70,70));
        var opening=new OpeningProfile("Roof/Hole","Roof",OpeningKind.Box,
            cut,null,cut,null,world,z,z,[]);
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var cutter=new SteppedSectionDefinition("RoofStep",
            [new Point2Mm(0,50),new Point2Mm(100,50)]);
        var section=SectionProjector.BuildSection(new GeometryModel([roof],[opening]),cutter,
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        var intervals=section.Value!.Regions.Select(r=>(
            Min:r.Footprint.Outer.Vertices.Min(p=>p.X),
            Max:r.Footprint.Outer.Vertices.Max(p=>p.X))).OrderBy(x=>x.Min).ToArray();
        Assert.Equal(new[]{(0d,30d),(70d,100d)},intervals);
        var voidProfile=Assert.Single(section.Value.Voids);
        Assert.Equal("Roof/Hole",voidProfile.SourceOpeningId);
        Assert.Equal(30,voidProfile.Boundary.Vertices.Min(p=>p.X));
        Assert.Equal(70,voidProfile.Boundary.Vertices.Max(p=>p.X));
    }

    [Fact]
    public void ExplicitCutterKeepsMaterialAboveAndBelowPartialDepthCut()
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(100,0),
            new(100,100),new(0,100)]),[]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var cut=new RectangleProfile(new Rectangle2Mm(30,30,70,70));
        var opening=new OpeningProfile("Roof/Recess","Roof",OpeningKind.Box,
            cut,null,cut,null,world,new Interval1D(25,75),new Interval1D(25,75),[]);
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var cutter=new SteppedSectionDefinition("RoofStep",
            [new Point2Mm(0,50),new Point2Mm(100,50)]);
        var section=SectionProjector.BuildSection(new GeometryModel([roof],[opening]),cutter,
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double area=section.Value!.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.Equal(8000,area,6);
        var voidProfile=Assert.Single(section.Value.Voids);
        Assert.Equal(25,voidProfile.Boundary.Vertices.Min(p=>p.Y));
        Assert.Equal(75,voidProfile.Boundary.Vertices.Max(p=>p.Y));
    }

    [Fact]
    public void ExplicitCutterUnionsOverlappingPartialDepthCuts()
    {
        var footprint=new Footprint(new Polygon2Mm([new(0,0),new(100,0),
            new(100,100),new(0,100)]),[]);
        var z=new Interval1D(0,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        OpeningProfile Cut(string id,double x0,double x1,double z0,double z1)
        {
            var box=new RectangleProfile(new Rectangle2Mm(x0,30,x1,70));
            var range=new Interval1D(z0,z1);
            return new OpeningProfile(id,"Roof",OpeningKind.Box,box,null,box,null,
                world,range,range,[]);
        }
        var a=Cut("A",20,50,20,80);
        var b=Cut("B",40,70,40,60);
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[a,b],[],[]);
        var cutter=new SteppedSectionDefinition("RoofStep",
            [new Point2Mm(0,50),new Point2Mm(100,50)]);
        var section=SectionProjector.BuildSection(new GeometryModel([roof],[a,b]),cutter,
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.True(section.Success);
        double area=section.Value!.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.Equal(7800,area,6);
        Assert.Equal(new[]{"A","B"},section.Value.Voids.Select(v=>v.SourceOpeningId).ToArray());
    }

    [Fact]
    public void PlanSliceCutsHostOpeningAndOmitsCatchFloorBelowSlice()
    {
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        ExtrusionSolid Box(double x0,double y0,double x1,double y1,double z0,double z1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0,y0),new(x1,y0),
                new(x1,y1),new(x0,y1)]),[]);
            return new ExtrusionSolid(footprint,world,new Interval1D(z0,z1));
        }
        var hostSolid=Box(-650,625,650,775,0,1400);
        var host=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",
            hostSolid.Footprint,hostSolid.Depth,hostSolid,[],[],[]);
        var cut=new AxisAlignedCut(new Rectangle2Mm(-200,625,200,775),
            new Interval1D(1100,1400));
        var owned=new OwnedOpeningChild("Wall/T1/Catch","Wall/T1","trace",[cut],18_000_000,[]);
        var catchGeometry=new CatchBasinGeometry(new UnionSolid([
            Box(-400,775,400,1375,800,900),
            Box(-400,775,-300,1275,900,1400),
            Box(300,775,400,1275,900,1400),
            Box(-400,1275,400,1375,900,1400)]),
            [owned],[],[],new Interval1D(1100,1400),800,150,150_000,138_000_000);
        var result=PlanProjector.BuildPlanAtElevation(new GeometryModel([host],[]),1250,catchGeometry);
        Assert.True(result.Success);
        double Area(MaterialRegion region)
        {
            var p=region.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        }
        Assert.Equal(135_000,result.Value!.Regions.Where(r=>r.SourceComponentId=="Wall/T1").Sum(Area),6);
        Assert.Equal(180_000,result.Value.Regions.Where(r=>r.SourceComponentId=="Catch").Sum(Area),6);
        var voidRegion=Assert.Single(result.Value.Voids);
        Assert.Equal("Wall/T1/Catch",voidRegion.SourceOpeningId);
        Assert.Equal(ProjectedVoidRole.ConcreteCutout,voidRegion.Role);
    }

    [Fact]
    public void PlanSliceUsesNeckCollarAndPanelFootprintsAtTheirOwnHeights()
    {
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        ComponentGeometry Ring(string id,ComponentKind kind,double outerHalf,double innerHalf,
            double z0,double z1)
        {
            var outer=new Polygon2Mm([new(-outerHalf,-outerHalf),new(outerHalf,-outerHalf),
                new(outerHalf,outerHalf),new(-outerHalf,outerHalf)]);
            var hole=new Polygon2Mm([new(-innerHalf,-innerHalf),new(-innerHalf,innerHalf),
                new(innerHalf,innerHalf),new(innerHalf,-innerHalf)]);
            var footprint=new Footprint(outer,[hole]);
            var depth=new Interval1D(z0,z1);
            return new ComponentGeometry(id,kind,"Concrete",footprint,depth,
                new ExtrusionSolid(footprint,world,depth),[],[],[]);
        }
        ComponentGeometry Panel(string id,double x0,double x1)
        {
            var footprint=new Footprint(new Polygon2Mm([new(x0,-300),new(x1,-300),
                new(x1,300),new(x0,300)]),[]);
            var depth=new Interval1D(250,350);
            return new ComponentGeometry(id,ComponentKind.Panel,"Concrete",footprint,depth,
                new ExtrusionSolid(footprint,world,depth),[],[],[]);
        }
        var model=new GeometryModel([Ring("Neck",ComponentKind.Neck,450,350,0,200),
            Ring("Collar",ComponentKind.Collar,500,300,200,250),
            Panel("Panel/A",-300,0),Panel("Panel/B",0,300)],[]);
        double Area(ViewProfile view)=>view.Regions.Sum(r=>
        {
            var points=r.Footprint.Outer.Vertices;
            return (points.Max(p=>p.X)-points.Min(p=>p.X))*
                (points.Max(p=>p.Y)-points.Min(p=>p.Y));
        });
        var neck=PlanProjector.BuildPlanAtElevation(model,100);
        var collar=PlanProjector.BuildPlanAtElevation(model,225);
        var panels=PlanProjector.BuildPlanAtElevation(model,300);
        Assert.True(neck.Success);Assert.True(collar.Success);Assert.True(panels.Success);
        Assert.Equal(320_000,Area(neck.Value!),6);
        Assert.Equal(640_000,Area(collar.Value!),6);
        Assert.Equal(360_000,Area(panels.Value!),6);
        Assert.Equal(new[]{"Panel/A","Panel/B"},
            panels.Value!.Regions.Select(r=>r.SourceComponentId).Distinct().Order().ToArray());
    }

    [Fact]
    public void PlanSliceUsesExactRoundChordAtRequestedElevation()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var circle=new CircleProfile(new Point2Mm(0,500),100);
        var opening=new OpeningProfile("Round","Wall/T1",OpeningKind.Round,
            circle,circle,circle,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(400,600),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var result=PlanProjector.BuildPlanAtElevation(new GeometryModel([wall],[opening]),560);
        Assert.True(result.Success);
        var cut=Assert.Single(result.Value!.Voids);
        Assert.Equal(-80,cut.Boundary.Vertices.Min(p=>p.X),6);
        Assert.Equal(80,cut.Boundary.Vertices.Max(p=>p.X),6);
        double area=result.Value.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.Equal(171_000,area,6);
    }

    [Fact]
    public void PlanSliceTreatsRegisteredOpenTopCatchAsPhysicalHostCut()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var shape=new OpenTopProfile(new Polygon2Mm([new(-200,1100),new(200,1100),
            new(200,1400),new(-200,1400)]));
        var opening=new OpeningProfile("Catch","Wall/T1",OpeningKind.OpenTop,
            shape,shape,shape,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(1100,1400),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var result=PlanProjector.BuildPlanAtElevation(new GeometryModel([wall],[opening]),1250);
        Assert.True(result.Success);
        var cut=Assert.Single(result.Value!.Voids);
        Assert.Equal(ProjectedVoidRole.ConcreteCutout,cut.Role);
        double material=result.Value.Regions.Sum(r=>
        {
            var p=r.Footprint.Outer.Vertices;
            return (p.Max(v=>v.X)-p.Min(v=>v.X))*(p.Max(v=>v.Y)-p.Min(v=>v.Y));
        });
        Assert.Equal(135_000,material,6);
    }

    [Fact]
    public void LongitudinalSectionCutsRegisteredOpenTopHostOpening()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var z=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var shape=new OpenTopProfile(new Polygon2Mm([new(-200,1100),new(200,1100),
            new(200,1400),new(-200,1400)]));
        var opening=new OpeningProfile("Catch","Wall/T1",OpeningKind.OpenTop,
            shape,shape,shape,frame,new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
            new Interval1D(1100,1400),new Interval1D(0,150),[]);
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[opening],[],[]);
        var result=SectionProjector.BuildSection(new GeometryModel([wall],[opening]),
            new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection));
        Assert.True(result.Success);
        var material=Assert.Single(result.Value!.Regions.Where(r=>r.SourceComponentId=="Wall/T1"));
        Assert.Equal(0,material.Footprint.Outer.Vertices.Min(p=>p.Y));
        Assert.Equal(1100,material.Footprint.Outer.Vertices.Max(p=>p.Y));
        var cut=Assert.Single(result.Value.Voids.Where(v=>v.SourceOpeningId=="Catch" &&
            v.Role==ProjectedVoidRole.ConcreteCutout));
        Assert.Equal(1100,cut.Boundary.Vertices.Min(p=>p.Y));
        Assert.Equal(1400,cut.Boundary.Vertices.Max(p=>p.Y));
    }

    [Fact]
    public void TwoOverlappingRoundHolesOnOneWallProduceUnionedMaterialCut()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        OpeningProfile Hole(string id,double z)
        {
            var circle=new CircleProfile(new Point2Mm(0,z),305);
            return new OpeningProfile(id,"Wall/T1",OpeningKind.Round,circle,circle,circle,frame,
                new Frame3Mm(frame.Origin,frame.Left,frame.Up,frame.Outward),
                new Interval1D(z-305,z+305),new Interval1D(0,150),[]);
        }
        var a=Hole("A",500);
        var b=Hole("B",1000);
        var footprint=new Footprint(new Polygon2Mm([new(-650,625),new(650,625),
            new(650,775),new(-650,775)]),[]);
        var range=new Interval1D(0,1400);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var wall=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,range,
            new ExtrusionSolid(footprint,world,range),[a,b],[],[]);
        var result=SectionProjector.BuildSection(new GeometryModel([wall],[a,b]),
            new SectionDefinition("AA",SectionAxis.AlongY,0,SectionMode.TrueSection));
        Assert.True(result.Success);
        var bands=result.Value!.Regions.Where(r=>r.SourceComponentId=="Wall/T1")
            .Select(r=>(Min:r.Footprint.Outer.Vertices.Min(p=>p.Y),
                Max:r.Footprint.Outer.Vertices.Max(p=>p.Y))).OrderBy(x=>x.Min).ToArray();
        Assert.Equal(2,bands.Length);
        Assert.Equal((0d,195d),bands[0]);
        Assert.Equal((1305d,1400d),bands[1]);
    }

    [Fact]
    public void DoubleBoxMiddleWallIsNotProjectedAsTwoInnerVoids()
    {
        var entry = new PipeCatalogEntry("DB1000", PipeKind.DoubleBox, null, 1000, 600, 50, 100, 50);
        var pipe = new PipeConnectionSpec(WallId.T1, entry.Code, 300, 0, 40, []);
        var section = PipeCrossSectionBuilder.BuildBox(entry, pipe, new DoubleBoxPolicy()).Value!;
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var opening = new OpeningProfile("DB", "Wall/T1", OpeningKind.Box,
            new MultiCellProfile(section.InnerCells.Select(c => c.Profile)), section.PipeOuter,
            section.ConcreteCutout, frame, new Frame3Mm(frame.Origin, frame.Left, frame.Up, frame.Outward),
            new Interval1D(210, 990), new Interval1D(0, 150), []);
        var footprint = new Footprint(new Polygon2Mm([new(-650, 625), new(650, 625),
            new(650, 775), new(-650, 775)]), []);
        var z = new Interval1D(0, 1400);
        var world = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        var wall = new ComponentGeometry("Wall/T1", ComponentKind.Wall, "Concrete", footprint, z,
            new ExtrusionSolid(footprint, world, z), [opening], [], []);
        var model = new GeometryModel([wall], [opening]);
        var middle = SectionProjector.BuildSection(model, new SectionDefinition("Middle", SectionAxis.AlongY, 0, SectionMode.TrueSection));
        Assert.True(middle.Success);
        Assert.DoesNotContain(middle.Value!.Voids, v => v.SourceOpeningId == opening.Id && v.Role == ProjectedVoidRole.Inner);
        var middleWall = Assert.Single(middle.Value.Regions.Where(r => r.SourceComponentId == opening.Id));
        Assert.Equal(250, middleWall.Footprint.Outer.Vertices.Min(p => p.Y), 6);
        Assert.Equal(950, middleWall.Footprint.Outer.Vertices.Max(p => p.Y), 6);
        var cell = SectionProjector.BuildSection(model, new SectionDefinition("Cell", SectionAxis.AlongY, -275, SectionMode.TrueSection));
        Assert.True(cell.Success);
        var inner = Assert.Single(cell.Value!.Voids.Where(v => v.SourceOpeningId == opening.Id && v.Role == ProjectedVoidRole.Inner));
        Assert.Equal(300, inner.Boundary.Vertices.Min(p => p.Y), 6);
        Assert.Equal(900, inner.Boundary.Vertices.Max(p => p.Y), 6);
        Assert.Equal(2, cell.Value.Regions.Count(r => r.SourceComponentId == opening.Id));
        var typed = SectionProjector.BuildSection(model,
            new SectionPlane("TypedCell", new Point3Mm(-275, 0, 0), new Vector3(1, 0, 0)),
            CutMode.TrueSection, CalculationPolicy.Select("S2Geometry-1", true).Value!);
        Assert.True(typed.Success);
        Assert.Equal(300, typed.Value!.Voids.Single(v => v.SourceOpeningId == opening.Id &&
            v.Role == ProjectedVoidRole.Inner).Boundary.Vertices.Min(p => p.Y), 6);
        var plan = PlanProjector.BuildPlan(model);
        Assert.True(plan.Success);
        var silhouette = Assert.Single(plan.Value!.Voids);
        Assert.Equal(opening.Id, silhouette.SourceOpeningId);
        Assert.Equal(-590, silhouette.Boundary.Vertices.Min(p => p.X), 6);
        Assert.Equal(590, silhouette.Boundary.Vertices.Max(p => p.X), 6);
    }
}
