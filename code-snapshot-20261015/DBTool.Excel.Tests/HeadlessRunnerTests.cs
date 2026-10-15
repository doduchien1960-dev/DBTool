using System.Text.Json;
using System.Security.Cryptography;
using ClosedXML.Excel;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Drawing;
using DBTool.Headless;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Excel.Tests;

public sealed class HeadlessRunnerTests
{
    [Theory]
    [InlineData("20")]
    [InlineData("30")]
    [InlineData("40")]
    public async Task ExplicitScaleGeneratesFreshDrawingAndRecordsRequestedDenominator(string scale)
    {
        string input=TestWorkbookFactory.CreateS1FromJson();
        string root=Path.Combine(Path.GetTempPath(),"dbtool-scale-"+Guid.NewGuid().ToString("N"));
        try
        {
            var output=new StringWriter();
            int exit=await HeadlessRunner.RunAsync([..Args(input,root),"--scale",scale],output,default);
            Assert.True(exit==0,output.ToString());
            string run=Assert.Single(Directory.GetDirectories(root,"run-*"));
            Assert.True(DrawingPackageJson.DeserializeResult(File.ReadAllText(Path.Combine(run,"drawing.json"))).Success);
            using var status=JsonDocument.Parse(File.ReadAllText(Path.Combine(run,"status.json")));
            Assert.Equal(double.Parse(scale,System.Globalization.CultureInfo.InvariantCulture),
                status.RootElement.GetProperty("PresentationScaleDenominator").GetDouble());
        }
        finally {File.Delete(input);if(Directory.Exists(root))Directory.Delete(root,true);}
    }

    [Theory]
    [InlineData("0")]
    [InlineData("NaN")]
    [InlineData("25")]
    public async Task InvalidScaleIsRejectedBeforeReadingWorkbook(string scale)
    {
        var text=new StringWriter();
        int exit=await HeadlessRunner.RunAsync([..Args("does-not-exist.xlsx","unused-output"),"--scale",scale],text,default);
        Assert.Equal(1,exit);
        Assert.Contains("CLI_ARGUMENT_INVALID: Scale must be 20, 30 or 40.",text.ToString());
    }

