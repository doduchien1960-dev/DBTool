using ClosedXML.Excel;
using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using System.Text.Json;

namespace DBTool.Excel.Tests;

internal static class TestWorkbookFactory
{
    /// <summary>Explicit software assembly fixture; never evidence approving the native product.</summary>
    public static void DeclareSyntheticCover(XLWorkbook book, double clearX = 820, double clearY = 570)
    {
        var config = book.Worksheet("CF.Ga");
        config.Cell("E13").Value = "TEST.COVER";
        book.Worksheets.FirstOrDefault(s => s.Name == "DBTool.Schema")?.Delete();
        book.AddWorksheet("DBTool.Schema").Cell("B1").Value = "DBTool-Input-1A-v1";
        book.Worksheets.FirstOrDefault(s => s.Name == "DBTool.Covers")?.Delete();
        var cover = book.AddWorksheet("DBTool.Covers");
        cover.Cell("A4").Value = "TEST.COVER";
        cover.Cell("B4").Value = 1050; cover.Cell("C4").Value = 1050;
        cover.Cell("D4").Value = 0; cover.Cell("E4").Value = 0; cover.Cell("F4").Value = 0;
        cover.Cell("G4").Value = 0; cover.Cell("H4").Value = "NeckTop"; cover.Cell("I4").Value = "mm";
        cover.Cell("J4").Value = -525; cover.Cell("K4").Value = -525;
        cover.Cell("L4").Value = 525; cover.Cell("M4").Value = 525;
        cover.Cell("N4").Value = -clearX/2; cover.Cell("O4").Value = -clearY/2;
        cover.Cell("P4").Value = clearX/2; cover.Cell("Q4").Value = clearY/2;
        cover.Cell("R4").Value = 20;
    }

