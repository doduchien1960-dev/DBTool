using DBTool.Domain;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class QuantityExportUseCaseTests
{
    [Fact]
    public async Task ExportQuantityDoesNotRecalculateAndSupportsRetryOnSamePackage()
    {
        var writer = new CountingWriter();
        var engineCalls = 1;
        var useCase = new ExportCalculationUseCase(null!, writer);
        var package = Package();
        var request = new WorkbookWriteRequest(Path.GetTempPath(), null, "quantity", "test");

        var first = await useCase.ExportQuantityAsync(package, request, CancellationToken.None);
        var second = await useCase.ExportQuantityAsync(package, request, CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(2, writer.Calls);
        Assert.Equal(1, engineCalls);
        Assert.All(writer.Packages, value => Assert.Same(package, value));
    }

    [Fact]
    public async Task CancelledQuantityExportReturnsNullValueAndDoesNotCallWriter()
    {
        var writer = new CountingWriter();
        var useCase = new ExportCalculationUseCase(null!, writer);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await useCase.ExportQuantityAsync(Package(),
            new WorkbookWriteRequest(Path.GetTempPath(), null, "quantity", "cancel"), cancellation.Token);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(0, writer.Calls);
        Assert.Contains(result.Diagnostics, x => x.Code == "OPERATION_CANCELLED" && x.Severity == DiagnosticSeverity.Block);
    }

    private static CalculationPackage Package() =>
        new(new string('a', 64), new string('b', 64), "s2-engine", "contract-v2-quantity", "S2Geometry-1/v1", "S2Geometry-1",
            CalculationScope.ConcreteOnly, new GeometryModel([], []), [], null, CalculationStatus.Calculated,
            [new QuantityLine("Q-1", "Base", "Concrete", 1, 0, 0, 1, QuantityUnit.M3, "S2-Q-1", [], [])],
            CalculationStatus.NotCalculated, null, null, []);

    private sealed class CountingWriter : IQuantityWorkbookWriter
    {
        public int Calls { get; private set; }
        public List<CalculationPackage> Packages { get; } = [];

        public OperationResult<ExportArtifact> Write(CalculationPackage package, WorkbookWriteRequest request, CancellationToken token)
        {
            Calls++;
            Packages.Add(package);
            return new(true, new ExportArtifact(Path.Combine(request.OutputDirectory, "quantity.xlsx"),
                package.InputHash, package.RuleVersion, package.Scope, RecalculationStatus.NotRun), []);
        }
    }
}
