using DBTool.Domain;
using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Drawing.Tests;

public sealed class SheetOneSectionViewsTests
{
    [Theory]
    [InlineData("AA",325)]
    [InlineData("BB",-325)]
    public void OffsetAccessOpeningRemainsOpenInBothPublishedSections(string view,double center)
    {
        Polygon2Mm Rect(double x0,double y0,double x1,double y1)=>new([new(x0,y0),new(x1,y0),new(x1,y1),new(x0,y1)]);
        var hole=Rect(-575,-25,-75,675);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        ComponentGeometry Component(string id,ComponentKind kind,Footprint fp,double lo,double hi)=>
            new(id,kind,"Concrete",fp,new(lo,hi),new ExtrusionSolid(fp,frame,new(lo,hi)),[],[],[]);
        var roof=Component("Roof",ComponentKind.Roof,new(Rect(-725,-825,725,825),[new Polygon2Mm(hole.Vertices.Reverse())]),1450,1600);
        var neck=Component("Neck",ComponentKind.Neck,new(Rect(-700,-150,50,800),[new Polygon2Mm(hole.Vertices.Reverse())]),1600,2100);
        var basis=Component("Base",ComponentKind.Base,new(Rect(-825,-925,825,925),[]),-150,0);
        var geometry=new GeometryModel([basis,roof,neck],[]);
        var axis=view=="AA"?SectionAxis.AlongY:SectionAxis.AlongX;
        var initial=SectionProjector.BuildSection(geometry,new(view,axis,0,SectionMode.TrueSection));
        Assert.True(initial.Success);
        var result=SheetOneSectionViews.ThroughNeck(geometry,initial.Value!);
        Assert.True(result.Success);
        Assert.Equal(2,result.Value!.Regions.Count(r=>r.SourceComponentId=="Neck"));
        Assert.DoesNotContain(result.Value.Regions.Where(r=>r.SourceComponentId is "Neck" or "Roof"),r=>
            r.Footprint.Outer.Vertices.Min(p=>p.X)<center && r.Footprint.Outer.Vertices.Max(p=>p.X)>center);
        Assert.Equal(initial.Value!.Regions.Where(r=>r.SourceComponentId=="Base"),result.Value.Regions.Where(r=>r.SourceComponentId=="Base"));
        Assert.Equal(-575,geometry.Components.Single(c=>c.Id=="Neck").Footprint.InnerLoops[0].Vertices.Min(p=>p.X));
    }

    [Fact]
    public void BoundaryTouchingHoleCutRemainsSplitAndDoesNotBecomeSolidBlock()
    {
        Polygon2Mm Rect(double x0,double y0,double x1,double y2)=>new([new(x0,y0),new(x1,y0),new(x1,y2),new(x0,y2)]);
        var hole=Rect(0,-350,700,350);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var outer=Rect(-150,-500,850,500);
        var ring=new Footprint(outer,[new Polygon2Mm(hole.Vertices.Reverse())]);
        var neck=new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",ring,new(2100,2500),new ExtrusionSolid(ring,frame,new(2100,2500)),[],[],[]);
        var geometry=new GeometryModel([neck],[]);
        var section=SectionProjector.BuildSection(geometry,new("AA",SectionAxis.AlongY,0,SectionMode.TrueSection));
        Assert.True(section.Success);
        var neckRegions=section.Value!.Regions.Where(r=>r.SourceComponentId=="Neck").ToArray();
        Assert.Equal(2,neckRegions.Length);
        Assert.DoesNotContain(neckRegions,r=>r.Footprint.Outer.Vertices.Min(p=>p.X)<0 && r.Footprint.Outer.Vertices.Max(p=>p.X)>0);
    }

    [Fact]
    public void OffsetCornerNeckSuppressesRoofHaunchAtAdjacentWall()
    {
        Polygon2Mm Rect(double x0,double y0,double x1,double y2)=>new([new(x0,y0),new(x1,y0),new(x1,y2),new(x0,y2)]);
        var hole=Rect(100,650,900,1450);
        var frame=new Frame3Mm(new(0,0,0),new(1,0,0),new(0,1,0),new(0,0,1));
        var roofFootprint=new Footprint(Rect(-1100,-1650,1100,1650),[new Polygon2Mm(hole.Vertices.Reverse())]);
        var roof=new ComponentGeometry("Roof",ComponentKind.Roof,"Concrete",roofFootprint,new(2550,2750),new ExtrusionSolid(roofFootprint,frame,new(2550,2750)),[],[],[]);
        var neckOuter=Rect(-50,500,1050,1600);
        var neckFp=new Footprint(neckOuter,[new Polygon2Mm(hole.Vertices.Reverse())]);
        var neck=new ComponentGeometry("Neck",ComponentKind.Neck,"Concrete",neckFp,new(2750,3150),new ExtrusionSolid(neckFp,frame,new(2750,3150)),[],[],[]);
        var haunchSolid=new HaunchSolid(1800,2900,100,100,HaunchOwner.Roof,new(2450,2550));
        var haunchFp=new Footprint(Rect(-900,-1450,900,1450),[]);
        var haunch=new ComponentGeometry("Roof/Haunch",ComponentKind.Roof,"Concrete",haunchFp,new(2450,2550),haunchSolid,[],[],[]);
        var geometry=new GeometryModel([roof,neck,haunch],[]);
        var section=SectionProjector.BuildSection(geometry,new("AA",SectionAxis.AlongY,500,SectionMode.TrueSection));
        Assert.True(section.Success);
        var haunchRegions=section.Value!.Regions.Where(r=>r.SourceComponentId=="Roof/Haunch").ToArray();
        Assert.Single(haunchRegions);
        Assert.True(haunchRegions[0].Footprint.Outer.Vertices.Max(p=>p.X)<=0);
    }
}