    public static string WorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName,"VHGpro 3.2","Demo","Excel")))
            current = current.Parent;
        return current?.FullName ?? throw new DirectoryNotFoundException("Workspace root was not found.");
    }

    public static string CopySource(int sourceNumber, Action<XLWorkbook>? edit = null)
    {
        string source = Directory.GetFiles(Path.Combine(WorkspaceRoot(),"VHGpro 3.2","Demo","Excel"),
            $"{sourceNumber}.0.*.xlsx").Single();
        string path = Path.Combine(Path.GetTempPath(), $"dbtool-source-{Guid.NewGuid():N}.xlsx");
        File.Copy(source,path);
        if (edit is not null)
        {
            using var book = new XLWorkbook(path);
            edit(book);
            book.Save();
        }
        return path;
    }

    public static string CreateS1FromJson()
    {
        string root = WorkspaceRoot();
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"DBTool","Tests","Fixtures","s1-mono-001.json")));
        var input = json.RootElement.GetProperty("input");
        string manifestPath = Path.Combine(root,"DBTool","DBTool.Excel.Tests","s1-workbook-fixture.manifest.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return Create(edit: book =>
        {
            var main = book.Worksheet(Profile.GaThamLienKhoi.ToString());
            var dimensions = input.GetProperty("dimensionsMm");
            var thickness = input.GetProperty("thicknessMm");
            main.Cell("B4").Value = json.RootElement.GetProperty("fixtureId").GetString();
            main.Cell("C4").Value = dimensions.GetProperty("length").GetDouble();
            main.Cell("D4").Value = dimensions.GetProperty("width").GetDouble();
            main.Cell("E4").Value = dimensions.GetProperty("height").GetDouble();
            main.Cell("F4").Value = thickness.GetProperty("base").GetDouble();
            main.Cell("I4").Value = thickness.GetProperty("wall").GetDouble();
            main.Cell("L4").Value = thickness.GetProperty("roof").GetDouble();
            main.Cell("W4").Value = input.GetProperty("neckHeightMm").GetDouble();
            main.Cell("AA4").Value = input.GetProperty("elevation").GetProperty("baseElevationM").GetDouble();
            main.Cell("O4").Value = input.GetProperty("pipeConnections")[0].GetProperty("catalogCode").GetString();
            main.Cell("P4").Value = input.GetProperty("pipeConnections")[0].GetProperty("invertMm").GetDouble();
            var pipeCatalog = book.Worksheet("CF.Cong");
            pipeCatalog.Cell("B4").Value = input.GetProperty("pipeConnections")[0].GetProperty("innerDiameterMm").GetDouble();
            pipeCatalog.Cell("C4").Value = input.GetProperty("pipeConnections")[0].GetProperty("pipeWallThicknessMm").GetDouble();
            string mesh = "D" + input.GetProperty("baseMesh").GetProperty("diameterMm").GetInt32() + "A" +
                input.GetProperty("baseMesh").GetProperty("spacingMm").GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
            main.Cell("G4").Value = mesh; main.Cell("H4").Value = mesh;
            var config = book.Worksheet("CF.Ga");
            config.Cell("C3").Value = input.GetProperty("expansionMm").GetProperty("pad").GetDouble();
            config.Cell("D3").Value = thickness.GetProperty("pad").GetDouble();
            config.Cell("C5").Value = input.GetProperty("expansionMm").GetProperty("base").GetDouble();
            config.Cell("D5").Value = input.GetProperty("haunchMm").GetProperty("base").GetDouble();
            config.Cell("C9").Value = input.GetProperty("haunchMm").GetProperty("top").GetDouble();
            config.Cell("E11").Value = "700x700";
            config.Cell("C17").Value = input.GetProperty("baseMesh").GetProperty("coverMm").GetDouble();
            foreach (var item in manifest.RootElement.GetProperty("explicitCells").EnumerateObject())
            {
                var parts = item.Name.Split('!');
                var cell = book.Worksheet(parts[0]).Cell(parts[1]);
                if (item.Value.ValueKind == JsonValueKind.Null) cell.Clear();
                else if (item.Value.ValueKind == JsonValueKind.Number) cell.Value = item.Value.GetDouble();
                else cell.Value = item.Value.GetString();
            }
        });
    }

    public static string CreateS2Synthetic(Profile profile, Action<XLWorkbook>? edit = null)
    {
        return Create(profile, edit: book =>
        {
            var main = book.Worksheet(profile.ToString());
            main.Cell("B4").Value = "G400-1";
            main.Cell("C4").Value = 1000;
            main.Cell("D4").Value = 1250;
            main.Cell("E4").Value = 1400;
            main.Cell("F4").Value = 150;
            main.Cell("G4").Value = "D10A150";
            main.Cell("H4").Value = "D10A150";
            main.Cell("I4").Value = 150;
            main.Cell("L4").Value = 150;
            main.Cell("W4").Value = 0;
            main.Cell("AA4").Value = 10;
            main.Cell("O4").Value = "D400";
            main.Cell("P4").Value = 300;
            var config = book.Worksheet("CF.Ga");
            var addresses = Vhg32Schema.ConfigAddresses(profile);
            void Set(string key, object value)
            {
                if (value is double number) config.Cell(addresses[key]).Value = number;
                else if (value is int integer) config.Cell(addresses[key]).Value = integer;
                else config.Cell(addresses[key]).Value = value.ToString();
            }
            Set("PadExpansion", 100);
            Set("PadThickness", 100);
            Set("BaseExpansion", 100);
            Set("BaseHaunch", 0);
            if (addresses.ContainsKey("TopHaunch")) Set("TopHaunch", 0);
            if (addresses.ContainsKey("BeddingMortar")) Set("BeddingMortar", 0);
            Set("NeckSize", "700x700");
            Set("NeckPosition", "Giữa ga");
            Set("CoverBottom", 30);
            Set("Distribution", "Chia đều");
            Set("HookPattern.C", "0M");
            Set("HookPattern.D", "0M");
            Set("PipeMortar", 50);
            Set("PipeOffsetT1", 0);
            Set("Elevation.C", "Tuyệt đối");
            var pipeCatalog = book.Worksheet("CF.Cong");
            pipeCatalog.Cell("B4").Value = 400;
            pipeCatalog.Cell("C4").Value = 55;
            edit?.Invoke(book);
        });
    }

    public static string Create(Profile profile = Profile.GaThamLienKhoi, Action<XLWorkbook>? edit = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbtool-{Guid.NewGuid():N}.xlsx");
        using var book = new XLWorkbook();
        var main = book.AddWorksheet(profile.ToString());
        main.Cell("B4").Value = "G400-1";
        main.Cell("C4").Value = 1000;
        main.Cell("D4").Value = 1100;
        main.Cell("E4").Value = 1200;
        main.Cell("G4").Value = "D10A150";
        main.Cell("H4").Value = "D10A150";
        main.Cell("W4").Value = 0;
        main.Cell("O4").Value = "D400";
        main.Cell("P4").Value = 300;
        var config = book.AddWorksheet("CF.Ga");
        config.Cell("C17").Value = 30;
        config.Cell("C19").Value = 47;
        book.AddWorksheet("CF.Cong");
        book.AddWorksheet("CF.Data");
        edit?.Invoke(book);
        book.SaveAs(path);
        return path;
    }
}
