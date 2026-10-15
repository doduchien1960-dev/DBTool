using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using System.Security.Cryptography;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class SourceSyncTests
{
    [Fact]
    public void SelectedBUpdatesItsOwnRowWhileAStaysNumericAndBecomesStale()
    {
        var (source, directory) = MakeWorkbook();
        try
        {
            string before = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            var row = new SourceSyncRow("ID-B","B6",Profile.GaThamLienKhoi,"B",CalculationScope.Full,
                Values(baseD10:100d/162,baseDOver10:144d/162));
            var request = new SourceSyncRequest(source,directory,["ID-B"],[row],
                SyncColumnContract.Vhg32(),CalculationScope.Full,Profile.GaThamLienKhoi,"run1");
            var result = SourceWorkbookSynchronizer.SyncToCopy(request);
            Assert.True(result.Success,string.Join("; ",result.Diagnostics.Select(d=>$"{d.Code}: {d.Message}")));
            Assert.NotEqual(source,result.Value!.Path);
            Assert.Equal(before,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
            using var copy = new XLWorkbook(result.Value.Path);
            var sheet = copy.Worksheet("Thep");
            Assert.Equal(123, sheet.Cell("C5").GetDouble());
            Assert.Equal(100d/162, sheet.Cell("C6").GetDouble(), 9);
            Assert.Equal(144d/162, sheet.Cell("D6").GetDouble(), 9);
            Assert.Equal(244d/162, sheet.Cell("P6").GetDouble(), 9);
            var status = copy.Worksheet("DBTool.SyncStatus");
            Assert.Equal("OverallTotalsNotValidated",status.Cell("A1").GetString());
            Assert.Equal("StaleNotRecalculated",status.Cell("E3").GetString());
            Assert.Equal("Calculated",status.Cell("E4").GetString());
            Assert.Equal("ID-B",status.Cell("B4").GetString());
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void PartialScopeDoesNotCreateCopy()
    {
        var (source,directory)=MakeWorkbook();
        try
        {
            var row = new SourceSyncRow("ID-B","B6",Profile.GaThamLienKhoi,"B",CalculationScope.BaseRebarOnly,
                Values(100d/162,0));
            var result = SourceWorkbookSynchronizer.SyncToCopy(new SourceSyncRequest(source,directory,["ID-B"],[row],
                SyncColumnContract.Vhg32(),CalculationScope.BaseRebarOnly,Profile.GaThamLienKhoi,"partial"));
            Assert.False(result.Success);
            Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics,d=>d.Code=="SOURCE_SYNC_SCOPE_INCOMPLETE" && d.Severity==DiagnosticSeverity.Block);
            Assert.Single(Directory.GetFiles(directory,"*.xlsx"));
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void DuplicateSourceNamesAndWrongHeaderBlockWithoutCopy()
    {
        var (source,directory)=MakeWorkbook();
        try
        {
            using (var book = new XLWorkbook(source)) { book.Worksheet("Thep").Cell("B6").Value="A"; book.Save(); }
            var row = new SourceSyncRow("ID-A","B5",Profile.GaThamLienKhoi,"A",CalculationScope.Full,Values(1,0));
            var request = new SourceSyncRequest(source,directory,["ID-A"],[row],SyncColumnContract.Vhg32(),CalculationScope.Full,Profile.GaThamLienKhoi,"duplicate");
            var ambiguous = SourceWorkbookSynchronizer.SyncToCopy(request);
            Assert.False(ambiguous.Success);
            Assert.Null(ambiguous.Value);
            Assert.Contains(ambiguous.Diagnostics,d=>d.Code=="SYNC_ID_AMBIGUOUS");
            using (var book = new XLWorkbook(source)) { book.Worksheet("Thep").Cell("C3").Value="wrong"; book.Save(); }
            var schema = SourceWorkbookSynchronizer.SyncToCopy(request);
            Assert.Contains(schema.Diagnostics,d=>d.Code=="SYNC_SCHEMA_UNVERIFIED");
            Assert.Single(Directory.GetFiles(directory,"*.xlsx"));
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void SourcePathAsDestinationBlocksWithoutChangingSource()
    {
        var (source,directory)=MakeWorkbook();
        try
        {
            string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            var row=new SourceSyncRow("ID-B","B6",Profile.GaThamLienKhoi,"B",CalculationScope.Full,Values(1,0));
            var result=SourceWorkbookSynchronizer.SyncToCopy(new SourceSyncRequest(source,source,["ID-B"],[row],
                SyncColumnContract.Vhg32(),CalculationScope.Full,Profile.GaThamLienKhoi,"same"));
            Assert.False(result.Success);
            Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics,d=>d.Code=="SOURCE_EQUALS_DESTINATION");
            Assert.Equal(before,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void ThreeSourceProfilesShareVerifiedThepHeaderWithoutModification()
    {
        string directory=Path.Combine(TestWorkbookFactory.WorkspaceRoot(),"VHGpro 3.2");
        var sources=Directory.GetFiles(directory,"*.xlsx",SearchOption.TopDirectoryOnly)
            .Where(path=>Path.GetFileName(path).StartsWith("1.0.") ||
                Path.GetFileName(path).StartsWith("2.0.") || Path.GetFileName(path).StartsWith("3.0.")).ToArray();
        Assert.Equal(3,sources.Length);
        foreach(string source in sources)
        {
            string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            using var book=new XLWorkbook(source);
            var sheet=book.Worksheet("Thep");
            Assert.Equal("TT",sheet.Cell("A2").GetString());
            Assert.Equal("Hố ga",sheet.Cell("B2").GetString());
            Assert.Equal("Đáy",sheet.Cell("C2").GetString());
            Assert.Equal("D ≤ 10",sheet.Cell("C3").GetString());
            Assert.Equal("D > 10",sheet.Cell("D3").GetString());
            Assert.Equal("Thép hình",sheet.Cell("O2").GetString());
            Assert.Equal("Tổng",sheet.Cell("P2").GetString());
            Assert.Equal(before,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
        }
    }

    [Theory]
    [InlineData("1.0.*.xlsx",Profile.GaThamLienKhoi)]
    [InlineData("2.0.*.xlsx",Profile.GaThamNapRoi)]
    [InlineData("3.0.*.xlsx",Profile.GaThamThuNapRoi)]
    public void RealWorkbookCachedNameMapsSelectedRowOnCopyForAllProfiles(string sourcePattern,Profile profile)
    {
        string source=Directory.GetFiles(Path.Combine(TestWorkbookFactory.WorkspaceRoot(),"VHGpro 3.2"),sourcePattern).Single();
        string directory=Path.Combine(Path.GetTempPath(),"dbtool-real-sync-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source)));
            var row=new SourceSyncRow($"S1/{profile}/G400-1","B5",profile,
                "G400-1",CalculationScope.Full,Values(100d/162,144d/162));
            var result=SourceWorkbookSynchronizer.SyncToCopy(new SourceSyncRequest(source,directory,
                [row.ManholeId],[row],SyncColumnContract.Vhg32(),CalculationScope.Full,profile,"readback"));
            Assert.True(result.Success,string.Join("; ",result.Diagnostics.Select(d=>$"{d.Code}: {d.Message}")));
            Assert.Equal(before,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source))));
            using var copy=new XLWorkbook(result.Value!.Path);
            Assert.Equal(100d/162,copy.Worksheet("Thep").Cell("C5").GetDouble(),9);
            Assert.Equal("StaleNotRecalculated",copy.Worksheet("DBTool.SyncStatus").Cell("E4").GetString());
            Assert.Equal(RecalculationStatus.NotRun,result.Value.RecalculationStatus);
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void CorruptSourceReturnsBlockInsteadOfThrowing()
    {
        string directory=Path.Combine(Path.GetTempPath(),"dbtool-corrupt-sync-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string source=Path.Combine(directory,"bad.xlsx");
            File.WriteAllBytes(source,"not an xlsx"u8.ToArray());
            var row=new SourceSyncRow("ID-B","B5",Profile.GaThamLienKhoi,"B",CalculationScope.Full,Values(1,0));
            var result=SourceWorkbookSynchronizer.SyncToCopy(new SourceSyncRequest(source,directory,["ID-B"],[row],
                SyncColumnContract.Vhg32(),CalculationScope.Full,Profile.GaThamLienKhoi,"bad"));
            Assert.False(result.Success);
            Assert.Null(result.Value);
            Assert.Contains(result.Diagnostics,d=>d.Severity==DiagnosticSeverity.Block && d.Scope==DiagnosticScope.Global);
        }
        finally { Directory.Delete(directory,true); }
    }

    [Fact]
    public void ExistingMetadataIdDoesNotFollowWrongRowAfterNamesMove()
    {
        var (source,directory)=MakeWorkbook();
        try
        {
            var firstRow=new SourceSyncRow("ID-B","B6",Profile.GaThamLienKhoi,"B",CalculationScope.Full,Values(1,0));
            var first=SourceWorkbookSynchronizer.SyncToCopy(new SourceSyncRequest(source,directory,["ID-B"],[firstRow],
                SyncColumnContract.Vhg32(),CalculationScope.Full,Profile.GaThamLienKhoi,"first"));
            Assert.True(first.Success);
            using(var book=new XLWorkbook(first.Value!.Path))
            {
                var sheet=book.Worksheet("Thep");
                sheet.Cell("B5").Value="B";
                sheet.Cell("B6").Value="A";
                book.Save();
            }
            var secondRow=new SourceSyncRow("ID-B","B5",Profile.GaThamLienKhoi,"B",CalculationScope.Full,Values(777,0));
            var second=SourceWorkbookSynchronizer.SyncToCopy(new SourceSyncRequest(first.Value!.Path,directory,["ID-B"],[secondRow],
                SyncColumnContract.Vhg32(),CalculationScope.Full,Profile.GaThamLienKhoi,"second"));
            Assert.True(second.Success);
            using var copy=new XLWorkbook(second.Value!.Path);
            Assert.Equal(777,copy.Worksheet("Thep").Cell("C5").GetDouble());
            Assert.Equal("ID-B",copy.Worksheet("DBTool.SyncStatus").Cell("B3").GetString());
        }
        finally { Directory.Delete(directory,true); }
    }

    private static IEnumerable<SyncSummaryValue> Values(double baseD10,double baseDOver10) =>
        Enum.GetValues<SyncBucket>().Select(bucket => new SyncSummaryValue(bucket,
            bucket == SyncBucket.BaseD10 ? baseD10 : bucket == SyncBucket.BaseDOver10 ? baseDOver10 : 0,
            CalculationStatus.Calculated));

    private static (string Source,string Directory) MakeWorkbook()
    {
        string directory=Path.Combine(Path.GetTempPath(),"dbtool-sync-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source=Path.Combine(directory,"source.xlsx");
        using var book=new XLWorkbook();
        var sheet=book.AddWorksheet("Thep");
        sheet.Cell("A2").Value="TT"; sheet.Cell("B2").Value="Hố ga";
        string[] groups=["Đáy","Tường","Trần","Cổ","Cửa thu","Nắp"];
        for(int i=0;i<6;i++) {
            int col=3+2*i;
            sheet.Cell(2,col).Value=groups[i];
            sheet.Cell(3,col).Value="D ≤ 10";
            sheet.Cell(3,col+1).Value="D > 10";
        }
        sheet.Cell("O2").Value="Thép hình"; sheet.Cell("P2").Value="Tổng";
        sheet.Cell("B5").Value="A"; sheet.Cell("B6").Value="B";
        sheet.Cell("C5").Value=123; sheet.Cell("C6").Value=456;
        book.SaveAs(source);
        return (source,directory);
    }
}
