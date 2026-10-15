using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Domain.Tests;
public sealed class OwnerCoverAssemblyTests
{
    private static readonly SourceRef Source=new(SourceKind.SyntheticFixture,"owner-fixture",new string('a',64),null,null,null,"Approved nominal drawing contract");
    private static CoverCatalogEntry Entry(double rotation=0) {
        var views=new[]{"MB","AA","BB"}.Select(v=>new CoverBlockView(v,v,new(0,0,0),new(-550,-100,550,100),new(0,0),[Source]));
        var blocks=new CoverBlockSet(views,new CircleProfile(new(0,0),299.5),[],[Source],sectionOutlineRule:new(125,80,[Source]),nominalOpening:new(new(820,570),[Source]));
        return new("OWNER",new(1100,200),new(0,0,0),rotation,CoverPlacementDatum.NeckTop,new(-550,-100,550,100),[Source],blockSet:blocks);
    }
    [Theory]
    [InlineData(70)]
    [InlineData(100)]
    [InlineData(150)]
    public void InstallationRiseIsIndependentOfSectionOutlineDepth(double rise)
    {
        var entry=Entry();
        var result=OwnerCoverAssemblyValidator.Validate(entry,new(-410,-285,410,285),rise,.001,Source,Source);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        Assert.Equal(80,result.Value!.AA.MaxY-result.Value.AA.MinY,6);
        Assert.Equal(80,entry.BlockSet!.SectionOutlineRule!.DepthMm);
    }

    [Theory]
    [InlineData(0,820,570)] [InlineData(90,570,820)] [InlineData(180,820,570)] [InlineData(270,570,820)]
    public void ApprovedNominalContractFollowsAxesAndRetainsPhysicalContour(double rotation,double x,double y) {
        var entry=Entry(rotation);
        var result=OwnerCoverAssemblyValidator.Validate(entry,new(-x/2,-y/2,x/2,y/2),80,.001,Source,Source);
        Assert.True(result.Success,string.Join(";",result.Diagnostics.Select(d=>d.Code)));
        Assert.Equal(299.5,Assert.IsType<CircleProfile>(entry.BlockSet!.MeasuredAperture).RadiusMm);
        Assert.Null(entry.OpeningContract);
    }
    [Theory]
    [InlineData(0,800,800,80,"COVER_OPENING_INCOMPATIBLE")]
    [InlineData(90,820,570,80,"COVER_OPENING_INCOMPATIBLE")]
    [InlineData(45,820,570,80,"COVER_OUTLINE_ROTATION_UNSUPPORTED")]
    [InlineData(0,820,570,-1,"COVER_INSTALLATION_HEIGHT_INVALID")]
    public void WrongOpeningAngleOrHeightRemainBlocking(double angle,double x,double y,double height,string code) {
        var result=OwnerCoverAssemblyValidator.Validate(Entry(angle),new(-x/2,-y/2,x/2,y/2),height,.001,Source,Source);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code==code && d.Severity==DiagnosticSeverity.Block);
    }
}
