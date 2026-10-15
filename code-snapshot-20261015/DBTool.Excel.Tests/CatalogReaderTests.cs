using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public class CatalogReaderTests
{
    [Fact] public void SchemaDocumentsCheckedCatalogColumns()
    {
        Assert.Equal("B",Vhg32Schema.PipeCatalogColumns["RoundDiameter"]);
        Assert.Equal("C",Vhg32Schema.PipeCatalogColumns["RoundThickness"]);
        Assert.Equal("D",Vhg32Schema.PipeCatalogColumns["BoxWidth"]);
        Assert.Equal("E",Vhg32Schema.PipeCatalogColumns["BoxHeight"]);
        Assert.Equal("F",Vhg32Schema.PipeCatalogColumns["BoxThickness"]);
        Assert.Equal("G",Vhg32Schema.PipeCatalogColumns["BoxChamfer"]);
        Assert.Equal("H",Vhg32Schema.PipeCatalogColumns["DoubleFlag"]);
        Assert.Equal("B",Vhg32Schema.DataCatalogColumns["AngleCode"]);
        Assert.Equal("C",Vhg32Schema.DataCatalogColumns["KgPerMetre"]);
        Assert.Equal("D",Vhg32Schema.DataCatalogColumns["HatchName"]);
        Assert.Equal("E",Vhg32Schema.DataCatalogColumns["HatchScale"]);
    }

    private static OperationResult<CatalogSet> Read(Action<XLWorkbook> edit)
    {
        string path = TestWorkbookFactory.Create(edit: edit);
        var raw = new WorkbookImporter().Read(new WorkbookReadRequest("S1",path,Profile.GaThamLienKhoi,"GaThamLienKhoi"));
        Assert.True(raw.Success);
        return new CatalogReader().Read(raw.Value!,new NumericPolicy("en-US",false,false));
    }

    [Fact] public void RoundAndBoxInSameRowRetainDecimalAndUnknownMiddleWall()
    {
        var result = Read(w =>
        {
            var c=w.Worksheet("CF.Cong"); c.Cell("B4").Value=400.5; c.Cell("C4").Value=55;
            c.Cell("D4").Value=1000; c.Cell("E4").Value=800; c.Cell("F4").Value=80;
            c.Cell("G4").Value=50; c.Cell("H4").Value="Có";
        });
        Assert.True(result.Success);
        Assert.Equal(400.5,result.Value!.Pipes["D400.5"].InnerDiameterMm);
        Assert.Equal("D400.5",result.Value.Aliases["400.5"]);
        Assert.Equal(1000,result.Value.Pipes["1000x800"].InnerWidthMm);
        Assert.Equal(800,result.Value.Pipes["1000x800"].InnerHeightMm);
        Assert.Equal(PipeKind.DoubleBox,result.Value.Pipes["1000x800"].Kind);
        Assert.Null(result.Value.Pipes["1000x800"].MiddleWallThicknessMm);
    }

    [Fact] public void Round400WithWall55HasOuterDiameter510()
    {
        var result=Read(w => { var c=w.Worksheet("CF.Cong"); c.Cell("B4").Value=400; c.Cell("C4").Value=55; });
        Assert.True(result.Success);
        var pipe=result.Value!.Pipes["D400"];
        Assert.Equal(510,pipe.InnerDiameterMm!.Value+2*pipe.WallThicknessMm);
    }

    [Fact] public void FractionalDecimalCodeUsesHumanCanonicalDigits()
    {
        var result=Read(w => { var c=w.Worksheet("CF.Cong"); c.Cell("B4").Value=400.1; c.Cell("C4").Value=55; });
        Assert.True(result.Success);
        Assert.Contains("D400.1",result.Value!.Pipes.Keys);
        Assert.Equal("D400.1",result.Value.Aliases["400.1"]);
    }

    [Fact] public void DuplicateCanonicalCodeBlocksInsteadOfOverwriting()
    {
        var result=Read(w => { var c=w.Worksheet("CF.Cong"); c.Cell("B4").Value=400; c.Cell("C4").Value=55;
            c.Cell("B5").Value=400; c.Cell("C5").Value=55; });
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CATALOG_DUPLICATE" && d.Source?.CellAddress=="B5");
    }

    [Fact] public void NegativeThicknessBlocksWithSource()
    {
        var result=Read(w => { var c=w.Worksheet("CF.Cong"); c.Cell("B4").Value=400; c.Cell("C4").Value=-1; });
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CATALOG_DIMENSION_INVALID" && d.Source?.CellAddress=="C4");
    }

    [Fact] public void UnknownDoubleBoxFlagDoesNotBecomeSingleBox()
    {
        var result=Read(w => { var c=w.Worksheet("CF.Cong"); c.Cell("D4").Value=1000; c.Cell("E4").Value=800;
            c.Cell("F4").Value=80; c.Cell("G4").Value=50; c.Cell("H4").Value="maybe"; });
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CATALOG_FLAG_INVALID" && d.Source?.CellAddress=="H4");
    }

    [Fact] public void CatalogContinuesPastBlankRowsAndOneHundred()
    {
        var result=Read(w => { var c=w.Worksheet("CF.Cong"); c.Cell("B101").Value=900; c.Cell("C101").Value=90; });
        Assert.True(result.Success); Assert.Equal(900,result.Value!.Pipes["D900"].InnerDiameterMm);
    }

    [Fact] public void DataCatalogUsesVerifiedColumnsAndSkipsKhong()
    {
        var result=Read(w => { var d=w.Worksheet("CF.Data"); d.Cell("B4").Value="Không"; d.Cell("D4").Value="Không";
            d.Cell("B6").Value="V40x40x4"; d.Cell("C6").Value=2.42;
            d.Cell("D6").Value="ANSI31"; d.Cell("E6").Value=10; });
        Assert.True(result.Success);
        Assert.Equal(2.42,result.Value!.Angles["V40x40x4"].KgPerMetre);
        Assert.Equal(10,result.Value.Hatches["ANSI31"].Scale);
        Assert.DoesNotContain("Không",result.Value.Angles.Keys);
    }

    [Fact] public void WeightWithoutAngleCodeIsBlocked()
    {
        var result=Read(w => w.Worksheet("CF.Data").Cell("C4").Value=2.42);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CATALOG_CODE_MISSING" && d.Source?.CellAddress=="C4");
    }

    [Fact] public void CatalogErrorCellIsNotSilentlySkipped()
    {
        var source=new SourceRef(SourceKind.WorkbookCell,"fixture.xlsx",new string('a',64),"CF.Cong","B4",null,"#VALUE!");
        var row=new RawManholeRow(4,new Dictionary<string,RawCell> { ["B"]=new(RawCellKind.Error,"#VALUE!",null,null,null,null,source) });
        var raw=new RawProjectInput("S1",new string('a',64),Profile.GaThamLienKhoi,"GaThamLienKhoi",
            Array.Empty<RawManholeRow>(),new Dictionary<string,RawCell>(),
            new Dictionary<string,IEnumerable<RawManholeRow>> { ["CF.Cong"]=new[]{row},["CF.Data"]=Array.Empty<RawManholeRow>() },
            Array.Empty<Diagnostic>());
        var result=new CatalogReader().Read(raw,new NumericPolicy("en-US",false,false));
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CELL_ERROR" && d.Source?.CellAddress=="B4");
    }

    [Fact] public void FormulaDiameterUsesCacheOnlyWithOptInAndReview()
    {
        var source=new SourceRef(SourceKind.WorkbookCell,"fixture.xlsx",new string('a',64),"CF.Cong","B4",null,"=200+200");
        var wallSource=new SourceRef(SourceKind.WorkbookCell,"fixture.xlsx",new string('a',64),"CF.Cong","C4",null,"55");
        var row=new RawManholeRow(4,new Dictionary<string,RawCell>
        {
            ["B"]=new(RawCellKind.Formula,null,null,null,"200+200","400",source),
            ["C"]=new(RawCellKind.Number,null,55,null,null,null,wallSource)
        });
        var raw=new RawProjectInput("S1",new string('a',64),Profile.GaThamLienKhoi,"GaThamLienKhoi",
            Array.Empty<RawManholeRow>(),new Dictionary<string,RawCell>(),
            new Dictionary<string,IEnumerable<RawManholeRow>> { ["CF.Cong"]=new[]{row},["CF.Data"]=Array.Empty<RawManholeRow>() },
            Array.Empty<Diagnostic>());
        var rejected=new CatalogReader().Read(raw,new NumericPolicy("en-US",false,false));
        Assert.False(rejected.Success); Assert.Null(rejected.Value);
        Assert.Contains(rejected.Diagnostics,d=>d.Code=="FORMULA_VALUE_UNAVAILABLE" && d.Source?.CellAddress=="B4");
        var accepted=new CatalogReader().Read(raw,new NumericPolicy("en-US",false,true));
        Assert.True(accepted.Success); Assert.Contains("D400",accepted.Value!.Pipes.Keys);
        Assert.Contains(accepted.Diagnostics,d=>d.Code=="FORMULA_CACHE_UNVERIFIED" && d.Severity==DiagnosticSeverity.Review);
    }

    [Fact] public void FormulaCatalogCodeCannotDisappearAsBlank()
    {
        var source=new SourceRef(SourceKind.WorkbookCell,"fixture.xlsx",new string('a',64),"CF.Data","B4",null,"=A4");
        var row=new RawManholeRow(4,new Dictionary<string,RawCell>
        { ["B"]=new(RawCellKind.Formula,null,null,null,"A4","V40x40x4",source) });
        var raw=new RawProjectInput("S1",new string('a',64),Profile.GaThamLienKhoi,"GaThamLienKhoi",
            Array.Empty<RawManholeRow>(),new Dictionary<string,RawCell>(),
            new Dictionary<string,IEnumerable<RawManholeRow>> { ["CF.Cong"]=Array.Empty<RawManholeRow>(),["CF.Data"]=new[]{row} },
            Array.Empty<Diagnostic>());
        var result=new CatalogReader().Read(raw,new NumericPolicy("en-US",false,true));
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CATALOG_CODE_FORMULA_UNSUPPORTED" && d.Source?.CellAddress=="B4");
    }

    [Fact] public void ActualSourceCatalogMatchesCheckedHashAndHeaderMap()
    {
        string root=TestWorkbookFactory.WorkspaceRoot();
        string source=Directory.GetFiles(Path.Combine(root,"VHGpro 3.2","Demo","Excel"),"1.0.*.xlsx").Single();
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(source))).ToLowerInvariant();
        Assert.Equal("50a4cbf80521995c0ac8ed4a8fcf870d3b545aa13eeabf4334d3abba8bc30ead",hash);
        using (var book=new XLWorkbook(source))
        {
            Assert.Equal("Loại thép",book.Worksheet("CF.Data").Cell("B3").GetString());
            Assert.Equal("KL riêng",book.Worksheet("CF.Data").Cell("C3").GetString());
            Assert.Equal("Mẫu hatch",book.Worksheet("CF.Data").Cell("D3").GetString());
            Assert.Equal("Tỷ lệ hatch",book.Worksheet("CF.Data").Cell("E3").GetString());
        }
        var raw=new WorkbookImporter().Read(new WorkbookReadRequest("S1",source,Profile.GaThamLienKhoi,"GaThamLienKhoi"));
        Assert.True(raw.Success);
        var result=new CatalogReader().Read(raw.Value!,new NumericPolicy("en-US",false,false));
        Assert.True(result.Success);
        Assert.Equal(510,result.Value!.Pipes["D400"].InnerDiameterMm!.Value+2*result.Value.Pipes["D400"].WallThicknessMm);
        Assert.Equal(2.42,result.Value.Angles["V40x40x4"].KgPerMetre);
        Assert.Equal(10,result.Value.Hatches["ANSI31"].Scale);
    }
}
