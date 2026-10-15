using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class Phase6CoverCatalogTests
{
    [Fact]
    public void ActualVisitCatchCoverHasMeasuredNativeBoundaryAndProvenance()
    {
        var path = TestWorkbookFactory.CopySource(3, _ => { });
        try
        {
            var raw = new WorkbookImporter().Read(new("P6", path, Profile.GaThamThuNapRoi, "GaThamThuNapRoi"));
            Assert.True(raw.Success);
            var result = new CatalogReader().Read(raw.Value!, new NumericPolicy("en-US", false, false));
            Assert.True(result.Success);
            Assert.True(result.Value!.Covers.TryGetValue("VIN.TNT.VH.700x700", out var cover));
            Assert.Equal(800, cover!.PhysicalSize.Width);
            Assert.Equal(800, cover.PhysicalSize.Height);
            Assert.Equal(-400, cover.LocalBounds!.MinX);
            Assert.Equal(400, cover.LocalBounds.MaxY);
            Assert.Equal(CoverPlacementDatum.NeckTop, cover.PlacementDatum);
            Assert.Contains(cover.Sources, s => s.SourceKind == SourceKind.Dwg &&
                s.RelativePath == "VHGpro 3.2/Template/VHG-Template.dwg" &&
                s.Sha256 == "ABF4B2A60EB51DE311401F7C92CF159DD578D5109C113AA10045308DDFB6C234");
            Assert.Contains(cover.Sources, s => s.SourceKind == SourceKind.Dxf &&
                s.RelativePath == "docs/audit-20261011/cover-catalog/template-current.dxf" &&
                s.Sha256 == "5D98CFBE4AE9EBFA10C78FBB606CDB822373581ED8E19E131556396E2426CF79");
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("Nap1.TNM.LD.700x700", 900, 900)]
    [InlineData("Nap2.TNM.LD.700x700", 850, 850)]
    [InlineData("Nap3.TNM.LD.800x800", 900, 900)]
    [InlineData("Nap4.TNM.LD.650x650", 850, 850)]
    [InlineData("Nap5.TNM.LD.800x380", 960, 530)]
    [InlineData("Nap6.TNM.LD.380x800", 530, 960)]
    [InlineData("Nap7.TNM.LD.700x600", 750, 650)]
    [InlineData("Nap8.TNM.LD.600x700", 650, 750)]
    // Current DWG includes the asymmetric hinge: X=-453..471 (924), not the old estimated 921.
    [InlineData("VIN.NBN.CX.800x800", 924, 860)]
    [InlineData("VIN.TNM.CX.200x500", 300, 600)]
    [InlineData("VIN.TNM.CX.500x200", 600, 300)]
    [InlineData("VIN.TNM.CX.500x500", 600, 600)]
    [InlineData("VIN.TNM.LD.390x820", 530, 960)]
    [InlineData("VIN.TNM.LD.570x820", 745, 1050)]
    [InlineData("VIN.TNM.LD.700x700", 915.1, 915.22)]
    [InlineData("VIN.TNM.LD.820x390", 960, 530)]
    [InlineData("VIN.TNM.LD.820x570", 1050, 745)]
    [InlineData("VIN.TNT.CX.700x700", 720, 720)]
    [InlineData("VIN.TNT.VH.700x700", 800, 800)]
    public void AllNineteenNativeCoversAreRegisteredWithCorrectBounds(string code, double expectedWidth, double expectedLength)
    {
        var path = TestWorkbookFactory.CopySource(2, _ => { });
        try
        {
            var raw = new WorkbookImporter().Read(new("P6", path, Profile.GaThamNapRoi, "GaThamNapRoi"));
            Assert.True(raw.Success);
            var result = new CatalogReader().Read(raw.Value!, new NumericPolicy("en-US", false, false));
            Assert.True(result.Success);
            Assert.True(result.Value!.Covers.TryGetValue(code, out var entry), $"Cover '{code}' should be registered");
            Assert.Equal(expectedWidth, entry!.PhysicalSize.Width, 2);
            Assert.Equal(expectedLength, entry.PhysicalSize.Height, 2);
            Assert.Equal(CoverPlacementDatum.NeckTop, entry.PlacementDatum);
        }
        finally { File.Delete(path); }
    }
}
