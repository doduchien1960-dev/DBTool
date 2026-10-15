using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CoverSectionOutlineTests
{
    private static readonly SourceRef Source=new(SourceKind.SyntheticFixture,"outline-fixture",new string('a',64),null,null,null,"Explicit section drawing rule");
    [Theory]
    [InlineData(700,700,150,1000,1000)]
    [InlineData(700,700,200,1100,1100)]
    [InlineData(820,570,150,870,1120)]
    [InlineData(570,820,200,1220,970)]
    public void CurrentRuleUsesActualNeckThicknessAndWorldSectionDirection(double width,double length,double thickness,double aa,double bb)
    {
        var neck=new NeckSpec(width,length,thickness,400,NeckPosition.Center,[Source]);
        var rule=CoverSectionOutlineRule.FromNeckThickness(neck);
        var result=CoverSectionOutlineBuilder.Build(new RectangleProfile(new(-width/2,-length/2,width/2,length/2)),
            new(123,-456),1800,0,rule);
        Assert.True(result.Success);
        Assert.Equal(aa,result.Value!.AA.MaxX-result.Value.AA.MinX);
        Assert.Equal(bb,result.Value.BB.MaxX-result.Value.BB.MinX);
        Assert.Equal(1700,result.Value.AA.MinY);Assert.Equal(1800,result.Value.AA.MaxY);
        Assert.Equal("OWNER-COVER-20261012-v2",rule.RuleVersion);
        Assert.Equal(-456,(result.Value.AA.MinX+result.Value.AA.MaxX)/2);
        Assert.Equal(123,(result.Value.BB.MinX+result.Value.BB.MaxX)/2);
        Assert.Contains(rule.Sources,s=>s.SourceKind==SourceKind.RuleDocument);
    }
    [Theory]
    [InlineData(0,820,1070)]
    [InlineData(90,1070,820)]
    [InlineData(180,820,1070)]
    [InlineData(270,1070,820)]
    [InlineData(-90,1070,820)]
    [InlineData(450,1070,820)]
    public void RectangleUsesActualApertureAndRotatedAxes(double angle,double aaWidth,double bbWidth)
    {
        // Offset native aperture reference must not leak into the world section centre.
        var result=CoverSectionOutlineBuilder.Build(new RoundedRectangleProfile(new(12,22),new(820,570),20),
            new(100,200),1600,angle,new(125,80,[Source]));
        Assert.True(result.Success);
        Assert.Equal(new Rectangle2Mm(200-aaWidth/2,1520,200+aaWidth/2,1600),result.Value!.AA);
        Assert.Equal(new Rectangle2Mm(100-bbWidth/2,1520,100+bbWidth/2,1600),result.Value.BB);
        Assert.Same(Source,Assert.Single(result.Value.Rule.Sources));
    }
    [Theory]
    [InlineData(125,80,900,1520)]
    [InlineData(60,35,770,1565)]
    public void CircleUsesDiameterAndSuppliedRuleRatherThanNamedCoverSize(double extension,double depth,double width,double bottom)
    {
        var result=CoverSectionOutlineBuilder.Build(new CircleProfile(new(30,-20),325),new(100,200),1600,90,new(extension,depth,[Source]));
        Assert.True(result.Success);
        Assert.Equal(new Rectangle2Mm(200-width/2,bottom,200+width/2,1600),result.Value!.AA);
        Assert.Equal(new Rectangle2Mm(100-width/2,bottom,100+width/2,1600),result.Value.BB);
    }
    [Theory]
    [InlineData(45,1600,"COVER_OUTLINE_ROTATION_UNSUPPORTED")]
    [InlineData(double.NaN,1600,"COVER_OUTLINE_PLACEMENT_INVALID")]
    [InlineData(0,double.PositiveInfinity,"COVER_OUTLINE_PLACEMENT_INVALID")]
    public void InvalidPlacementIsBlockedWithoutApproximation(double angle,double top,string code)
    {
        var result=CoverSectionOutlineBuilder.Build(new CircleProfile(new(0,0),325),new(0,0),top,angle,new(125,80,[Source]));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code==code && d.Severity==DiagnosticSeverity.Block);
    }
    [Fact]
    public void OverflowIsBlockedBeforeConstructingInvalidRectangle()
    {
        var result=CoverSectionOutlineBuilder.Build(new CircleProfile(new(0,0),325),new(double.MaxValue,0),1600,0,new(double.MaxValue,80,[Source]));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_OUTLINE_OVERFLOW");
    }
}
