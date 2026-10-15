using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class AccessoryPolicyTests
{
    private static RawProjectInput Raw(string cache)
    {
        var raw = ManholeValidatorTests.Raw();
        var source = ManholeValidatorTests.Cell(cache, "C9", "CF.Ga").Source;
        return new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet, raw.Rows,
            raw.ConfigCells.SetItem("HasDowel", new(RawCellKind.Formula, null, null, null, "test()", cache, source)),
            new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
    }

    [Fact]
    public void CachedDisabledAccessoryStillRecordsUnverifiedSource()
    {
        var result = AccessoryInputMapper.Parse(Raw("Không"), "M1", new NumericPolicy("en-US", false, true));
        Assert.True(result.Success);
        Assert.Null(result.Value!.Dowel);
        Assert.Contains(result.Diagnostics, d => d.Code == "FORMULA_CACHE_UNVERIFIED" &&
            d.Severity == DiagnosticSeverity.Review && d.Source?.CellAddress == "C9");
    }

    [Fact]
    public void FormulaErrorCacheCannotBecomeDisabledOrEnabledAccessory()
    {
        var result = AccessoryInputMapper.Parse(Raw("#VALUE!"), "M1", new NumericPolicy("en-US", false, true));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "CELL_ERROR" &&
            d.Severity == DiagnosticSeverity.Block && d.Source?.CellAddress == "C9");
    }

    [Fact]
    public void ValidatorTextFormulaCannotBypassCachePolicy()
    {
        var raw = ManholeValidatorTests.Raw();
        var source = raw.ConfigCells["Distribution"].Source;
        raw = new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet, raw.Rows,
            raw.ConfigCells.SetItem("Distribution", new(RawCellKind.Formula, null, null, null, "test()", "Chia đều", source)),
            new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
        var result = ManholeValidatorTests.Validate(raw);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "FORMULA_VALUE_UNAVAILABLE" &&
            d.Severity == DiagnosticSeverity.Block && d.Source?.CellAddress == "K19");
    }

    [Theory]
    [InlineData("Không")]
    [InlineData("Có")]
    [InlineData("0")]
    public void DisabledAndEnabledFormulaBothRequirePolicyOptIn(string cached)
    {
        var result = AccessoryInputMapper.Parse(Raw(cached), "M1", new NumericPolicy("en-US", false, false));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "FORMULA_VALUE_UNAVAILABLE" &&
            d.Severity == DiagnosticSeverity.Block && d.Source?.CellAddress == "C9");
    }

    [Fact]
    public void AccessoryNumericTextUsesDeclaredCultureAndRejectsThousands()
    {
        var raw = Raw("Không");
        var config = raw.ConfigCells
            .SetItem("HasDowel", ManholeValidatorTests.Cell("Có", "C9", "CF.Ga"))
            .SetItem("DowelSpec", ManholeValidatorTests.Cell("D18A500", "D9", "CF.Ga"))
            .SetItem("DowelLength", ManholeValidatorTests.Cell("500,5", "E9", "CF.Ga"))
            .SetItem("DowelHoleDia", ManholeValidatorTests.Cell(25, "F9", "CF.Ga"));
        RawProjectInput Input() => new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet,
            raw.Rows, config, new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
        var accepted = AccessoryInputMapper.Parse(Input(), "M1", new NumericPolicy("vi-VN", false, false));
        Assert.True(accepted.Success);
        Assert.Equal(500.5, accepted.Value!.Dowel!.LengthMm);
        config = config.SetItem("DowelLength", ManholeValidatorTests.Cell("1.000", "E9", "CF.Ga"));
        var rejected = AccessoryInputMapper.Parse(Input(), "M1", new NumericPolicy("vi-VN", false, false));
        Assert.False(rejected.Success);
        Assert.Contains(rejected.Diagnostics, d => d.Code == "NUMBER_FORMAT" && d.Source?.CellAddress == "E9");
    }

    [Theory]
    [InlineData("#VALUE!")]
    [InlineData("#REF!")]
    [InlineData("   ")]
    public void InvalidDisabledFeatureFormulaCannotEscapeValidator(string cached)
    {
        var result = new ManholeValidator(new NumericCellReader(), new NumericPolicy("en-US", false, true))
            .Validate(Raw(cached), ManholeValidatorTests.Catalogs(),
                new("S1", [ManholeValidatorTests.Id], CalculationScope.BaseRebarOnly, "S1-Analytic-1", true),
                CalculationPolicy.Select("S1-Analytic-1", true).Value!);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Block && d.Source?.CellAddress == "C9");
    }

    [Fact]
    public void ExplicitNoDowelDoesNotReadStaleDependentSpecification()
    {
        var raw = Raw("Không");
        var config = raw.ConfigCells
            .SetItem("HasDowel", new(RawCellKind.Boolean, null, null, false, null, null, raw.ConfigCells["HasDowel"].Source))
            .SetItem("DowelSpec", ManholeValidatorTests.Cell("stale-invalid", "D9", "CF.Ga"))
            .SetItem("DowelLength", ManholeValidatorTests.Cell(-1, "E9", "CF.Ga"));
        raw = new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet, raw.Rows, config,
            new Dictionary<string, IEnumerable<RawManholeRow>>(), []);
        var result = AccessoryInputMapper.Parse(raw, "M1", new NumericPolicy("en-US", false, false));
        Assert.True(result.Success);
        Assert.Null(result.Value!.Dowel);
        Assert.Null(result.Value.DowelHoleDiameterMm);
        Assert.Empty(result.Diagnostics);
    }
}