    [Theory]
    [InlineData(false,false,false,false,"NotRequested")]
    [InlineData(true,false,false,false,"Failed")]
    [InlineData(true,true,false,false,"Failed")]
    [InlineData(true,true,true,true,"Partial")]
    [InlineData(true,true,true,false,"Complete")]
    public void N11DrawingStatusUsesActualPayloadAndCompleteness(bool requested,bool payload,bool valid,bool partial,string expected) =>
        Assert.Equal(expected,HeadlessRunner.DrawingStatus(requested,payload,valid,partial));
    [Fact]
    public async Task NativeRoundedCoverSurvivesRealCalculationPackageAndDrawingExport()
    {
        string input=TestWorkbookFactory.CopySource(2,book=>
        {
            var cf=book.Worksheet("CF.Ga");
            cf.Cell("D13").Value=80;
            book.Worksheet("GaThamNapRoi").Cell("V4").Value=300; // Keep the source pipe wholly within its wall for this serialization test.
            foreach(var c in new[]{"C9","H11","C25"}) cf.Cell(c).Value="Không";
            cf.Cell("H9").Value="0D12";cf.Cell("J11").Value="0";cf.Cell("H23").Value="0";
            foreach(var c in new[]{"D28","F28","C30","E30"}) cf.Cell(c).Value=0;
        });
        string root=Path.Combine(Path.GetTempPath(),"dbtool-native-headless-"+Guid.NewGuid().ToString("N"));
        try
        {
            var output=new StringWriter();
            int exit=await HeadlessRunner.RunAsync(S3Args(input,root,Profile.GaThamNapRoi,"G400-1"),output,default);
            Assert.True(exit==0,output.ToString());
            string run=Assert.Single(Directory.GetDirectories(root,"run-*"));
            using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(run,"package.json")));
            var cover=Assert.Single(json.RootElement.GetProperty("Geometry").GetProperty("PurchasedCovers").EnumerateArray());
            Assert.Equal("RoundedRectangleProfile",cover.GetProperty("LocalClearProfile").GetProperty("$type").GetString());
            Assert.Equal(100,cover.GetProperty("SectionOutlines").GetProperty("Rule").GetProperty("DepthMm").GetDouble());
            var drawing=DrawingPackageJson.Deserialize(File.ReadAllText(Path.Combine(run,"drawing.json")));
            Assert.Contains(drawing.Primitives.OfType<BlockPrimitive>(),b=>b.SemanticId.StartsWith("PurchasedCover/",StringComparison.Ordinal));
        }
        finally {File.Delete(input);if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
    [Fact]
    public async Task RealWorkbookThroughRunnerProducesThreeMatchingArtifacts()
    {
        string input=TestWorkbookFactory.CreateS1FromJson();
        string root=Path.Combine(Path.GetTempPath(),"dbtool-t39-"+Guid.NewGuid().ToString("N"));
        var output=new StringWriter();
        int exit=await HeadlessRunner.RunAsync(Args(input,root),output,default);
        Assert.Equal(0,exit);
        string run=Directory.GetDirectories(root,"run-*").Single();
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(run,"package.json")));
        Assert.Equal(48,json.RootElement.GetProperty("Occurrences").GetArrayLength());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))),json.RootElement.GetProperty("SourceWorkbookHash").GetString());
        var drawing=DrawingPackageJson.Deserialize(File.ReadAllText(Path.Combine(run,"drawing.json")));
        Assert.Equal(6,drawing.Views.Length);
        var rows=drawing.Primitives.OfType<TableRowPrimitive>().Where(r=>r.ViewId=="SteelSchedule").ToArray();
        Assert.All(rows,row=>Assert.Equal(row.Cells.Length,row.Placements.Length));
        var total=Assert.Single(drawing.Primitives.OfType<BlockPrimitive>(),p=>p.Role=="Schedule/TotalMass");
        Assert.Equal("SteelSummary",total.BlockRole);
        Assert.Equal(9,total.Attributes.Length);
        Assert.True(total.Insertion.Y < rows.SelectMany(r=>r.Placements).Min(p=>p.Position.Y));
        Assert.Equal(json.RootElement.GetProperty("SteelSchedule").GetProperty("TotalMassKg").GetProperty("Value").GetDouble(),
            double.Parse(total.Attributes.Single(a=>a.Key=="TONG").Value,System.Globalization.CultureInfo.InvariantCulture));
        Assert.All(drawing.Primitives.Where(p=>p.ViewId is "BaseBottom" or "BaseTop"),p=>Assert.Equal("Base",p.ComponentId));
        Assert.Equal(json.RootElement.GetProperty("InputHash").GetString(),drawing.InputHash);
        using var book=new XLWorkbook(Path.Combine(run,"steel.xlsx"));
        Assert.Equal(48,Enumerable.Range(2,4).Sum(r=>book.Worksheet("Steel").Cell(r,7).GetDouble()));
        Assert.Equal(74620,Enumerable.Range(2,4).Sum(r=>book.Worksheet("Steel").Cell(r,9).GetDouble()),6);
        Assert.InRange(Enumerable.Range(2,4).Sum(r=>book.Worksheet("Steel").Cell(r,11).GetDouble()),46.06172839406173,46.06172839606173);
        Assert.Contains("BaseRebarOnly",output.ToString()); Assert.Contains("Experimental",output.ToString());
        string? evidence=Environment.GetEnvironmentVariable("DBTOOL_T39A_OUTPUT");
        if(!string.IsNullOrWhiteSpace(evidence))
        {
            Directory.CreateDirectory(evidence); File.Copy(input,Path.Combine(evidence,"s1-input.xlsx"),true);
            File.WriteAllText(Path.Combine(evidence,"test-run.log"),output.ToString());
        }
        File.Delete(input);
    }

    [Theory]
    [InlineData(Profile.GaThamLienKhoi)]
    [InlineData(Profile.GaThamNapRoi)]
    [InlineData(Profile.GaThamThuNapRoi)]
    public async Task S3FullRebarRunsThreeProfiles(Profile profile)
    {
        string input = profile == Profile.GaThamLienKhoi
            ? TestWorkbookFactory.CreateS1FromJson()
            : TestWorkbookFactory.CreateS2Synthetic(profile);
        string root = Path.Combine(Path.GetTempPath(), "dbtool-s3-" + Guid.NewGuid().ToString("N"));
        try
        {
            var output = new StringWriter();
            string id = profile == Profile.GaThamLienKhoi ? "S1-MONO-001" : "G400-1";
            Assert.True(await HeadlessRunner.RunAsync(S3Args(input, root, profile, id), output, default) == 0, output.ToString());
            string run = Directory.GetDirectories(root, "run-*").Single();
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "package.json")));
            Assert.Equal("FullRebar", json.RootElement.GetProperty("Scope").GetString());
            Assert.True(json.RootElement.GetProperty("Occurrences").GetArrayLength() > 0);
            Assert.True(File.Exists(Path.Combine(run, "drawing.json")));
            Assert.True(File.Exists(Path.Combine(run, "steel.xlsx")));
            string? evidenceRoot = Environment.GetEnvironmentVariable("DBTOOL_S3_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidenceRoot))
            {
                string evidence = Path.Combine(evidenceRoot, profile.ToString());
                Directory.CreateDirectory(evidence);
                foreach (string name in new[] { "package.json", "drawing.json", "steel.xlsx", "status.json" })
                    if (File.Exists(Path.Combine(run, name))) File.Copy(Path.Combine(run, name), Path.Combine(evidence, name), true);
                File.Copy(input, Path.Combine(evidence, "input.xlsx"), true);
                File.WriteAllText(Path.Combine(evidence, "source-sha256.txt"),
                    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(input))));
                File.WriteAllText(Path.Combine(evidence, "headless.log"), output.ToString());
            }
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task S3FullRunsConcreteFormworkAndSteelOnce()
    {
        string input = TestWorkbookFactory.CreateS1FromJson();
        string root = Path.Combine(Path.GetTempPath(), "dbtool-s3-full-" + Guid.NewGuid().ToString("N"));
        try
        {
            var output = new StringWriter();
            string[] args = ["--project", "S3", "--input", input, "--profile", Profile.GaThamLienKhoi.ToString(),
                "--sheet", Profile.GaThamLienKhoi.ToString(), "--id", "S1-MONO-001", "--scope", "Full",
                "--policy", "S2Rebar-1", "--allow-experimental", "true", "--output", root];
            Assert.True(await HeadlessRunner.RunAsync(args, output, default) == 0, output.ToString());
            string run = Directory.GetDirectories(root, "run-*").Single();
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(run, "package.json")));
            Assert.Equal("Calculated", json.RootElement.GetProperty("ConcreteStatus").GetString());
            Assert.Equal("Calculated", json.RootElement.GetProperty("FormworkStatus").GetString());
            Assert.True(File.Exists(Path.Combine(run, "steel.xlsx")));
        }
        finally
        {
            if (File.Exists(input)) File.Delete(input);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData("config","REBAR_REQUIRED")]
    [InlineData("box","FEATURE_UNSUPPORTED")]
    [InlineData("type2","FEATURE_UNSUPPORTED")]
    public async Task InvalidWorkbookPublishesNoRun(string mutation,string code)
    {
        string input=TestWorkbookFactory.CreateS1FromJson();
        using(var book=new XLWorkbook(input))
        {
            if(mutation=="config") book.Worksheet("GaThamLienKhoi").Cell("G4").Clear();
            if(mutation=="box") {book.Worksheet("CF.Cong").Cell("D4").Value=400;book.Worksheet("CF.Cong").Cell("E4").Value=400;book.Worksheet("CF.Cong").Cell("F4").Value=55;book.Worksheet("CF.Cong").Cell("G4").Value=0;book.Worksheet("GaThamLienKhoi").Cell("O4").Value="400x400";}
            if(mutation=="type2") {book.Worksheet("GaThamLienKhoi").Name="GaThamNapRoi";book.Worksheet("CF.Ga").Cell("G9").Value=0;}
            book.Save();
        }
        string root=Path.Combine(Path.GetTempPath(),"dbtool-t39-invalid-"+Guid.NewGuid().ToString("N"));
        var args=Args(input,root);
        if(mutation=="type2") {args[5]="GaThamNapRoi"; args[7]="GaThamNapRoi";}
        var output=new StringWriter();
        Assert.NotEqual(0,await HeadlessRunner.RunAsync(args,output,default));
        Assert.Contains(code,output.ToString());
        Assert.False(Directory.Exists(root)); File.Delete(input);
    }

    [Fact]
    public async Task CancellationDuringWorkbookSaveLeavesNoFinalRun()
    {
        string input=TestWorkbookFactory.CreateS1FromJson();
        string root=Path.Combine(Path.GetTempPath(),"dbtool-t39-cancel-"+Guid.NewGuid().ToString("N"));
        using var cancel=new CancellationTokenSource();
        var writer=new SteelWorkbookWriter((book,path)=>{book.SaveAs(path);cancel.Cancel();});
        var output=new StringWriter();
        Assert.NotEqual(0,await HeadlessRunner.RunAsync(Args(input,root),output,cancel.Token,writer));
        Assert.Contains("OPERATION_CANCELLED",output.ToString());
        Assert.Empty(Directory.EnumerateFileSystemEntries(root)); File.Delete(input);
    }

    [Fact]
    public async Task ExcelFailureKeepsCompletedPackageWithExplicitFailedStatus()
    {
        string input=TestWorkbookFactory.CreateS1FromJson();
        string root=Path.Combine(Path.GetTempPath(),"dbtool-t39-fail-"+Guid.NewGuid().ToString("N"));
        var output=new StringWriter();
        Assert.Equal(2,await HeadlessRunner.RunAsync(Args(input,root),output,default,new SteelWorkbookWriter((_,_)=>throw new IOException("injected"))));
        string run=Directory.GetDirectories(root,"run-*").Single();
        Assert.True(File.Exists(Path.Combine(run,"package.json"))); Assert.True(File.Exists(Path.Combine(run,"drawing.json")));
        Assert.False(File.Exists(Path.Combine(run,"steel.xlsx"))); Assert.Contains("Excel=Failed",output.ToString()); File.Delete(input);
    }

    [Fact]
    public async Task WorkbookMetadataChangesSourceHashButNotCalculationHash()
    {
        string input = TestWorkbookFactory.CreateS1FromJson();
        string root = Path.Combine(Path.GetTempPath(), "dbtool-t39-metadata-" + Guid.NewGuid().ToString("N"));
        async Task<(string?, string?)> Run(string destination)
        {
            Assert.Equal(0, await HeadlessRunner.RunAsync(Args(input, destination), new StringWriter(), default));
            using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory.GetDirectories(destination).Single(), "package.json")));
            return (json.RootElement.GetProperty("InputHash").GetString(), json.RootElement.GetProperty("SourceWorkbookHash").GetString());
        }
        var before = await Run(Path.Combine(root, "before"));
        using (var book = new XLWorkbook(input))
        {
            book.Properties.Title = "Metadata only; calculation inputs unchanged";
            book.Save();
        }
        var after = await Run(Path.Combine(root, "after"));
        Assert.Equal(before.Item1, after.Item1);
        Assert.NotEqual(before.Item2, after.Item2);
        File.Delete(input);
    }

    [Fact]
    public async Task HelpAndMissingArgumentsHaveExplicitOutcomes()
    {
        Assert.Equal(0,await HeadlessRunner.RunAsync(["--help"],new StringWriter(),default));
        var text=new StringWriter();Assert.NotEqual(0,await HeadlessRunner.RunAsync([],text,default));Assert.Contains("CLI_ARGUMENT_INVALID",text.ToString());
    }

    internal static string[] Args(string input,string root)=>["--project","S1","--input",input,"--profile","GaThamLienKhoi","--sheet","GaThamLienKhoi",
        "--id","S1-MONO-001","--scope","BaseRebarOnly","--policy","S1-Analytic-1","--allow-experimental","true","--output",root];

    internal static string[] S3Args(string input, string root, Profile profile, string id) => ["--project", "S3", "--input", input,
        "--profile", profile.ToString(), "--sheet", profile.ToString(), "--id", id, "--scope", "FullRebar",
        "--policy", "S2Rebar-1", "--allow-experimental", "true", "--output", root];
}

