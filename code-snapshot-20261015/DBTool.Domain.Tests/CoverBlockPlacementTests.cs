using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CoverBlockPlacementTests
{
    private static readonly SourceRef Source=new(SourceKind.SyntheticFixture,"alignment-fixture",new string('a',64),null,null,null,"Independent per-view alignment fixture");
    private static CoverBlockSet Blocks(bool missing=false)=>new([
        new("FIX.MB","MB",new(0,0,0),new(-500,-400,600,500),new(20,-10),[Source]),
        new("FIX.AA","AA",new(0,0,0),new(-200,-80,400,0),missing?null:new Point2Mm(12,-3),missing?[]:[Source]),
        new("FIX.BB","BB",new(0,0,0),new(-500,-100,600,0),new(-7,5),[Source])],
        null,["MinimumBearingRule"],[Source]);

    [Theory]
    [InlineData(0,80,210,"AA",188,303,1,"BB",107,295,1)]
    [InlineData(90,90,180,"BB",207,295,1,"AA",112,303,-1)]
    [InlineData(180,120,190,"AA",212,303,-1,"BB",93,295,-1)]
    [InlineData(270,110,220,"BB",193,295,-1,"AA",88,303,1)]
    [InlineData(-90,110,220,"BB",193,295,-1,"AA",88,303,1)]
    [InlineData(450,90,180,"BB",207,295,1,"AA",112,303,-1)]
    public void IndependentViewOriginsAndReversedAxesAlignWithTheNeck(double angle,double px,double py,
        string aa,double ax,double ay,int ad,string bb,double bx,double by,int bd)
    {
        var result=Resolve(Blocks(),angle);
        Assert.True(result.Success);
        var placement=result.Value!;
        Assert.Equal(new Point2Mm(px,py),placement.PlanInsertion);
        var a=placement.SectionAA;var b=placement.SectionBB;
        Assert.Equal(aa,a.NativeView);Assert.Equal(bb,b.NativeView);
        Assert.Equal(new Point2Mm(ax,ay),a.Insertion);
        Assert.Equal(new Point2Mm(bx,by),b.Insertion);
        Assert.Equal(ad,a.HorizontalDirection);Assert.Equal(bd,b.HorizontalDirection);
    }

    [Theory]
    [InlineData(45,false,"COVER_VIEW_ROTATION_UNSUPPORTED")]
    [InlineData(0,true,"COVER_VIEW_ALIGNMENT_REQUIRED")]
    public void MissingEvidenceAndObliqueViewsBlockInsteadOfUsingInspectionBounds(double angle,bool missing,string code)
    {
        var result=Resolve(Blocks(missing),angle);
        Assert.False(result.Success);Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code==code && d.Severity==DiagnosticSeverity.Block);
    }

    private static OperationResult<CoverBlockPlacements> Resolve(CoverBlockSet blocks,double angle)=>
        CoverBlockPlacementResolver.Resolve(blocks,new(100,200),300,angle);

    [Fact]
    public void AssemblyCannotApprovePlanOnlyMetadataWhenASectionAlignmentIsMissing()
    {
        var entry=new CoverCatalogEntry("FIX",new(1000,1000),new(-20,10,0),0,CoverPlacementDatum.NeckTop,
            new(-500,-500,500,500),[Source],new CoverOpeningContract(new CircleProfile(new(20,-10),325),100,[Source]),Blocks(true));
        var result=CoverAssemblyValidator.Validate(entry,new(-325,-325,325,325),150,.001,Source,Source);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_VIEW_ALIGNMENT_REQUIRED" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void DeclaredInsertionCannotContradictMeasuredBlockAlignmentEvenWhenOpeningExtentsMatch()
    {
        var entry=new CoverCatalogEntry("FIX",new(1000,1000),new(0,0,0),0,CoverPlacementDatum.NeckTop,
            new(-500,-500,500,500),[Source],new CoverOpeningContract(new CircleProfile(new(0,0),325),100,[Source]),Blocks());
        var result=CoverAssemblyValidator.Validate(entry,new(-325,-325,325,325),150,.001,Source,Source);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_VIEW_ANCHOR_INCOMPATIBLE" && d.Severity==DiagnosticSeverity.Block);
    }
}
