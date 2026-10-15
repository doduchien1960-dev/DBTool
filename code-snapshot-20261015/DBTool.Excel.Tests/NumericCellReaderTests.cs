using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public class NumericCellReaderTests
{
    private static readonly NumericCellReader Reader = new();
    private static SourceRef Source(string raw) => new(SourceKind.WorkbookCell,"fixture.xlsx",new string('a',64),"CF.Ga","C3",null,raw);
    private static RawCell Text(string value) => new(RawCellKind.Text,value,null,null,null,null,Source(value));
    private static RawCell Number(double value) => new(RawCellKind.Number,null,value,null,null,null,Source(value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact] public void NativeNumberAndCultureSpecificText()
    {
        Assert.Equal(30.5,Reader.ReadNumber(Number(30.5),new NumericPolicy("vi-VN",false,false)).Value!.Value);
        Assert.Equal(30.5,Reader.ReadNumber(Text(" 30,5 "),new NumericPolicy("vi-VN",false,false)).Value!.Value);
    }

    [Theory]
    [InlineData("30,5","en-US","NUMBER_FORMAT")]
    [InlineData("1.234","vi-VN","NUMBER_FORMAT")]
    [InlineData("NaN","en-US","NUMBER_NOT_FINITE")]
    [InlineData("Infinity","en-US","NUMBER_NOT_FINITE")]
    public void RejectsAmbiguousOrNonfiniteText(string value,string culture,string code)
    {
        var result = Reader.ReadNumber(Text(value),new NumericPolicy(culture,false,false));
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Equal(code,Assert.Single(result.Diagnostics).Code);
    }

    [Fact] public void BlankIsNotZeroAndErrorCellIsBlocked()
    {
        var blank = new RawCell(RawCellKind.Blank,null,null,null,null,null,Source(""));
        Assert.Equal("NUMBER_MISSING",Assert.Single(Reader.ReadNumber(blank,new NumericPolicy("en-US",false,false)).Diagnostics).Code);
        Assert.Equal(0,Reader.ReadNumber(Number(0),new NumericPolicy("en-US",false,false)).Value!.Value);
        var error = new RawCell(RawCellKind.Error,"#VALUE!",null,null,null,null,Source("#VALUE!"));
        Assert.Equal("CELL_ERROR",Assert.Single(Reader.ReadNumber(error,new NumericPolicy("en-US",false,false)).Diagnostics).Code);
    }

    [Fact] public void FormulaRequiresOptInAndMarksCacheUnverified()
    {
        var formula = new RawCell(RawCellKind.Formula,null,null,null,"1+2","3",Source("=1+2"));
        Assert.Equal("FORMULA_VALUE_UNAVAILABLE",Assert.Single(Reader.ReadNumber(formula,new NumericPolicy("en-US",false,false)).Diagnostics).Code);
        var accepted = Reader.ReadNumber(formula,new NumericPolicy("en-US",false,true));
        Assert.Equal(3,accepted.Value!.Value);
        Assert.Equal("FORMULA_CACHE_UNVERIFIED",Assert.Single(accepted.Diagnostics).Code);
        Assert.Equal(DiagnosticSeverity.Review,accepted.Diagnostics[0].Severity);
        var missing = new RawCell(RawCellKind.Formula,null,null,null,"1+2",null,Source("=1+2"));
        Assert.Equal("FORMULA_VALUE_UNAVAILABLE",Assert.Single(Reader.ReadNumber(missing,new NumericPolicy("en-US",false,true)).Diagnostics).Code);
    }

    [Theory]
    [InlineData("1000x800")]
    [InlineData("1000X800")]
    [InlineData("1000×800")]
    public void DimensionsRequireTwoPositiveNumbers(string text)
    {
        var result = Reader.ReadDimensions(text,new NumericPolicy("en-US",false,false));
        Assert.True(result.Success); Assert.Equal(1000,result.Value!.Width); Assert.Equal(800,result.Value.Height);
    }
}
