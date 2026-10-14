using System.Collections.Immutable;
using DBTool.Domain;

namespace DBTool.Application;

public enum ExpectedValueSourceKind
{
    EffectiveInput,
    RecipeTemplate,
    SourcePdf,
    OwnerRule,
    Unknown
}

public sealed record NumericalEvidenceRecord
{
    public NumericalEvidenceRecord(
        string id,
        string profile,
        string? manholeId,
        int? rowIndex,
        string? frameRole,
        string? reviewedRegion,
        ExpectedValueSourceKind expectedSourceKind,
        double expectedValue,
        double? actualValue,
        double? delta,
        double tolerance,
        string unit,
        string toleranceRuleId,
        ImmutableArray<string> sourceIds,
        string? nativeHandle,
        string? evidencePath,
        string? evidenceHash,
        ComparisonClassification classification,
        string verdict)
    {
        Id = id;
        Profile = profile;
        ManholeId = manholeId;
        RowIndex = rowIndex;
        FrameRole = frameRole;
        ReviewedRegion = reviewedRegion;
        ExpectedSourceKind = expectedSourceKind;
        ExpectedValue = expectedValue;
        ActualValue = actualValue;
        Delta = delta;
        Tolerance = tolerance;
        Unit = unit;
        ToleranceRuleId = toleranceRuleId;
        SourceIds = sourceIds;
        NativeHandle = nativeHandle;
        EvidencePath = evidencePath;
        EvidenceHash = evidenceHash;
        Classification = classification;
        Verdict = verdict;
    }

    public string Id { get; }
    public string Profile { get; }
    public string? ManholeId { get; }
    public int? RowIndex { get; }
    public string? FrameRole { get; }
    public string? ReviewedRegion { get; }
    public ExpectedValueSourceKind ExpectedSourceKind { get; }
    public double ExpectedValue { get; }
    public double? ActualValue { get; }
    public double? Delta { get; }
    public double Tolerance { get; }
    public string Unit { get; }
    public string ToleranceRuleId { get; }
    public ImmutableArray<string> SourceIds { get; }
    public string? NativeHandle { get; }
    public string? EvidencePath { get; }
    public string? EvidenceHash { get; }
    public ComparisonClassification Classification { get; }
    public string Verdict { get; }
}

public sealed record ReferenceIdentityContext
{
    public ReferenceIdentityContext(
        string profile,
        string? manholeId = null,
        int? rowIndex = null,
        string? inputHash = null,
        string? frameRole = null,
        string? reviewedRegion = null)
    {
        Profile = profile;
        ManholeId = manholeId;
        RowIndex = rowIndex;
        InputHash = inputHash;
        FrameRole = frameRole;
        ReviewedRegion = reviewedRegion;
    }

    public string Profile { get; }
    public string? ManholeId { get; }
    public int? RowIndex { get; }
    public string? InputHash { get; }
    public string? FrameRole { get; }
    public string? ReviewedRegion { get; }
}

public sealed class ReferenceComparisonService
{
    public OperationResult<ComparisonReport> Compare(CalculationPackage package, ReferenceFixture fixture, ComparisonPolicy policy) =>
        Compare(package, fixture, policy, null);

    public OperationResult<ComparisonReport> Compare(CalculationPackage package, ReferenceFixture fixture, ComparisonPolicy policy, ReferenceIdentityContext? identityContext)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(policy);

        var actual = package.ConcreteLines ?? [];
        var actualById = actual.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var lines = new List<ComparisonLine>();
        foreach (var reference in fixture.Values)
        {
            actualById.TryGetValue(reference.Id, out var geometry);
            var classification = Classify(package, fixture, policy, reference, geometry, identityContext, out double? delta);
            lines.Add(new ComparisonLine(reference.Id, reference.ComponentId, reference.MaterialId, reference.Unit,
                reference.Value, geometry?.Net, delta, reference.ReferenceRuleId, reference.Source, classification,
                geometry?.TraceIds ?? []));
        }

