using DBTool.Domain.Geometry;
using DBTool.Domain.Tests.Fixtures;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CoverPanelBuilderTests
{
    private static readonly CalculationPolicy Policy=CalculationPolicy.Select("S2Geometry-1",true).Value!;
    private static NeckSeatProfile Seat(double inner=850,double outer=950,double seatHeight=0)
    {
        var source=SyntheticTestFactory.Create().Sources;
        return new(new Rectangle2Mm(-inner/2,-inner/2,inner/2,inner/2),
            new Rectangle2Mm(-outer/2,-outer/2,outer/2,outer/2),1550,2150,seatHeight,source);
    }
    private static CoverPanelSpec Panels(int count,double gap,SplitAxis axis=SplitAxis.X,PanelSupportMode support=PanelSupportMode.Recess,double capHeight=100) =>
        new(count,gap,axis,support,capHeight,SyntheticTestFactory.Create().Sources);

    [Fact]
    public void TwoRecessPanelsHaveHandComputedDimensionsAndCenters()
    {
        var result=CoverPanelBuilder.Build(Seat(),Panels(2,5),Policy);
        Assert.True(result.Success);
        Assert.Equal(417.5,result.Value!.Panels[0].Bounds.MaxX-result.Value.Panels[0].Bounds.MinX);
        Assert.Equal(840,result.Value.Panels[0].Bounds.MaxY-result.Value.Panels[0].Bounds.MinY);
        Assert.Equal([-211.25,211.25],result.Value.Panels.Select(p=>(p.Bounds.MinX+p.Bounds.MaxX)/2));
        Assert.Equal(["Panel/001","Panel/002"],result.Value.Panels.Select(p=>p.Id));
    }

    [Fact]
    public void SinglePanelAndZeroGapHaveNoPhantomInteriorGap()
    {
        var one=CoverPanelBuilder.Build(Seat(),Panels(1,5),Policy).Value!;
        Assert.Single(one.Panels);
        Assert.Equal(new Rectangle2Mm(-420,-420,420,420),one.Panels[0].Bounds);
        var zero=CoverPanelBuilder.Build(Seat(),Panels(2,0),Policy).Value!;
        Assert.Equal(425,zero.Panels[0].Bounds.MaxX-zero.Panels[0].Bounds.MinX);
        Assert.Equal(850,zero.Panels[0].Bounds.MaxY-zero.Panels[0].Bounds.MinY);
    }

    [Fact]
    public void OverOuterUsesOuterBoundaryAndOnlyInteriorGap()
    {
        var result=CoverPanelBuilder.Build(Seat(),Panels(2,5,support:PanelSupportMode.OverOuter),Policy).Value!;
        Assert.Equal(472.5,result.Panels[0].Bounds.MaxX-result.Panels[0].Bounds.MinX);
        Assert.Equal(950,result.Panels[0].Bounds.MaxY-result.Panels[0].Bounds.MinY);
        Assert.Equal([-238.75,238.75],result.Panels.Select(p=>(p.Bounds.MinX+p.Bounds.MaxX)/2));
    }

    [Fact]
    public void SeatHeightAddsToThicknessAndAxisSwapPreservesVolume()
    {
        var x=CoverPanelBuilder.Build(Seat(seatHeight:50),Panels(2,5),Policy).Value!;
        var y=CoverPanelBuilder.Build(Seat(seatHeight:50),Panels(2,5,SplitAxis.Y),Policy).Value!;
        Assert.Equal(150,x.Panels[0].ZRange.Length);
        Assert.Equal(105210000,x.TotalVolumeMm3,6);
        Assert.Equal(x.TotalVolumeMm3,y.TotalVolumeMm3,6);
        Assert.Equal(417.5,y.Panels[0].Bounds.MaxY-y.Panels[0].Bounds.MinY);
        Assert.Equal(70140000,CoverPanelBuilder.Build(Seat(),Panels(2,5),Policy).Value!.TotalVolumeMm3,6);
    }

    [Fact]
    public void ExcessGapFailsWithBlockAndNoLayout()
    {
        var result=CoverPanelBuilder.Build(Seat(),Panels(2,500),Policy);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="PANEL_CLEARANCE_INVALID" && d.Severity==DiagnosticSeverity.Block);
    }
}
