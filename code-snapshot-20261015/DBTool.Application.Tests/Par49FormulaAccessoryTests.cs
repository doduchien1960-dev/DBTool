using DBTool.Domain;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class Par49FormulaAccessoryTests
{
    private const string ValidHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static SourceRef Source(string cell = "C9") =>
        new(SourceKind.WorkbookCell, "test.xlsx", ValidHash, "CF.Ga", cell, null, "formula");

    [Fact]
    public void FormulaCellWithCachedValue_ActivatesAccessory()
    {
        var config = new Dictionary<string, RawCell>
        {
            ["HasDowel"] = new(RawCellKind.Formula, null, null, null, "IF(TRUE,\"Có\",\"Không\")", "Có", Source("C9")),
            ["DowelSpec"] = new(RawCellKind.Formula, null, null, null, "\"D18A400\"", "D18A400", Source("D9")),
            ["DowelLength"] = new(RawCellKind.Formula, null, null, null, "200*2", "400", Source("E9")),
            ["DowelHoleDia"] = new(RawCellKind.Formula, null, null, null, "20+5", "25", Source("F9")),
        };

        var raw = new RawProjectInput("P1", ValidHash, Profile.GaThamNapRoi, "Test", [], config, new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
        var result = AccessoryInputMapper.Parse(raw, "M1", new NumericPolicy("en-US", false, true));

        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Value!.Dowel);
        Assert.Equal(18, result.Value!.Dowel!.DiameterMm);
        Assert.Equal(400, result.Value!.Dowel!.LengthMm);
        Assert.Equal(25, result.Value!.DowelHoleDiameterMm);
    }

    [Fact]
    public void FormulaCellWithMissingCache_ReportsBlockDiagnostic()
    {
        var config = new Dictionary<string, RawCell>
        {
            ["HasDowel"] = new(RawCellKind.Formula, null, null, null, "IF(TRUE,\"Có\",\"Không\")", null, Source("C9")),
        };

        var raw = new RawProjectInput("P1", ValidHash, Profile.GaThamNapRoi, "Test", [], config, new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
        var result = AccessoryInputMapper.Parse(raw, "M1", new NumericPolicy("en-US", false, true));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "ACCESSORY_INPUT_INVALID" && d.Message.Contains("cached"));
    }

    [Fact]
    public void CellWithError_ReportsBlockDiagnostic()
    {
        var config = new Dictionary<string, RawCell>
        {
            ["HasDowel"] = new(RawCellKind.Error, "#VALUE!", null, null, null, null, Source("C9")),
        };

        var raw = new RawProjectInput("P1", ValidHash, Profile.GaThamNapRoi, "Test", [], config, new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
        var result = AccessoryInputMapper.Parse(raw, "M1", new NumericPolicy("en-US", false, true));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "ACCESSORY_INPUT_INVALID" && d.Message.Contains("error"));
    }

    [Fact]
    public void BooleanCell_ActivatesAccessoryWhenTrue()
    {
        var config = new Dictionary<string, RawCell>
        {
            ["HasDowel"] = new(RawCellKind.Boolean, null, null, true, null, null, Source("C9")),
            ["DowelSpec"] = new(RawCellKind.Text, "D18A400", null, null, null, null, Source("D9")),
            ["DowelLength"] = new(RawCellKind.Number, null, 400, null, null, null, Source("E9")),
            ["DowelHoleDia"] = new(RawCellKind.Number, null, 25, null, null, null, Source("F9")),
        };

        var raw = new RawProjectInput("P1", ValidHash, Profile.GaThamNapRoi, "Test", [], config, new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
        var result = AccessoryInputMapper.Parse(raw, "M1", new NumericPolicy("en-US", false, true));

        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Value!.Dowel);
        Assert.Equal(18, result.Value!.Dowel!.DiameterMm);
    }
}
