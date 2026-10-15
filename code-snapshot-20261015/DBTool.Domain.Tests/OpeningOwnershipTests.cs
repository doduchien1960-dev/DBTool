using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class OpeningOwnershipTests
{
    [Fact]
    public void OrthogonalRoundCutsInOneOwnerHaveExactSteinmetzUnion()
    {
        const double radius=100;
        var footprint=new Footprint(new Polygon2Mm([new(-100,-100),new(100,-100),
            new(100,100),new(-100,100)]),[]);
        var z=new Interval1D(-100,100);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var owner=new ComponentGeometry("Core",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[],[],[]);
        var alongY=new WallFrame(WallId.T1,new Point3Mm(0,0,0),
            new Vector3(-1,0,0),new Vector3(0,1,0),new Vector3(0,0,1));
        var alongX=new WallFrame(WallId.T2,new Point3Mm(0,0,0),
            new Vector3(0,1,0),new Vector3(1,0,0),new Vector3(0,0,1));
        var circle=new CircleProfile(new Point2Mm(0,0),radius);
        var cutY=new RoundOpeningCut(alongY,circle,new Interval1D(-100,100));
        var cutX=new RoundOpeningCut(alongX,circle,new Interval1D(-100,100));
        var opening=new OpeningSolid("Cross",[cutY,cutX],[]);
        var result=OpeningOwnershipPartitioner.Partition(new GeometryModel([owner],[]),
            opening,new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        var child=Assert.Single(result.Value!.Children);
        double expected=(4*Math.PI-16d/3)*Math.Pow(radius,3);
        Assert.InRange(Math.Abs(child.VolumeMm3-expected),0,1e-6);
        Assert.Equal("Cross",child.ParentId);
        Assert.Equal("Cross/owner/Core",child.TraceId);
        var owned=Assert.Single(child.CrossFrameRoundCells);
        Assert.Equal(2,owned.Cuts.Length);
        Assert.Equal(child.VolumeMm3,owned.VolumeMm3);
        var reordered=OpeningOwnershipPartitioner.Partition(new GeometryModel([owner],[]),
            new OpeningSolid("Cross",[cutX,cutY,cutX],[]),new OpeningOwnershipPolicy());
        Assert.True(reordered.Success);
        Assert.Equal(child.VolumeMm3,Assert.Single(reordered.Value!.Children).VolumeMm3);
        var clippedFootprint=new Footprint(new Polygon2Mm([new(-50,-100),new(100,-100),
            new(100,100),new(-50,100)]),[]);
        var clipped=new ComponentGeometry("Core",ComponentKind.Wall,"Concrete",clippedFootprint,z,
            new ExtrusionSolid(clippedFootprint,world,z),[],[],[]);
        var unsupported=OpeningOwnershipPartitioner.Partition(new GeometryModel([clipped],[]),
            opening,new OpeningOwnershipPolicy());
        Assert.False(unsupported.Success);
        Assert.Null(unsupported.Value);
        Assert.Contains(unsupported.Diagnostics,d=>d.Code=="OPENING_ROUND_UNION_UNSUPPORTED");
    }

    [Fact]
    public void DistinctOverlappingRoundsHaveExactUnionVolumeWithinOneOwner()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var footprint = new Footprint(new Polygon2Mm([new(-200, 625), new(200, 625),
            new(200, 725), new(-200, 725)]), []);
        var z = new Interval1D(-150, 150);
        var world = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        var owner = new ComponentGeometry("Wall/T1", ComponentKind.Wall, "Concrete", footprint, z,
            new ExtrusionSolid(footprint, world, z), [], [], []);
        var model = new GeometryModel([owner], []);
        var first = new RoundOpeningCut(frame,
            new CircleProfile(new Point2Mm(-50, 0), 100), new Interval1D(0, 100));
        var second = new RoundOpeningCut(frame,
            new CircleProfile(new Point2Mm(50, 0), 100), new Interval1D(0, 100));
        var opening = new OpeningSolid("PhysicalHole", [first, second, first], []);
        var result = OpeningOwnershipPartitioner.Partition(model, opening, new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        var child = Assert.Single(result.Value!.Children);
        double lens = 2 * 100d * 100 * Math.Acos(0.5) - 50 * Math.Sqrt(4 * 100d * 100 - 100d * 100);
        double expected = (2 * Math.PI * 100d * 100 - lens) * 100;
        Assert.InRange(Math.Abs(child.VolumeMm3 - expected), 0, 1);
        Assert.Equal("PhysicalHole", child.ParentId);
        Assert.Equal("PhysicalHole/owner/Wall/T1", child.TraceId);
        Assert.NotEmpty(child.RoundUnionCells);
        Assert.InRange(Math.Abs(child.RoundUnionCells.Sum(c=>c.VolumeMm3)-child.VolumeMm3),0,1e-6);
        Assert.Contains(child.RoundUnionCells,c=>c.Disks.Length==2);
    }

    [Fact]
    public void RoundsWithStaggeredDepthsSubtractTheirLensOnlyInSharedDepth()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var footprint = new Footprint(new Polygon2Mm([new(-200, 625), new(200, 625),
            new(200, 775), new(-200, 775)]), []);
        var z = new Interval1D(-150, 150);
        var world = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        var owner = new ComponentGeometry("Wall/T1", ComponentKind.Wall, "Concrete", footprint, z,
            new ExtrusionSolid(footprint, world, z), [], [], []);
        var model = new GeometryModel([owner], []);
        var first = new RoundOpeningCut(frame,
            new CircleProfile(new Point2Mm(-50, 0), 100), new Interval1D(0, 100));
        var second = new RoundOpeningCut(frame,
            new CircleProfile(new Point2Mm(50, 0), 100), new Interval1D(50, 150));
        var result = OpeningOwnershipPartitioner.Partition(model,
            new OpeningSolid("Staggered", [first, second], []), new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        var child = Assert.Single(result.Value!.Children);
        double lens = 2 * 100d * 100 * Math.Acos(0.5) - 50 * Math.Sqrt(4 * 100d * 100 - 100d * 100);
        double expected = 2 * Math.PI * 100d * 100 * 100 - lens * 50;
        Assert.InRange(Math.Abs(child.VolumeMm3 - expected), 0, 1);
        Assert.Equal(new[] { 0d, 50d, 100d },
            child.RoundUnionCells.Select(c=>c.LocalDepth.Start).Order().ToArray());
        Assert.Equal(new[] { 50d, 100d, 150d },
            child.RoundUnionCells.Select(c=>c.LocalDepth.End).Order().ToArray());
        Assert.Equal(new[] { 1, 1, 2 },child.RoundUnionCells.Select(c=>c.Disks.Length).Order().ToArray());
    }

    [Fact]
    public void RoundUnionSubtractsExistingRectangularVoidFromOwnerMaterial()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var outer = new Polygon2Mm([new(-200, 625), new(200, 625),
            new(200, 775), new(-200, 775)]);
        var existingVoid = new Polygon2Mm([new(-50, 650), new(-50, 750),
            new(50, 750), new(50, 650)]);
        var footprint = new Footprint(outer, [existingVoid]);
        var z = new Interval1D(-150, 150);
        var world = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        var owner = new ComponentGeometry("Wall/T1", ComponentKind.Wall, "Concrete", footprint, z,
            new ExtrusionSolid(footprint, world, z), [], [], []);
        var model = new GeometryModel([owner], []);
        var circle = new CircleProfile(new Point2Mm(0, 0), 100);
        var cutA = new RoundOpeningCut(frame, circle, new Interval1D(0, 100));
        var cutB = new RoundOpeningCut(frame, circle, new Interval1D(50, 150));
        var result = OpeningOwnershipPartitioner.Partition(model,
            new OpeningSolid("Round", [cutA, cutB], []), new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        var child = Assert.Single(result.Value!.Children);
        double stripArea = 2 * (50 * Math.Sqrt(100d * 100 - 50d * 50) +
            100d * 100 * Math.Asin(0.5));
        double expected = Math.PI * 100d * 100 * 150 - stripArea * 100;
        Assert.InRange(Math.Abs(child.VolumeMm3 - expected), 0, 1);
    }

    [Fact]
    public void OverlappingRoundOwnersNeedPriorityAndAssignPhysicalCutOnce()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var footprint = new Footprint(new Polygon2Mm([new(-200, 625), new(200, 625),
            new(200, 725), new(-200, 725)]), []);
        var z = new Interval1D(-150, 150);
        var world = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        ComponentGeometry Owner(string id) => new(id, ComponentKind.Wall, "Concrete", footprint, z,
            new ExtrusionSolid(footprint, world, z), [], [], []);
        var model = new GeometryModel([Owner("A"), Owner("B")], []);
        var first = new RoundOpeningCut(frame,
            new CircleProfile(new Point2Mm(-50, 0), 100), new Interval1D(0, 100));
        var second = new RoundOpeningCut(frame,
            new CircleProfile(new Point2Mm(50, 0), 100), new Interval1D(0, 100));
        var opening = new OpeningSolid("Physical", [first, second], []);
        var undecided = OpeningOwnershipPartitioner.Partition(model, opening, new OpeningOwnershipPolicy());
        Assert.False(undecided.Success);
        Assert.Null(undecided.Value);
        Assert.Contains(undecided.Diagnostics, d => d.Code == "OPENING_OWNER_AMBIGUOUS");
        var decided = OpeningOwnershipPartitioner.Partition(model, opening,
            new OpeningOwnershipPolicy(["A", "B"]));
        Assert.True(decided.Success);
        var child = Assert.Single(decided.Value!.Children);
        Assert.Equal("A", child.OwnerId);
        double lens = 2 * 100d * 100 * Math.Acos(0.5) - 50 * Math.Sqrt(4 * 100d * 100 - 100d * 100);
        Assert.InRange(Math.Abs(child.VolumeMm3 - (2 * Math.PI * 100d * 100 - lens) * 100), 0, 1);
    }

    [Fact]
    public void OwnerOverlapOutsideRoundUnionDoesNotRequirePriority()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        var z = new Interval1D(-50, 50);
        var world = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        ComponentGeometry Owner(string id,double x0,double x1)
        {
            var footprint = new Footprint(new Polygon2Mm([new(x0, 625), new(x1, 625),
                new(x1, 725), new(x0, 725)]), []);
            return new ComponentGeometry(id, ComponentKind.Wall, "Concrete", footprint, z,
                new ExtrusionSolid(footprint, world, z), [], [], []);
        }
        var model = new GeometryModel([Owner("A",-200,0),Owner("B",-100,200)], []);
        var a = new RoundOpeningCut(frame,new CircleProfile(new Point2Mm(-140,0),20),new Interval1D(0,100));
        var b = new RoundOpeningCut(frame,new CircleProfile(new Point2Mm(-180,0),20),new Interval1D(0,100));
        var result=OpeningOwnershipPartitioner.Partition(model,new OpeningSolid("H",[a,b],[]),
            new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        var child=Assert.Single(result.Value!.Children);
        Assert.Equal("B",child.OwnerId);
        Assert.InRange(Math.Abs(child.VolumeMm3-2*Math.PI*20*20*100),0,1);
    }

    [Fact]
    public void MixedBoxAndRoundCutUseOneOwnedUnionWithoutDoubleCount()
    {
        var frame = WallFrame.Create(WallId.T1,1000,1250);
        var footprint = new Footprint(new Polygon2Mm([new(-200,625),new(200,625),
            new(200,725),new(-200,725)]),[]);
        var z=new Interval1D(-150,150);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        var owner=new ComponentGeometry("Wall/T1",ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[],[],[]);
        var box=new AxisAlignedCut(new Rectangle2Mm(-50,625,50,725),new Interval1D(-150,150));
        var round=new RoundOpeningCut(frame,new CircleProfile(new Point2Mm(0,0),100),
            new Interval1D(0,100));
        var result=OpeningOwnershipPartitioner.Partition(new GeometryModel([owner],[]),
            new OpeningSolid("Mixed",[box],[round],[]),new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        var child=Assert.Single(result.Value!.Children);
        double strip=2*(50*Math.Sqrt(100d*100-50d*50)+100d*100*Math.Asin(0.5));
        double expected=100d*100*300+Math.PI*100*100*100-strip*100;
        Assert.InRange(Math.Abs(child.VolumeMm3-expected),0,1);
        Assert.NotEmpty(child.Cells);
        Assert.NotEmpty(child.RoundUnionCells);
        Assert.InRange(Math.Abs(child.Cells.Sum(c=>(c.XY.MaxX-c.XY.MinX)*
            (c.XY.MaxY-c.XY.MinY)*c.Z.Length)+
            child.RoundUnionCells.Sum(c=>c.VolumeMm3)-child.VolumeMm3),0,1e-6);
    }

    [Fact]
    public void SingleRoundAcrossOverlappingOwnersAlsoNeedsPriority()
    {
        var frame=WallFrame.Create(WallId.T1,1000,1250);
        var footprint=new Footprint(new Polygon2Mm([new(-200,625),new(200,625),
            new(200,725),new(-200,725)]),[]);
        var z=new Interval1D(-150,150);
        var world=new Frame3Mm(new Point3Mm(0,0,0),new Vector3(1,0,0),
            new Vector3(0,1,0),new Vector3(0,0,1));
        ComponentGeometry Owner(string id)=>new(id,ComponentKind.Wall,"Concrete",footprint,z,
            new ExtrusionSolid(footprint,world,z),[],[],[]);
        var model=new GeometryModel([Owner("A"),Owner("B")],[]);
        var cut=new RoundOpeningCut(frame,new CircleProfile(new Point2Mm(0,0),100),
            new Interval1D(0,100));
        var opening=new OpeningSolid("Single",[cut],[]);
        var undecided=OpeningOwnershipPartitioner.Partition(model,opening,new OpeningOwnershipPolicy());
        Assert.False(undecided.Success);
        Assert.Null(undecided.Value);
        Assert.Contains(undecided.Diagnostics,d=>d.Code=="OPENING_OWNER_AMBIGUOUS");
        var decided=OpeningOwnershipPartitioner.Partition(model,opening,
            new OpeningOwnershipPolicy(["A","B"]));
        Assert.True(decided.Success);
        var child=Assert.Single(decided.Value!.Children);
        Assert.Equal("A",child.OwnerId);
        Assert.InRange(Math.Abs(child.VolumeMm3-Math.PI*100*100*100),0,1);
    }

    [Fact]
    public void CrossingTwoAdjacentOwnersCreatesTwoUniqueChildrenAndDeduplicatesCut()
    {
        var left = Component("Left", 0, 500);
        var right = Component("Right", 500, 1000);
        var model = new GeometryModel([left, right], []);
        var cut = new AxisAlignedCut(new Rectangle2Mm(400, 0, 600, 100), new Interval1D(0, 100));
        var opening = new OpeningSolid("Hole", [cut, cut], []);
        var result = OpeningOwnershipPartitioner.Partition(model, opening, new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        var children = result.Value!.Children;
        Assert.Equal(2, children.Length);
        Assert.Equal(new[] { "Left", "Right" }, children.Select(x => x.OwnerId).OrderBy(x => x).ToArray());
        Assert.All(children, child => Assert.Equal(1_000_000, child.VolumeMm3, 6));
        Assert.Equal(2_000_000, children.Sum(x => x.VolumeMm3), 6);
        Assert.Equal(2, children.Select(x => x.TraceId).Distinct().Count());
        Assert.All(children, child => Assert.Equal("Hole", child.ParentId));
    }

    [Fact]
    public void OverlappingOwnersRequireExplicitPriorityAndNeverDoubleCount()
    {
        var model = new GeometryModel([Component("Left", 0, 500), Component("Right", 250, 750)], []);
        var opening = new OpeningSolid("Hole", [new AxisAlignedCut(
            new Rectangle2Mm(200, 0, 550, 100), new Interval1D(0, 100))], []);
        var undecided = OpeningOwnershipPartitioner.Partition(model, opening, new OpeningOwnershipPolicy());
        Assert.False(undecided.Success);
        Assert.Null(undecided.Value);
        Assert.Contains(undecided.Diagnostics, d => d.Code == "OPENING_OWNER_AMBIGUOUS" && d.Severity == DiagnosticSeverity.Block);
        var decided = OpeningOwnershipPartitioner.Partition(model, opening,
            new OpeningOwnershipPolicy(["Left", "Right"]));
        Assert.True(decided.Success);
        Assert.Equal(3_500_000, decided.Value!.Children.Sum(c => c.VolumeMm3), 6);
        Assert.Equal(3_000_000, decided.Value.Children.Single(c => c.OwnerId == "Left").VolumeMm3, 6);
        Assert.Equal(500_000, decided.Value.Children.Single(c => c.OwnerId == "Right").VolumeMm3, 6);
    }

    [Fact]
    public void RoundOpeningCrossingBaseSplitsExactCircularSegmentByOwner()
    {
        var frame = WallFrame.Create(WallId.T1, 1000, 1250);
        ComponentGeometry Part(string id, double z0, double z1)
        {
            var footprint = new Footprint(new Polygon2Mm([new(-500, 625), new(500, 625),
                new(500, 775), new(-500, 775)]), []);
            var range = new Interval1D(z0, z1);
            var world = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(0, 1, 0), new Vector3(0, 0, 1));
            return new ComponentGeometry(id, ComponentKind.Wall, "Concrete", footprint, range,
                new ExtrusionSolid(footprint, world, range), [], [], []);
        }
        var model = new GeometryModel([Part("Base",-200,0), Part("Wall/T1",0,1000)], []);
        var roundCut = new RoundOpeningCut(frame,
            new CircleProfile(new Point2Mm(0, 200), 305), new Interval1D(0,150));
        var opening = new OpeningSolid("Round", [roundCut, roundCut], []);
        var result = OpeningOwnershipPartitioner.Partition(model, opening, new OpeningOwnershipPolicy());
        Assert.True(result.Success);
        double cap = (305d*305*Math.Acos(200d/305)-200*Math.Sqrt(305d*305-200d*200))*150;
        Assert.InRange(Math.Abs(result.Value!.Children.Single(c=>c.OwnerId=="Base").VolumeMm3-cap),0,1);
        Assert.InRange(Math.Abs(result.Value.Children.Sum(c=>c.VolumeMm3)-Math.PI*305*305*150),0,1);
    }

    private static ComponentGeometry Component(string id, double minX, double maxX)
    {
        var footprint = new Footprint(new Polygon2Mm([
            new(minX, 0), new(maxX, 0), new(maxX, 100), new(minX, 100)]), []);
        var z = new Interval1D(0, 100);
        var frame = new Frame3Mm(new Point3Mm(0, 0, 0), new Vector3(1, 0, 0),
            new Vector3(0, 1, 0), new Vector3(0, 0, 1));
        return new ComponentGeometry(id, ComponentKind.Wall, "Concrete", footprint, z,
            new ExtrusionSolid(footprint, frame, z), [], [], []);
    }
}
