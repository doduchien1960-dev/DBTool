using DBTool.Application;
using DBTool.Domain;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class ReferenceFormulaTests
{
    [Fact]
    public void Bn820Ln570R150ReportsGeometryMinusReferenceDelta()
    {
        var report = Compare(
            lineId: "Bn820",
            actual: 0.07245375,
            expected: 0.07011,
            referenceRuleId: "REF-BN820",
            source: Source("KL", "HG!AR8", new string('a', 64), "=Bn820*150"));

        var line = Assert.Single(report.Lines);
        Assert.InRange(line.Delta!.Value, 0.00234375 - 1e-12, 0.00234375 + 1e-12);
        Assert.Equal(ComparisonClassification.ImplementationDefect, line.Classification);
    }

    [Fact]
    public void Roof1300SquareUsesReferenceWithoutS1OuterWidth()
    {
        var report = Compare("Roof", 0.18339, 0.18104625, "REF-ROOF-1300", Source("KL", "HG!AR8", new string('b', 64), "=1300*1300"));

        var line = Assert.Single(report.Lines);
        Assert.InRange(line.Delta!.Value, 0.00234375 - 1e-12, 0.00234375 + 1e-12);
    }

    [Fact]
    public void Same700SquareIsSame()
    {
        var report = Compare("Roof", 0.0735, 0.0735, "REF-ROOF-700", Source("KL", "HG!AS8", new string('c', 64), "=700*700"));

        Assert.Equal(ComparisonClassification.Same, Assert.Single(report.Lines).Classification);
    }

    [Fact]
    public void HaunchPanelDistinguishesConcreteHeightFromTotalHeight()
    {
        const double hc = 100;
        const double hv = 50;
        double total = 2 * 417.5 * 840 * (hc + hv) / 1_000_000_000d;
        double concreteOnly = 2 * 417.5 * 840 * hc / 1_000_000_000d;
        var report = Compare("Panel/Haunch", total, concreteOnly, "REF-PANEL-HC", Source("KL", "HG!AR8", new string('d', 64), "=2*417.5*840*100"));

        var line = Assert.Single(report.Lines);
        Assert.InRange(line.Delta!.Value, 0.03507 - 1e-12, 0.03507 + 1e-12);
    }

    [Fact]
    public void SourceCellHashChangeIsClassifiedAsSourceFormulaDefect()
    {
        var reference = Source("KL", "HG!AR8", new string('e', 64), "=historical");
        var actual = Source("KL", "HG!AR8", new string('f', 64), "=changed");
        var report = Compare("Roof", 0.2, 0.1, "REF-ROOF", reference, actual);

        Assert.Equal(ComparisonClassification.SourceFormulaDefect, Assert.Single(report.Lines).Classification);
        Assert.Equal("HG!AR8", report.Lines[0].ReferenceSource.CellAddress);
        Assert.Equal(new string('E', 64), report.Lines[0].ReferenceSource.Sha256);
    }

    [Fact]
    public void SameG400WithDifferentDimensionsIsInputMismatchNotQuantityMatch()
    {
        var line = new QuantityLine("G400", "G400", "Concrete", 1.0, 0, 0, 1.0, QuantityUnit.M3, "REF-G400", [], []);
        var package = Package(line, new string('a', 64));
        var fixture = Fixture("G400", 1.0, "REF-G400", Source("KL", "HG!AR8", new string('1', 64), "=1000*1000"), new string('2', 64));
        var result = new ReferenceComparisonService().Compare(package, fixture, new ComparisonPolicy("CMP-1", "RULE-1"));

        Assert.True(result.Success);
        Assert.Equal(ComparisonClassification.InputMismatch, Assert.Single(result.Value!.Lines).Classification);
    }

    [Fact]
    public void RuleDifferenceIsPolicyDifferenceAndUnmatchedLineIsUnresolved()
    {
        var ruleDifference = Compare("Roof", 0.2, 0.1, "REF-RULE", Source("KL", "HG!AR8", new string('7', 64), "=x"), actualRuleId: "GEOM-RULE");
        Assert.Equal(ComparisonClassification.PolicyDifference, Assert.Single(ruleDifference.Lines).Classification);

        var package = Package(new QuantityLine("Other", "Other", "Concrete", 0.2, 0, 0, 0.2, QuantityUnit.M3, "REF-RULE", [], []));
        var fixture = Fixture("Roof", 0.1, "REF-RULE", Source("KL", "HG!AR8", new string('8', 64), "=x"));
        var result = new ReferenceComparisonService().Compare(package, fixture, new ComparisonPolicy("CMP-1", "RULE-1"));

        Assert.True(result.Success);
        Assert.Equal(ComparisonClassification.Unresolved, Assert.Single(result.Value!.Lines).Classification);
    }

    [Fact]
    public void ReferenceSameInputAndRuleWithDifferentValueIsImplementationDefect()
    {
        var report = Compare("Roof", 0.2, 0.1, "REF-RULE", Source("KL", "HG!AR8", new string('9', 64), "=x"));

        Assert.Equal(ComparisonClassification.ImplementationDefect, Assert.Single(report.Lines).Classification);
    }

    private static ComparisonReport Compare(string lineId, double actual, double expected, string referenceRuleId,
        SourceRef source, SourceRef? actualSource = null, string? canonicalInputHash = null, string? actualRuleId = null)
    {
        var packageLine = new QuantityLine(lineId, lineId, "Concrete", actual, 0, 0, actual, QuantityUnit.M3,
            actualRuleId ?? referenceRuleId, actualSource is null ? [] : [actualSource], []);
        var package = Package(packageLine, canonicalInputHash);
        var fixture = Fixture(lineId, expected, referenceRuleId, source, canonicalInputHash);
        var result = new ReferenceComparisonService().Compare(package, fixture, new ComparisonPolicy("CMP-1", "RULE-1"));
        Assert.True(result.Success);
        return result.Value!;
    }

    private static ReferenceFixture Fixture(string lineId, double expected, string ruleId, SourceRef source, string? canonicalInputHash = null) =>
        new("fixture-" + lineId, canonicalInputHash ?? new string('a', 64), [
            new ReferenceValue(lineId, lineId, "Concrete", expected, QuantityUnit.M3, ruleId, source)]);

    private static CalculationPackage Package(QuantityLine line, string? inputHash = null) =>
        new(inputHash ?? new string('a', 64), new string('b', 64), "engine", "contract", "RULE-1", "POLICY-1",
            CalculationScope.ConcreteOnly, new GeometryModel([], []), [], null, CalculationStatus.Calculated, [line],
            CalculationStatus.NotCalculated, null, null, []);

    private static SourceRef Source(string sheet, string cell, string hash, string formula) =>
        new(SourceKind.WorkbookCell, "source.xlsx", hash, sheet, cell, null, formula);
}
