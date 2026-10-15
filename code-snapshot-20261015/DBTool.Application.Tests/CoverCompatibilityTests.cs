using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class CoverCompatibilityTests
{
    [Fact]
    public void PhysicalBoundaryAloneCannotProveCoverOpeningCompatibility()
    {
        var raw = ManholeValidatorTests.Raw();
        var config = raw.ConfigCells.SetItem("CoverType", ManholeValidatorTests.Cell("Gang", "C13", "CF.Ga"))
            .SetItem("CoverBlock", ManholeValidatorTests.Cell("C", "E13", "CF.Ga"));
        var cover = new CoverCatalogEntry("C", new(1050, 745), new(0, 0, 0), 0,
            CoverPlacementDatum.NeckTop, new(-525, -372.5, 525, 372.5),
            [ManholeValidatorTests.Cell(1050, "B4", "DBTool.Covers").Source]);
        var c = ManholeValidatorTests.Catalogs();
        var result = new ManholeValidator(new NumericCellReader(), new NumericPolicy("en-US", false, false)).Validate(
            new(raw.ProjectId, raw.WorkbookHash, raw.Profile, raw.MainSheet, raw.Rows, config,
                new Dictionary<string, IEnumerable<RawManholeRow>>(), []),
            new(c.Pipes, c.Angles, c.Hatches, c.Aliases, new Dictionary<string, CoverCatalogEntry> { ["C"] = cover }),
            new("S1", [ManholeValidatorTests.Id], CalculationScope.GeometryOnly, "S2Geometry-1", true),
            CalculationPolicy.Select("S2Geometry-1", true).Value!);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == "COVER_ASSEMBLY_METADATA_REQUIRED" &&
            d.Severity == DiagnosticSeverity.Block && d.Source?.CellAddress == "E13");
    }
}
