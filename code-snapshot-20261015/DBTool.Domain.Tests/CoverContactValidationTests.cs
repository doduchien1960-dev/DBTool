using DBTool.Domain.Geometry;
using Xunit;

namespace DBTool.Domain.Tests;

public sealed class CoverContactValidationTests
{
    private static readonly SourceRef Source=new(SourceKind.SyntheticFixture,"contact-fixture",new string('a',64),null,null,null,"Explicit synthetic frame contact and bearing requirement");

    [Theory]
    [InlineData(0,true)]
    [InlineData(90,false)]
    [InlineData(180,true)]
    [InlineData(270,false)]
    public void OffCentreRoofSupportFollowsEachNativeAxisAfterQuarterTurns(double angle,bool success)
    {
        var contacts=new[]{"AA","BB"}.Select(view=>
        {
            double edge=view=="AA"?450:350;
            return new CoverContactSection(view,[new(new(-edge,-60),new(-edge+7,-60)),
                new(new(edge-7,-60),new(edge,-60))],[Source]);
        });
        var opening=new Rectangle2Mm(-225,-125,425,525); // actual centre(100,200)
        var surface=new CoverSupportSurface(new(-300,-250,500,650),opening,1550,[Source]);
        var result=Validate(7,angle,contacts,surface,1610,opening);
        Assert.Equal(success,result.Success);
        if(!success) Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_BEARING_INCOMPATIBLE");
    }

    [Theory]
    [InlineData(7,0,true)]
    [InlineData(8,0,false)]
    [InlineData(7,90,true)]
    [InlineData(8,270,false)]
    public void NativeBearingUsesRealFootRatherThanOversizedFrameBounds(double requirement,double angle,bool success)
    {
        var result=Validate(requirement,angle,Contacts());
        Assert.Equal(success,result.Success);
        if(!success) Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_BEARING_INCOMPATIBLE" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void MissingNativeContactCannotBeApprovedFromOuterBounds()
    {
        var result=Validate(7,0,[]);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_CONTACT_GEOMETRY_REQUIRED" && d.Severity==DiagnosticSeverity.Block);
    }

    [Fact]
    public void DifferentFootHeightsCannotBeMergedIntoOneBearingPlane()
    {
        var contacts=new[]{"AA","BB"}.Select(view=>new CoverContactSection(view,[
            new(new(-450,-60),new(-443,-60)),new(new(443,-50),new(450,-50))],[Source]));
        var result=Validate(7,0,contacts);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_CONTACT_PLANE_INCOMPATIBLE");
    }

    [Theory]
    [InlineData(446.5,true)]
    [InlineData(448,false)]
    public void OnlyTouchingContactStripsFormAContinuousBearingWidth(double secondStart,bool success)
    {
        var contacts=new[]{"AA","BB"}.Select(view=>new CoverContactSection(view,[
            new(new(-450,-60),new(-443,-60)),new(new(443,-60),new(446.5,-60)),
            new(new(secondStart,-60),new(450,-60))],[Source]));
        Assert.Equal(success,Validate(7,0,contacts).Success);
    }

    [Fact]
    public void AFootOutsideTheNeckWallCannotCountAsSupportedBearing()
    {
        var contacts=new[]{"AA","BB"}.Select(view=>new CoverContactSection(view,[
            new(new(-1000,-60),new(-993,-60)),new(new(993,-60),new(1000,-60))],[Source]));
        var result=Validate(7,0,contacts);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_BEARING_INCOMPATIBLE");
    }

    private static IEnumerable<CoverContactSection> Contacts()=>new[]{"AA","BB"}.Select(view=>
        new CoverContactSection(view,[new(new(-450,-60),new(-443,-60)),new(new(443,-60),new(450,-60))],[Source]));

    private static OperationResult<CoverOpeningContract> Validate(double requirement,double angle,IEnumerable<CoverContactSection> contacts,
        CoverSupportSurface? support=null,double? top=null,Rectangle2Mm? opening=null)
    {
        var bounds=new Rectangle2Mm(-1000,-1000,1000,1000);
        var views=new[]{"MB","AA","BB"}.Select(view=>new CoverBlockView("FIX."+view,view,new(0,0,0),bounds,new(0,0),[Source]));
        var blocks=new CoverBlockSet(views,new CircleProfile(new(0,0),325),[],[Source],contacts);
        var entry=new CoverCatalogEntry("FIX",new(2000,2000),new(0,0,0),angle,CoverPlacementDatum.NeckTop,bounds,[Source],
            new CoverOpeningContract(new CircleProfile(new(0,0),325),requirement,[Source]),blocks);
        return CoverAssemblyValidator.Validate(entry,opening ?? new(-325,-325,325,325),150,.001,Source,Source,support,top);
    }
}
