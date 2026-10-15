using System.Text.Json;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Headless;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class S2QuantityHeadlessTests
{
    [Theory]
    [InlineData(Profile.GaThamLienKhoi)]
    [InlineData(Profile.GaThamNapRoi)]
    [InlineData(Profile.GaThamThuNapRoi)]
    public async Task ConsoleArgumentArrayProducesRealConcreteQuantityRoundtripForEveryProfile(Profile profile)
    {
        string input = TestWorkbookFactory.CreateS2Synthetic(profile);
        using var output = new TemporaryOutput();
        string id = "G400-1";
        var args = Args(profile, id, input, output.Directory, CalculationScope.ConcreteOnly);
        using var text = new StringWriter();

        int exitCode = await HeadlessRunner.RunAsync(args, text, CancellationToken.None);

        Assert.True(exitCode == 0, text.ToString());
        string run = text.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Single(x => x.StartsWith("RUN_DIRECTORY=", StringComparison.Ordinal))["RUN_DIRECTORY=".Length..];
        Assert.True(File.Exists(Path.Combine(run, "package.json")));
        Assert.True(File.Exists(Path.Combine(run, "quantity.xlsx")));
        Assert.True(File.Exists(Path.Combine(run, "trace.json")));
        Assert.True(File.Exists(Path.Combine(run, "delta.json")));
        Assert.False(File.Exists(Path.Combine(run, "drawing.json")));
        using var status = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "status.json")));
        Assert.Equal("NotRequested", status.RootElement.GetProperty("Drawing").GetString());
        using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "package.json")));
        Assert.Equal("ConcreteOnly", package.RootElement.GetProperty("Scope").GetString());
        Assert.Equal("Calculated", package.RootElement.GetProperty("ConcreteStatus").GetString());
        Assert.Equal("NotCalculated", package.RootElement.GetProperty("FormworkStatus").GetString());
    }

    [Theory]
    [InlineData(Profile.GaThamLienKhoi)]
    [InlineData(Profile.GaThamNapRoi)]
    [InlineData(Profile.GaThamThuNapRoi)]
    public async Task ConsoleArgumentArrayProducesRealFormworkQuantityRoundtripForEveryProfile(Profile profile)
    {
        string input = TestWorkbookFactory.CreateS2Synthetic(profile);
        using var output = new TemporaryOutput();
        string id = "G400-1";
        var args = Args(profile, id, input, output.Directory, CalculationScope.FormworkOnly);
        using var text = new StringWriter();

        int exitCode = await HeadlessRunner.RunAsync(args, text, CancellationToken.None);

        Assert.True(exitCode == 0, text.ToString());
        string run = text.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Single(x => x.StartsWith("RUN_DIRECTORY=", StringComparison.Ordinal))["RUN_DIRECTORY=".Length..];
        Assert.True(File.Exists(Path.Combine(run, "quantity.xlsx")));
        Assert.True(File.Exists(Path.Combine(run, "trace.json")));
        Assert.True(File.Exists(Path.Combine(run, "delta.json")));
        using var package = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "package.json")));
        Assert.Equal("FormworkOnly", package.RootElement.GetProperty("Scope").GetString());
        Assert.Equal("NotCalculated", package.RootElement.GetProperty("ConcreteStatus").GetString());
        Assert.Equal("Calculated", package.RootElement.GetProperty("FormworkStatus").GetString());
    }

    [Fact]
    public async Task InvalidCfGaStopsBeforeS2EngineAndQuantityWriter()
    {
        string input = TestWorkbookFactory.CreateS2Synthetic(Profile.GaThamLienKhoi,
            book => book.Worksheet("CF.Ga").Cell("C3").Clear());
        using var output = new TemporaryOutput();
        var writer = new CountingQuantityWriter();
        using var text = new StringWriter();

        int exitCode = await HeadlessRunner.RunAsync(
            Args(Profile.GaThamLienKhoi, "G400-1", input, output.Directory, CalculationScope.ConcreteOnly),
            text, CancellationToken.None, quantityWriter: writer);

        Assert.Equal(1, exitCode);
        Assert.Equal(0, writer.Calls);
        Assert.DoesNotContain("RUN_DIRECTORY=", text.ToString(), StringComparison.Ordinal);
        Assert.Empty(Directory.GetDirectories(output.Directory));
    }

    [Fact]
    public async Task CancelledS2RunPublishesNoFinalOutput()
    {
        string input = TestWorkbookFactory.CreateS2Synthetic(Profile.GaThamLienKhoi);
        using var output = new TemporaryOutput();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var text = new StringWriter();

        int exitCode = await HeadlessRunner.RunAsync(
            Args(Profile.GaThamLienKhoi, "G400-1", input, output.Directory, CalculationScope.ConcreteOnly),
            text, cancellation.Token);

        Assert.Equal(1, exitCode);
        Assert.Contains("no final run published", text.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetDirectories(output.Directory));
    }

    private static string[] Args(Profile profile, string id, string input, string output, CalculationScope scope) =>
    ["--project", "S2", "--input", input, "--profile", profile.ToString(), "--sheet", profile.ToString(),
        "--id", id, "--scope", scope.ToString(), "--policy", "S2Geometry-1", "--allow-experimental", "true", "--output", output];

    private sealed class TemporaryOutput : IDisposable
    {
        public TemporaryOutput()
        {
            Directory = Path.Combine(Path.GetTempPath(), "dbtool-s2-headless-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
        }
        public string Directory { get; }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }

    private sealed class CountingQuantityWriter : IQuantityWorkbookWriter
    {
        public int Calls { get; private set; }
        public OperationResult<ExportArtifact> Write(CalculationPackage package, WorkbookWriteRequest request, CancellationToken token)
        {
            Calls++;
            return new(true, new ExportArtifact(Path.Combine(request.OutputDirectory, "quantity.xlsx"),
                package.InputHash, package.RuleVersion, package.Scope, RecalculationStatus.NotRun), []);
        }
    }
}
