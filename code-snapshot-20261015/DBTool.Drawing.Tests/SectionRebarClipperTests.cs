using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;
namespace DBTool.Drawing.Tests;
public class SectionRebarClipperTests
{
    [Fact]
    public void BackgroundProjectionCannotSpanClearOpening()
    {
        Footprint Rect(double min,double max)=>new(new Polygon2Mm([new(min,0),new(max,0),new(max,1400),new(min,1400)]),[]);
        var cut=SectionRebarClipper.Clip(new(-775,500),new(775,500),[Rect(-775,-625),Rect(625,775)]);
        Assert.Equal(2,cut.Count);
        Assert.Equal(new Point2Mm(-625,500),cut[0].End);
        Assert.Equal(new Point2Mm(625,500),cut[1].Start);
        Assert.Empty(SectionRebarClipper.Clip(new(0,0),new(0,1400),[Rect(-775,-625),Rect(625,775)]));
    }
}