        return new(true, new ComparisonReport(fixture.FixtureId, policy.PolicyId, lines, []), []);
    }

    public static ExpectedValueSourceKind DetectExpectedSource(SourceRef source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.SourceKind switch
        {
            SourceKind.PdfPage => ExpectedValueSourceKind.SourcePdf,
            SourceKind.Dwg or SourceKind.Dxf => ExpectedValueSourceKind.RecipeTemplate,
            SourceKind.WorkbookCell => ExpectedValueSourceKind.EffectiveInput,
            _ => ExpectedValueSourceKind.OwnerRule
        };
    }

    public static ImmutableArray<NumericalEvidenceRecord> ExtractNumericalEvidence(
        CalculationPackage package,
        ReferenceFixture fixture,
        ComparisonPolicy policy,
        ReferenceIdentityContext? identityContext = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(policy);

        var profile = identityContext?.Profile ?? "Unknown";
        var manholeId = identityContext?.ManholeId;
        var rowIndex = identityContext?.RowIndex;
        var frameRole = identityContext?.FrameRole;
        var reviewedRegion = identityContext?.ReviewedRegion;

        var actual = package.ConcreteLines ?? [];
        var actualById = actual.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var evidence = ImmutableArray.CreateBuilder<NumericalEvidenceRecord>();

        foreach (var reference in fixture.Values)
        {
            actualById.TryGetValue(reference.Id, out var geometry);
            var classification = Classify(package, fixture, policy, reference, geometry, identityContext, out double? delta);
            double tolerance = Math.Max(policy.AbsoluteTolerance, policy.RelativeTolerance * Math.Max(Math.Abs(reference.Value), Math.Abs(geometry?.Net ?? 0.0)));
            var sourceKind = DetectExpectedSource(reference.Source);
            var actualSources = geometry?.Sources.Select(s => s.Sha256) ?? Enumerable.Empty<string>();
            var sourceIds = actualSources.Concat([reference.Source.Sha256]).Distinct(StringComparer.Ordinal).ToImmutableArray();
            var verdict = classification switch
            {
                ComparisonClassification.Same => "Passed",
                ComparisonClassification.InputMismatch => "InputDifference",
                ComparisonClassification.PolicyDifference => "PolicyDifference",
                ComparisonClassification.SourceFormulaDefect => "Failed",
                ComparisonClassification.ImplementationDefect => "Failed",
                _ => "Unresolved"
            };

            evidence.Add(new NumericalEvidenceRecord(
                reference.Id,
                profile,
                manholeId,
                rowIndex,
                frameRole,
                reviewedRegion,
                sourceKind,
                reference.Value,
                geometry?.Net,
                delta,
                tolerance,
                reference.Unit.ToString(),
                reference.ReferenceRuleId,
                sourceIds,
                geometry?.Id,
                reference.Source.RelativePath,
                reference.Source.Sha256,
                classification,
                verdict));
        }

        return evidence.ToImmutable();
    }

    private static ComparisonClassification Classify(CalculationPackage package, ReferenceFixture fixture, ComparisonPolicy policy,
        ReferenceValue reference, QuantityLine? geometry, ReferenceIdentityContext? identityContext, out double? delta)
    {
        delta = geometry is null ? null : geometry.Net - reference.Value;
        if (geometry is null) return ComparisonClassification.Unresolved;

        // Identity check: InputDifference is not a regression defect
        var expectedHash = identityContext?.InputHash ?? fixture.CanonicalInputHash;
        if (!string.Equals(package.InputHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            return ComparisonClassification.InputMismatch;

        if (geometry.Unit != reference.Unit || !string.Equals(geometry.MaterialId, reference.MaterialId, StringComparison.Ordinal))
            return ComparisonClassification.Unresolved;
        if (!string.Equals(geometry.RuleId, reference.ReferenceRuleId, StringComparison.Ordinal) ||
            !string.Equals(policy.RuleVersion, package.RuleVersion, StringComparison.Ordinal))
            return ComparisonClassification.PolicyDifference;
        if (geometry.Sources.Any(source => SameCell(source, reference.Source) &&
            !string.Equals(source.Sha256, reference.Source.Sha256, StringComparison.OrdinalIgnoreCase)))
            return ComparisonClassification.SourceFormulaDefect;
        double tolerance = Math.Max(policy.AbsoluteTolerance, policy.RelativeTolerance * Math.Max(Math.Abs(reference.Value), Math.Abs(geometry.Net)));
        return Math.Abs(delta!.Value) <= tolerance ? ComparisonClassification.Same : ComparisonClassification.ImplementationDefect;
    }

    private static bool SameCell(SourceRef left, SourceRef right) =>
        left.SourceKind == SourceKind.WorkbookCell && right.SourceKind == SourceKind.WorkbookCell &&
        string.Equals(left.RelativePath, right.RelativePath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.SheetName, right.SheetName, StringComparison.Ordinal) &&
        string.Equals(left.CellAddress, right.CellAddress, StringComparison.OrdinalIgnoreCase);
}
