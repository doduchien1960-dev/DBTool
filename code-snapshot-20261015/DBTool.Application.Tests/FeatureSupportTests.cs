using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class FeatureSupportTests
{
    // Deliberately synthetic: T39.a owns registration of a production HeadlessS1 set.
    private static CapabilitySet Slice => new(new[]
    {
        "Profile.Mono", "Geometry.Pad", "Geometry.Base", "Geometry.Wall", "Geometry.Roof",
        "Geometry.RoofOpening.Centered700", "Geometry.RoundOpening.T1",
        "Steel.BaseBottom", "Steel.BaseTop"
    });
    private static CalculationPolicy Policy => CalculationPolicy.Select("S1-Analytic-1", true).Value!;
    private static FeatureSupportValidator Validator => new();

    private static ValidatedManholeSpec Spec(RawProjectInput? raw = null, CatalogSet? catalogs = null)
    {
        raw ??= ManholeValidatorTests.Raw();
        catalogs ??= ManholeValidatorTests.Catalogs();
        var id = $"S1/{raw.Profile}/S1-MONO-001";
        var result = new ManholeValidator(new NumericCellReader(), new NumericPolicy("en-US", false, false))
            .Validate(raw, catalogs, new CalculationRequest("S1", new[] { id }, CalculationScope.BaseRebarOnly, "S1-Analytic-1", true), Policy);
        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code)));
        return Assert.Single(result.Value!.SelectedManholes);
    }

    private static RawProjectInput WithRow(string key, object? value, string address)
    {
        var raw = ManholeValidatorTests.Raw();
        var row = new RawManholeRow(4, raw.Rows[0].Cells.SetItem(key, ManholeValidatorTests.Cell(value, address)));
        return new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet, new[] { row }, raw.ConfigCells,
            new Dictionary<string, IEnumerable<RawManholeRow>>(), Array.Empty<Diagnostic>());
    }

    private static RawProjectInput WithConfig(string key, object? value, string address)
    {
        var raw = ManholeValidatorTests.Raw();
        var config = raw.ConfigCells.SetItem(key, ManholeValidatorTests.Cell(value, address, "CF.Ga"));
        return new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet, raw.Rows, config,
            new Dictionary<string, IEnumerable<RawManholeRow>>(), Array.Empty<Diagnostic>());
    }

    private static void Blocked(ValidatedManholeSpec spec, string code, string featureId)
    {
        var result = Validator.Validate(spec, CalculationScope.BaseRebarOnly, Slice, Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.Severity == DiagnosticSeverity.Block &&
            d.Scope == DiagnosticScope.Manhole && d.ManholeId == spec.Input.Id && d.Actual == featureId);
    }

    [Fact]
    public void ApprovedSyntheticSlice_ReportsOnlyBaseRebarCalculated()
    {
        var result = Validator.Validate(Spec(), CalculationScope.BaseRebarOnly, Slice, Policy);
        Assert.True(result.Success, string.Join(";", result.Diagnostics.Select(d => d.Code)));
        var report = result.Value!;
        Assert.Equal(CalculationScope.BaseRebarOnly, report.Scope);
        Assert.Equal(CalculationStatus.NotCalculated, report.ConcreteStatus);
        Assert.Equal(CalculationStatus.NotCalculated, report.FormworkStatus);
        Assert.Equal(CapabilityStatus.Supported, report.Results.Single(x => x.FeatureId == "Steel.BaseBottom").Status);
        Assert.Equal(CapabilityStatus.Supported, report.Results.Single(x => x.FeatureId == "Geometry.RoundOpening.T1").Status);
        Assert.Equal(CapabilityStatus.NotRequested, report.Results.Single(x => x.FeatureId == "Concrete").Status);
        Assert.Equal(CapabilityStatus.NotRequested, report.Results.Single(x => x.FeatureId == "Formwork").Status);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void UnregisteredConsumerCannotBeReportedSupported()
    {
        var set = new CapabilitySet(Slice.FeatureIds.Where(x => x != "Steel.BaseTop"));
        var result = Validator.Validate(Spec(), CalculationScope.BaseRebarOnly, set, Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, x => x.Code == "FEATURE_UNSUPPORTED" && x.Actual == "Steel.BaseTop");
    }

    [Theory]
    [InlineData(PipeKind.Box)]
    [InlineData(PipeKind.DoubleBox)]
    public void BoxOrDoublePipeIsUnsupported(PipeKind kind)
    {
        var raw = WithRow("PipeCodeT1", "BOX", "O4");
        var entry = new PipeCatalogEntry("BOX", kind, null, 400, 400, 55,
            kind == PipeKind.DoubleBox ? 40 : null, 0);
        var catalogs = new CatalogSet(new Dictionary<string, PipeCatalogEntry> { ["BOX"] = entry },
            new Dictionary<string, SteelAngleEntry>(), new Dictionary<string, HatchEntry>(), new Dictionary<string, string>());
        Blocked(Spec(raw, catalogs), "FEATURE_UNSUPPORTED", kind == PipeKind.Box ? "Geometry.Pipe.Box" : "Geometry.Pipe.DoubleBox");
    }

    [Fact]
    public void NonzeroPipeOffsetIsUnsupportedEvenThoughItCanBeParsed() =>
        Blocked(Spec(WithConfig("PipeOffsetT1", 100, "I28")), "FEATURE_UNSUPPORTED", "Geometry.Pipe.Offset");

    [Fact]
    public void TwoRoundPipesAreUnsupported()
    {
        var raw = ManholeValidatorTests.Raw();
        var row = new RawManholeRow(4, raw.Rows[0].Cells
            .SetItem("PipeCodeT2", ManholeValidatorTests.Cell("D200", "Q4"))
            .SetItem("InvertT2", ManholeValidatorTests.Cell(300, "R4")));
        var changed = new RawProjectInput(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet,
            new[] { row }, raw.ConfigCells, new Dictionary<string, IEnumerable<RawManholeRow>>(), Array.Empty<Diagnostic>());
        Blocked(Spec(changed), "FEATURE_UNSUPPORTED", "Geometry.Pipe.Multiple");
    }

    [Fact]
    public void MissingPipeIsOutsideSingleOpeningSlice()
    {
        var raw = ManholeValidatorTests.Raw();
        var row = new RawManholeRow(4, raw.Rows[0].Cells
            .SetItem("PipeCodeT1", ManholeValidatorTests.Cell(null, "O4"))
            .SetItem("InvertT1", ManholeValidatorTests.Cell(null, "P4")));
        var changed = new RawProjectInput(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet,
            new[] { row }, raw.ConfigCells, new Dictionary<string, IEnumerable<RawManholeRow>>(), Array.Empty<Diagnostic>());
        Blocked(Spec(changed), "FEATURE_UNSUPPORTED", "Geometry.Pipe.Count");
    }

    [Fact]
    public void ActiveNeckIsUnsupported() =>
        Blocked(Spec(WithRow("NeckHeight", 100, "W4")), "FEATURE_UNSUPPORTED", "Geometry.Neck");

    [Fact]
    public void ActiveHaunchIsUnsupported() =>
        Blocked(Spec(WithConfig("BaseHaunch", 20, "D5")), "FEATURE_UNSUPPORTED", "Geometry.BaseHaunch");

    [Fact]
    public void DensityDistributionIsOutsideStrictSlice() =>
        Blocked(Spec(WithConfig("Distribution", "Theo mật độ", "K19")),
            "FEATURE_UNSUPPORTED", "Steel.Distribution.DensityCentered");

    [Fact]
    public void RoundOpeningCrossingWallTopIsUnsupported() =>
        Blocked(Spec(WithRow("InvertT1", 1200, "P4")),
            "FEATURE_UNSUPPORTED", "Geometry.Pipe.OutsideWall");

    [Fact]
    public void DifferentRoofOpeningCannotClaimCentered700Consumer() =>
        Blocked(Spec(WithConfig("NeckSize", "800x700", "E11")),
            "FEATURE_UNSUPPORTED", "Geometry.RoofOpening.NonStandard");

    [Fact]
    public void HookWithoutBendRuleIsNeedsRule()
    {
        var spec = Spec(WithConfig("HookPattern.C", "1M", "C20"));
        Blocked(spec, "FEATURE_NEEDS_RULE", "Steel.BaseHook.Bottom");
    }

    [Theory]
    [InlineData(CalculationScope.FullRebar)]
    [InlineData(CalculationScope.ConcreteOnly)]
    [InlineData(CalculationScope.FormworkOnly)]
    [InlineData(CalculationScope.Full)]
    public void UnsupportedScopeCannotReturnBaseOnlyAsFull(CalculationScope scope)
    {
        var result = Validator.Validate(Spec(), scope, Slice, Policy);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics, d => d.Code == "S1_SCOPE_UNSUPPORTED" && d.Severity == DiagnosticSeverity.Block);
    }

    [Fact]
    public void ReadWallMeshRemainsStoredButNotRequestedByBaseScope()
    {
        var spec = Spec(WithRow("WallOuterMesh", "D10A150", "J4"));
        Assert.True(spec.Input.FeatureRequests.Single(x => x.FeatureId == "WallRebar").Requested);
        var result = Validator.Validate(spec, CalculationScope.BaseRebarOnly, Slice, Policy);
        Assert.True(result.Success);
        var wall = result.Value!.Results.Single(x => x.FeatureId == "WallRebar");
        Assert.False(wall.Requested);
        Assert.Equal(CapabilityStatus.NotRequested, wall.Status);
        Assert.True(spec.Input.UnsupportedRawFields.ContainsKey("Row.WallOuterMesh"));
    }

    [Theory]
    [InlineData("Ties.H", "D8A150", "Accessories.Ties")]
    [InlineData("HasDowel", "Có", "Accessories.Dowels")]
    public void ActiveBaseAccessoryCannotBeIgnored(string field, string value, string featureId) =>
        Blocked(Spec(WithConfig(field, value, "H20")), "FEATURE_UNSUPPORTED", featureId);

    [Fact]
    public void NeckConfigDoesNotCreateNeckWhenHeightIsZero()
    {
        var spec = Spec(WithConfig("Anchor", "D6A150L100", "D11"));
        var result = Validator.Validate(spec, CalculationScope.BaseRebarOnly, Slice, Policy);
        Assert.True(result.Success);
        Assert.Equal(CapabilityStatus.NotRequested, result.Value!.Results.Single(x => x.FeatureId == "NeckAnchor").Status);
    }

    [Fact]
    public void UnknownActiveRawSwitchIsBlockedInsteadOfSilentlyIgnored()
    {
        var raw = ManholeValidatorTests.Raw();
        var source = ManholeValidatorTests.Cell("Có", "M33", "CF.Ga").Source;
        var config = raw.ConfigCells.SetItem("UnknownSwitch",
            new RawCell(RawCellKind.Boolean, null, null, true, null, null, source));
        var changed = new RawProjectInput(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet,
            raw.Rows, config, new Dictionary<string, IEnumerable<RawManholeRow>>(), Array.Empty<Diagnostic>());
        Blocked(Spec(changed), "FEATURE_UNSUPPORTED", "Raw.Config.UnknownSwitch");
    }

    [Fact]
    public void UnknownEnabledTextSwitchIsBlocked()
    {
        Blocked(Spec(WithConfig("UnknownTextSwitch", "Có", "M34")),
            "FEATURE_UNSUPPORTED", "Raw.Config.UnknownTextSwitch");
    }

    [Fact]
    public void UnknownNonblankFeatureTextIsBlockedUntilClassified() =>
        Blocked(Spec(WithConfig("CatchBasinType", "ConcreteBasin", "M35")),
            "FEATURE_UNSUPPORTED", "Raw.Config.CatchBasinType");

    [Fact]
    public void PresentationLabelIsNotARequestedConstructionFeature()
    {
        var result = Validator.Validate(Spec(WithConfig("Elevation.D", "Z_BASE", "D32")),
            CalculationScope.BaseRebarOnly, Slice, Policy);
        Assert.True(result.Success);
    }

    [Fact]
    public void LiftingSizeWithoutLiftingSpecIsPassiveConfiguration()
    {
        var result = Validator.Validate(Spec(WithConfig("LiftingSize", "D10", "H9")),
            CalculationScope.BaseRebarOnly, Slice, Policy);
        Assert.True(result.Success);
    }

    [Fact]
    public void UnknownExplicitlyDisabledSwitchDoesNotRequestAConsumer()
    {
        var result = Validator.Validate(Spec(WithConfig("UnknownTextSwitch", "Không", "M34")),
            CalculationScope.BaseRebarOnly, Slice, Policy);
        Assert.True(result.Success);
        Assert.DoesNotContain(result.Value!.Results, x => x.FeatureId == "Raw.Config.UnknownTextSwitch");
    }

    [Theory]
    [InlineData(Profile.GaThamNapRoi)]
    [InlineData(Profile.GaThamThuNapRoi)]
    public void OtherProfilesCalculateIsUnsupportedEvenIfImportCanValidate(Profile profile)
    {
        var raw = ManholeValidatorTests.Raw();
        var config = raw.ConfigCells.SetItem("BeddingMortar", ManholeValidatorTests.Cell(0, "G9", "CF.Ga"));
        var typeTwo = new RawProjectInput(raw.ProjectId, raw.WorkbookHash, profile, profile.ToString(),
            raw.Rows, config, new Dictionary<string, IEnumerable<RawManholeRow>>(), Array.Empty<Diagnostic>());
        Blocked(Spec(typeTwo), "FEATURE_UNSUPPORTED", $"Profile.{profile}");
    }
}
