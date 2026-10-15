using DBTool.Domain;
using DBTool.Application;
using DBTool.Domain.Geometry;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class NativeCoverMeasuredBoundsTests
{
    [Theory]
    [InlineData("Nap1.TNM.LD.700x700",650,650)]
    [InlineData("Nap2.TNM.LD.700x700",700,700)]
    [InlineData("Nap3.TNM.LD.800x800",800,800)]
    [InlineData("Nap4.TNM.LD.650x650",650,650)]
    [InlineData("Nap5.TNM.LD.800x380",800,380)]
    [InlineData("Nap6.TNM.LD.380x800",380,800)]
    [InlineData("Nap7.TNM.LD.700x600",700,600)]
    [InlineData("Nap8.TNM.LD.600x700",600,700)]
    [InlineData("VIN.NBN.CX.800x800",800,800)]
    [InlineData("VIN.TNM.CX.200x500",200,500)]
    [InlineData("VIN.TNM.CX.500x200",500,200)]
    [InlineData("VIN.TNM.CX.500x500",500,500)]
    [InlineData("VIN.TNM.LD.390x820",390,820)]
    [InlineData("VIN.TNM.LD.570x820",570,820)]
    [InlineData("VIN.TNM.LD.700x700",700,700)]
    [InlineData("VIN.TNM.LD.820x390",820,390)]
    [InlineData("VIN.TNM.LD.820x570",820,570)]
    [InlineData("VIN.TNT.CX.700x700",700,700)]
    [InlineData("VIN.TNT.VH.700x700",700,700)]
    public void OwnerSuffixPolicySuppliesNominalOpeningWithoutOverwritingMeasuredProduct(string code,double width,double height)
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("owner-size",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers[code];
            var json=System.Text.Json.JsonSerializer.SerializeToElement(entry.BlockSet);
            Assert.True(json.TryGetProperty("NominalOpening",out var nominal));
            Assert.Equal(width,nominal.GetProperty("Size").GetProperty("Width").GetDouble());
            Assert.Equal(height,nominal.GetProperty("Size").GetProperty("Height").GetDouble());
            Assert.NotEmpty(nominal.GetProperty("Sources").EnumerateArray());
            if(code=="Nap3.TNM.LD.800x800") Assert.Equal(299.5,Assert.IsType<CircleProfile>(entry.BlockSet!.MeasuredAperture).RadiusMm);
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }
    [Fact]
    public void EveryApprovedNativeCoverPassesRealInputAndGeometryWithMatchingOpening()
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var importer=new WorkbookImporter();
            var numeric=new NumericPolicy("en-US",false,false);
            var initial=importer.Read(new("native-production",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var covers=new CatalogReader().Read(initial,numeric).Value!.Covers.Values.Where(c=>c.BlockSet?.NominalOpening is not null).ToArray();
            Assert.Equal(19,covers.Length);
            foreach(var cover in covers)
            {
                using(var book=new ClosedXML.Excel.XLWorkbook(path))
                {
                    var config=book.Worksheet("CF.Ga");
                    var size=cover.BlockSet!.NominalOpening!.Size;
                    config.Cell("E13").Value=cover.Code;config.Cell("D13").Value=80;
                    config.Cell("E11").Value=$"{size.Width}x{size.Height}";
                    book.Save();
                }
                var raw=importer.Read(new("native-production",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
                var catalog=new CatalogReader().Read(raw,numeric).Value!;
                var policy=CalculationPolicy.Select("S2Rebar-1",true).Value!;
                var validated=new DBTool.Application.ManholeValidator(new NumericCellReader(),numeric).Validate(raw,catalog,
                    new("native-production",[ManholeIdentity.Create("native-production",raw.Profile,"G400-1")],CalculationScope.FullRebar,policy.PolicyId,true),policy);
                Assert.True(validated.Success,cover.Code+":"+string.Join(";",validated.Diagnostics.Select(d=>d.Code)));
                var spec=Assert.Single(validated.Value!.SelectedManholes);
                var geometry=new DBTool.Application.GeometryStageAdapter().Build(spec,policy);
                Assert.True(geometry.Success,cover.Code+":"+string.Join(";",geometry.Diagnostics.Select(d=>d.Code)));
                var placed=Assert.Single(geometry.Value!.PurchasedCovers);
                Assert.Equal(1,placed.Scale);
                Assert.NotNull(placed.SectionOutlines);Assert.NotNull(placed.NativeViewPlacement);
                var nominal=cover.BlockSet!.NominalOpening!.Size;
                Assert.Equal(nominal.Height+250,placed.SectionOutlines!.AA.MaxX-placed.SectionOutlines.AA.MinX,6);
                Assert.Equal(nominal.Width+250,placed.SectionOutlines.BB.MaxX-placed.SectionOutlines.BB.MinX,6);
                Assert.Equal(100,placed.SectionOutlines.AA.MaxY-placed.SectionOutlines.AA.MinY,6);
                Assert.Equal(cover.BlockSet.MeasuredAperture,placed.LocalClearProfile);
            }
        }
        finally {File.Delete(path);}
    }
    [Fact]
    public void EveryReviewedNativeCoverCarriesTheOwnerSectionRuleWithProvenance()
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("outline-rule",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entries=result.Value!.Covers.Values.Where(e=>e.BlockSet is not null).ToArray();
            Assert.Equal(19,entries.Length);
            foreach(var entry in entries)
            {
                var json=System.Text.Json.JsonSerializer.SerializeToElement(entry.BlockSet);
                Assert.True(json.TryGetProperty("SectionOutlineRule",out var rule));
                Assert.Equal(125,rule.GetProperty("SideExtensionMm").GetDouble());
                Assert.Equal(100,rule.GetProperty("DepthMm").GetDouble());
                Assert.NotEmpty(rule.GetProperty("Sources").EnumerateArray());
                Assert.Null(entry.OpeningContract); // Drawing convention does not approve material bearing.
            }
        }
        finally {File.Delete(path);}
    }
    [Theory]
    [InlineData("Nap4.TNM.LD.650x650","AA",6,-75,162.6)]
    [InlineData("Nap4.TNM.LD.650x650","BB",6,-75,162.6)]
    [InlineData("VIN.TNM.LD.820x390","AA",2,-50,142)]
    [InlineData("VIN.TNM.LD.820x390","BB",2,-50,142)]
    [InlineData("VIN.TNM.LD.390x820","AA",2,-50,142)]
    [InlineData("VIN.TNM.LD.390x820","BB",2,-50,142)]
    [InlineData("VIN.TNM.LD.700x700","AA",3,-80,207)]
    [InlineData("VIN.TNM.LD.700x700","BB",3,-80,207)]
    [InlineData("Nap2.TNM.LD.700x700","AA",2,-75,193.611111102073)]
    [InlineData("Nap2.TNM.LD.700x700","BB",2,-75,193.611111102073)]
    [InlineData("Nap3.TNM.LD.800x800","AA",2,-80,107)]
    [InlineData("Nap3.TNM.LD.800x800","BB",2,-80,107)]
    [InlineData("Nap7.TNM.LD.700x600","AA",2,-70,84)]
    [InlineData("Nap7.TNM.LD.700x600","BB",2,-70,84)]
    [InlineData("Nap8.TNM.LD.600x700","AA",2,-70,84)]
    [InlineData("Nap8.TNM.LD.600x700","BB",2,-70,84)]
    [InlineData("VIN.TNT.VH.700x700","AA",4,-63,125.723883453756)]
    [InlineData("VIN.TNT.VH.700x700","BB",4,-63,125.723883453756)]
    public void FrameContactKeepsMaterialIslandsWithoutBridgingTheGaps(string code,string view,int count,double elevation,double totalLength)
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("islands",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers[code];
            var contact=Assert.Single(entry.BlockSet!.ContactSections,s=>s.View==view);
            Assert.Equal(count,contact.Segments.Length);
            Assert.Equal(totalLength,contact.Segments.Sum(s=>s.End.X-s.Start.X),6);
            Assert.All(contact.Segments,s=>Assert.Equal(elevation,s.Start.Y,6));
            if(count==6)
            {
                var sorted=contact.Segments.OrderBy(s=>s.Start.X).ToArray();
                Assert.Equal(18.7,sorted[1].Start.X-sorted[0].End.X,6);
                Assert.Equal(18.7,sorted[5].Start.X-sorted[4].End.X,6);
            }
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }
    [Fact]
    public void Nap3TrueCircularThroatAgreesWithBothSectionFeetInsteadOfProductName800()
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("nap3-throat",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers["Nap3.TNM.LD.800x800"];
            var aperture=Assert.IsType<CircleProfile>(entry.BlockSet!.MeasuredAperture);
            Assert.Equal(299.5,aperture.RadiusMm);
            Assert.Equal(new Point2Mm(0,0),aperture.Center);
            foreach(var section in entry.BlockSet.ContactSections)
            {
                var left=Assert.Single(section.Segments,s=>s.End.X<0);
                var right=Assert.Single(section.Segments,s=>s.Start.X>0);
                Assert.Equal(599,right.Start.X-left.End.X,6);
            }
            Assert.Equal(2,entry.BlockSet.ContactSections.Length);
            Assert.All(entry.BlockSet.Views,v=>Assert.Equal(new Point2Mm(0,0),v.AlignmentPoint));
            Assert.Contains(entry.BlockSet.Sources,s=>s.RawText!.Contains("4A783"));
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }
    [Theory]
    [InlineData("Nap1.TNM.LD.700x700","AA",-450,-443,443,450)]
    [InlineData("Nap1.TNM.LD.700x700","BB",-450,-443,443,450)]
    [InlineData("Nap5.TNM.LD.800x380","AA",-265,-190,190,265)]
    [InlineData("Nap5.TNM.LD.800x380","BB",-480,-400,400,480)]
    [InlineData("Nap6.TNM.LD.380x800","AA",-480,-400,400,480)]
    [InlineData("Nap6.TNM.LD.380x800","BB",-265,-190,190,265)]
    public void MeasuredFrameContactsRetainActualSeparatedFootSegments(string code,string view,double a,double b,double c,double d)
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("contact",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers[code];
            var node=System.Text.Json.JsonSerializer.SerializeToElement(entry.BlockSet);
            Assert.True(node.TryGetProperty("ContactSections",out var sections));
            var section=Assert.Single(sections.EnumerateArray(),s=>s.GetProperty("View").GetString()==view);
            var segments=section.GetProperty("Segments").EnumerateArray().ToArray();
            Assert.Equal(2,segments.Length);
            Assert.Equal(a,segments[0].GetProperty("Start").GetProperty("X").GetDouble(),6);
            Assert.Equal(b,segments[0].GetProperty("End").GetProperty("X").GetDouble(),6);
            Assert.Equal(c,segments[1].GetProperty("Start").GetProperty("X").GetDouble(),6);
            Assert.Equal(d,segments[1].GetProperty("End").GetProperty("X").GetDouble(),6);
            Assert.All(segments,s=>Assert.Equal(-60,s.GetProperty("Start").GetProperty("Y").GetDouble(),6));
            Assert.NotEmpty(section.GetProperty("Sources").EnumerateArray());
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }
    [Fact]
    public void Native570By820HasItsOwnShiftedXAxisCentreAndRoundedContour()
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("rounded570",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers["VIN.TNM.LD.570x820"];
            var aperture=Assert.IsType<RoundedRectangleProfile>(entry.BlockSet!.MeasuredAperture);
            Assert.Equal(new Dimensions2Mm(570,820),aperture.Size);
            Assert.Equal(20,aperture.CornerRadiusMm);
            Assert.Equal(21.8224943950845,aperture.Center.X,10);
            Assert.Equal(0,aperture.Center.Y,10);
            Assert.Equal(aperture.Center,entry.BlockSet.Views.Single(v=>v.View=="MB").AlignmentPoint);
            Assert.Contains(entry.BlockSet.Sources,s=>s.RawText!.Contains("378F4"));
            Assert.Contains("PerViewApertureAnchor",entry.BlockSet.UnresolvedFields);
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }
    [Fact]
    public void VinRound700UsesDimensionedApertureRatherThanLowerFrameLip668()
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("round700",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers["VIN.TNM.LD.700x700"];
            var aperture=Assert.IsType<CircleProfile>(entry.BlockSet!.MeasuredAperture);
            Assert.Equal(350,aperture.RadiusMm);
            Assert.Equal(new Point2Mm(0,1.455192e-10),aperture.Center);
            foreach(var view in entry.BlockSet.Views)
            {
                Assert.Equal(view.View=="MB"?aperture.Center:new Point2Mm(0,0),view.AlignmentPoint);
                Assert.NotEmpty(view.AlignmentSources);
            }
            Assert.Contains(entry.BlockSet.Sources,s=>s.SourceKind==SourceKind.Dxf && s.RawText!.Contains("3F635") && s.RawText.Contains("3F638"));
            Assert.DoesNotContain("ClearAperture",entry.BlockSet.UnresolvedFields);
            Assert.DoesNotContain("PerViewApertureAnchor",entry.BlockSet.UnresolvedFields);
            Assert.Contains("SupportFootprint",entry.BlockSet.UnresolvedFields);
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }
    [Fact]
    public void OwnerConfirmed820By570ApertureUsesActualRoundedContourAndDisplacedCentre()
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("rounded",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers["VIN.TNM.LD.820x570"];
            var aperture=Assert.IsType<RoundedRectangleProfile>(entry.BlockSet!.MeasuredAperture);
            Assert.Equal(new Dimensions2Mm(820,570),aperture.Size);
            Assert.Equal(20,aperture.CornerRadiusMm);
            Assert.Equal(new Point2Mm(0,21.8224943950918),entry.BlockSet.Views.Single(v=>v.View=="MB").AlignmentPoint);
            Assert.Contains("PerViewApertureAnchor",entry.BlockSet.UnresolvedFields);
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }
    [Theory]
    [InlineData("Nap5.TNM.LD.800x380",800,380,true,"4EB21")]
    [InlineData("Nap6.TNM.LD.380x800",380,800,true,"4EB1E")]
    [InlineData("VIN.NBN.CX.800x800",800,800,false,"3F417")]
    [InlineData("VIN.TNT.CX.700x700",700,700,true,"3F50D")]
    public void RectangularAperturesUseDimensionedNativeAxesInsteadOfFrameBounds(string code,double width,double height,bool aligned,string evidence)
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("aperture",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers[code];
            var rectangle=Assert.IsType<RectangleProfile>(entry.BlockSet!.MeasuredAperture).Rectangle;
            Assert.Equal(new Rectangle2Mm(-width/2,-height/2,width/2,height/2),rectangle);
            if(aligned) Assert.All(entry.BlockSet.Views,v=>Assert.Equal(new Point2Mm(0,0),v.AlignmentPoint));
            else Assert.Contains("PerViewApertureAnchor",entry.BlockSet.UnresolvedFields);
            Assert.DoesNotContain("ClearAperture",entry.BlockSet.UnresolvedFields);
            Assert.Contains(entry.BlockSet.Sources,s=>s.SourceKind==SourceKind.Dxf && s.RawText!.Contains(evidence));
            Assert.Null(entry.OpeningContract);
        }
        finally {File.Delete(path);}
    }

    [Theory]
    [InlineData("Nap1.TNM.LD.700x700")]
    [InlineData("Nap4.TNM.LD.650x650")]
    public void SourcedRoundCoverHasIndependentAlignmentPointsInAllThreeViews(string code)
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("alignment",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers[code];
            var measured=Assert.IsType<CircleProfile>(entry.BlockSet!.MeasuredAperture);
            Assert.Equal(325,measured.RadiusMm);
            Assert.Equal(3,entry.BlockSet.Views.Length);
            foreach(var view in entry.BlockSet.Views)
            {
                Assert.Equal(new Point2Mm(0,0),view.AlignmentPoint);
                Assert.NotEmpty(view.AlignmentSources);
            }
            Assert.DoesNotContain("PerViewApertureAnchor",entry.BlockSet.UnresolvedFields);
            Assert.Contains("MinimumBearingRule",entry.BlockSet.UnresolvedFields);
            Assert.Null(entry.OpeningContract); // Measured alignment does not approve structural bearing.
        }
        finally {File.Delete(path);}
    }

    [Fact]
    public void MeasuredNativeBoundsKeepHingeAsymmetryInsteadOfInventedCentredBox()
    {
        var path=TestWorkbookFactory.CopySource(2);
        try
        {
            var raw=new WorkbookImporter().Read(new("bounds",path,Profile.GaThamNapRoi,"GaThamNapRoi")).Value!;
            var result=new CatalogReader().Read(raw,new("en-US",false,false));
            Assert.True(result.Success);
            var entry=result.Value!.Covers["VIN.NBN.CX.800x800"];
            Assert.Equal(-453,entry.LocalBounds!.MinX,6);
            Assert.Equal(471,entry.LocalBounds.MaxX,6);
            Assert.Equal(924,entry.PhysicalSize.Width,6);
            Assert.Equal(0,entry.InsertionOffset!.Value.X);
        }
        finally {File.Delete(path);}
    }
}

