using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public class LadderOpeningPolicyTests
{
    [Fact]
    public void PublishedLadderUsesCoverDatumAndSourceOffsetWithRealUpperSupport()
    {
        var (spec,geometry,ladder,policy)=Fixture();
        var (cx,cy)=NeckPositionMap.Center(spec.NeckSpec!.Position,spec.Input.L,spec.Input.B,700,700);
        var footprint=new Footprint(new Polygon2Mm([new(cx-475,cy-475),new(cx+475,cy-475),new(cx+475,cy+475),new(cx-475,cy+475)]),
            [new Polygon2Mm([new(cx-350,cy-350),new(cx-350,cy+350),new(cx+350,cy+350),new(cx+350,cy-350)])]);
        var heights=new Interval1D(geometry.Components.Single(c=>c.Id=="Roof").ZRange.End,2300);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var neck=new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",footprint,heights,new ExtrusionSolid(footprint,frame,heights),[],[],spec.Sources);
        var supported=new GeometryModel(geometry.Components.Append(neck),geometry.Openings);
        var result=LadderPlanner.PlanApproved(ladder,spec,supported,policy,LadderOpeningPolicy.PublishedTruncateAtOpening,15,2130);
        Assert.True(result.Success);
        Assert.Equal(new double[]{1630,1330,1030,730,430},result.Value!.Occurrences.Select(b=>b.Path.Segments[1].Start.Z));
        Assert.All(result.Value.Occurrences,b=>Assert.Equal("LADDER-PUBLISHED-1",b.RuleId));
        Assert.Same(neck,supported.Components.Single(c=>c.Id=="Neck"));
        var unsupported=LadderPlanner.PlanApproved(ladder,spec,geometry,policy,LadderOpeningPolicy.PublishedTruncateAtOpening,15,2130);
        Assert.False(unsupported.Success);
        Assert.Contains(unsupported.Diagnostics,d=>d.Code=="LADDER_EMBED_SUPPORT_MISSING");
        var changed=new LadderSpec(300,300,0,300,250,150,18,sources:spec.Sources);
        var shifted=LadderPlanner.PlanApproved(changed,spec,supported,policy,LadderOpeningPolicy.PublishedTruncateAtOpening,15,2130);
        Assert.True(shifted.Success);
        Assert.Equal(1830,shifted.Value!.Occurrences[0].Path.Segments[1].Start.Z);
        Assert.Equal(6,shifted.Value.Occurrences.Length);
    }
    [Fact]
    public void UnderNeckWallWithoutOpeningIsPreferredAndOldPolicyStillBlocks()
    {
        var (spec,geometry,ladder,policy)=Fixture((WallId.T2,800));
        var result=New(spec,geometry,ladder,policy);
        Assert.True(result.Success);
        Assert.Equal(4,result.Value!.Occurrences.Length);
        Assert.All(result.Value.Occurrences,b=>Assert.Equal("Wall/T3",b.ComponentId));
        var old=LadderPlanner.PlanApproved(ladder,spec,geometry,policy);
        Assert.False(old.Success);
        Assert.Contains(old.Diagnostics,d=>d.Code=="LADDER_OPENING_COLLISION");
    }

    [Fact]
    public void BothWallsWithOpeningsChooseLongestContiguousLadderAndNeverJumpBelowOpening()
    {
        var (spec,geometry,ladder,policy)=Fixture((WallId.T2,800),(WallId.T3,500));
        var result=New(spec,geometry,ladder,policy);
        Assert.True(result.Success);
        Assert.Equal(2,result.Value!.Occurrences.Length);
        Assert.All(result.Value.Occurrences,b=>Assert.Equal("Wall/T3",b.ComponentId));
        Assert.Equal(new[]{1100d,800d},result.Value.Occurrences.Select(b=>b.Path.Segments[1].Start.Z));
        var diagnostic=Assert.Single(result.Diagnostics,d=>d.Code=="LADDER_TRUNCATED_AT_OPENING");
        Assert.Equal(DiagnosticSeverity.Review,diagnostic.Severity);
        Assert.Contains("500",diagnostic.Message);
        Assert.Contains("Opening/T3",diagnostic.Message);
    }

    [Fact]
    public void EveryFirstRungCollidesReturnsEmptyPartialReviewRatherThanBlock()
    {
        var (spec,geometry,ladder,policy)=Fixture((WallId.T2,1100),(WallId.T3,1100));
        var result=New(spec,geometry,ladder,policy);
        Assert.True(result.Success);
        Assert.Empty(result.Value!.Occurrences);
        Assert.Equal(LayoutStatus.Partial,result.Value.Status);
        Assert.Contains(result.Diagnostics,d=>d.Code=="LADDER_TRUNCATED_ALL" && d.Severity==DiagnosticSeverity.Review);
        Assert.DoesNotContain(result.Diagnostics,d=>d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void NoOpeningsPreservesOldPlacementAndNominalLengths()
    {
        var (spec,geometry,ladder,policy)=Fixture();
        var result=New(spec,geometry,ladder,policy);
        var old=LadderPlanner.PlanApproved(ladder,spec,geometry,policy);
        Assert.True(result.Success);Assert.True(old.Success);
        Assert.Equal(old.Value!.Occurrences.Select(b=>b.Id),result.Value!.Occurrences.Select(b=>b.Id));
        Assert.Equal(old.Value.Occurrences.Select(b=>b.ScheduleLengthMm),result.Value.Occurrences.Select(b=>b.ScheduleLengthMm));
        Assert.Equal(old.Value.Occurrences.SelectMany(b=>b.Path.Segments),result.Value.Occurrences.SelectMany(b=>b.Path.Segments));
    }

    [Theory]
    [InlineData(670,1)]
    [InlineData(653,2)]
    public void OpeningCoverAndHalfDiameterAreAppliedOnceToEmbeddedLegs(double openingZ,int expectedRungs)
    {
        var (spec,geometry,ladder,policy)=Fixture((WallId.T2,openingZ),(WallId.T3,openingZ));
        var result=New(spec,geometry,ladder,policy);
        Assert.True(result.Success);
        Assert.Equal(expectedRungs,result.Value!.Occurrences.Length);
    }

    private static OperationResult<RebarLayout> New(ValidatedManholeSpec spec,GeometryModel geometry,LadderSpec ladder,
        CalculationPolicy policy)=>LadderPlanner.PlanApproved(ladder,spec,geometry,policy,LadderOpeningPolicy.TruncateAtOpening,15,1400);

    [Fact]
    public void FirstRungUsesExplicitPlacementDatumMinus300()
    {
        var (spec,geometry,ladder,policy)=Fixture();
        var result=LadderPlanner.PlanApproved(ladder,spec,geometry,policy,LadderOpeningPolicy.TruncateAtOpening,15,1900);
        Assert.True(result.Success);
        Assert.Equal(1600,result.Value!.Occurrences[0].Path.Segments[1].Start.Z);
        Assert.Equal(6,result.Value.Occurrences.Length);
        var missing=LadderPlanner.PlanApproved(ladder,spec,geometry,policy,LadderOpeningPolicy.TruncateAtOpening,15);
        Assert.False(missing.Success);
        Assert.Contains(missing.Diagnostics,d=>d.Code=="LADDER_TOP_DATUM_REQUIRED");
    }

    private static (ValidatedManholeSpec,GeometryModel,LadderSpec,CalculationPolicy) Fixture(params (WallId Wall,double Z)[] cuts)
    {
        var seed=SyntheticTestFactory.Create(pipes:[]);var i=seed.Input;
        var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
        var main=new MainRebarInput(null,null,null,null,30,null,null,StructureKind.Other,null,[],wallSupportCoverMm:30);
        var neck=new NeckSpec(700,700,125,400,NeckPosition.SouthEast,seed.Sources);
        var input=new ManholeInput(i.Id,i.Name,i.Profile,i.L,i.B,i.H,i.BaseThicknessMm,i.WallThicknessMm,i.RoofThicknessMm,
            i.PadThicknessMm,i.BaseExpansionMm,i.PadExpansionMm,i.BeddingMortarMm,i.BaseHaunch,i.TopHaunch,i.NeckHeight,
            i.RoofOpening,[],i.BaseBottomMesh,i.BaseTopMesh,i.CoverMm,i.ElevationMode,i.BaseElevationM,i.PipeInvertReference,
            i.HookBaseBottom,i.HookBaseTop,i.DistributionMode,i.MeshJunctionPolicy,i.LayerStackPolicy,i.FeatureRequests,
            i.SourceRefs,i.UnsupportedRawFields,neckSpec:neck,mainRebar:main);
        var spec=new ValidatedManholeSpec(input,seed.Catalogs,policy.Version,seed.Sources,[]);
        var geometry=ChamberGeometryBuilder.Build(seed,CalculationPolicy.Select("S1-Analytic-1",true).Value!).Value!;
        var openings=cuts.Select(c=>
        {
            var frame=WallFrame.Create(c.Wall,i.L,i.B);
            var (cx,cy)=NeckPositionMap.Center(neck.Position,i.L,i.B,700,700);
            var u=frame.ToLocal(new(cx,cy,0)).U;
            var circle=new CircleProfile(new(u,c.Z),180);
            return new OpeningProfile("Opening/"+c.Wall,"Wall/"+c.Wall,OpeningKind.Round,circle,circle,circle,frame,
                new(frame.Origin,frame.Left,frame.Up,frame.Outward),new(c.Z-180,c.Z+180),new(0,150),seed.Sources);
        });
        return (spec,new GeometryModel(geometry.Components,geometry.Openings.Concat(openings)),new(500,300,0,300,250,150,18,sources:seed.Sources),policy);
    }
}
