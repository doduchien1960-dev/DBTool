using DBTool.Domain;
using DBTool.Infrastructure.Excel;
using Xunit;

namespace DBTool.Application.Tests;

public sealed class S2StructureInputTests
{
    [Theory]
    [InlineData("Gang",CoverKind.Purchased)]
    [InlineData(" gang ",CoverKind.Purchased)]
    [InlineData("BTCT",CoverKind.Panel)]
    [InlineData("Panel",CoverKind.Panel)]
    [InlineData("Không",CoverKind.None)]
    public void VhgCoverVocabularySelectsOnlyTheActiveKind(string text,CoverKind expected)
    { Assert.True(CoverKindMap.TryParse(text,out var kind)); Assert.Equal(expected,kind); }

    [Fact]
    public void UnknownCoverKindBlocksAtSourceRatherThanAssumingPurchasedOrInactive()
    {
        var raw=ManholeValidatorTests.Raw();
        var result=Validate(raw,raw.ConfigCells.SetItem("CoverType",ManholeValidatorTests.Cell("unknown","C13","CF.Ga")));
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="COVER_KIND_UNRESOLVED" && d.Source?.CellAddress=="C13");
    }

    [Fact]
    public void PurchasedCatalogWithoutPlacementProvenanceRemainsBlockedAtAssetCell()
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells.SetItem("CoverType",ManholeValidatorTests.Cell("Gang","C13","CF.Ga"))
            .SetItem("CoverBlock",ManholeValidatorTests.Cell("missing-cover","E13","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics,d=>d.Code=="U_NECK_COVER_CATALOG" && d.Source?.CellAddress=="E13"
            && d.Message.Contains("missing-cover"));
        Assert.DoesNotContain(result.Diagnostics,d=>d.Code.StartsWith("PANEL_",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Height",false)]
    [InlineData("RoofThickness",false)]
    [InlineData("NeckHeight",false)]
    [InlineData("NeckPosition",true)]
    public void PurchasedCoverWithMissingUpstreamInputReturnsBlockRatherThanThrowing(string key,bool configField)
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells.SetItem("CoverType",ManholeValidatorTests.Cell("Gang","C13","CF.Ga"))
            .SetItem("CoverBlock",ManholeValidatorTests.Cell("C","E13","CF.Ga"));
        var cells=raw.Rows[0].Cells;
        if(configField) config=config.Remove(key); else cells=cells.Remove(key);
        var c=ManholeValidatorTests.Catalogs();
        var cover=new CoverCatalogEntry("C",new(800,800),new(0,0,0),0,CoverPlacementDatum.NeckTop,
            new(-400,-400,400,400),[ManholeValidatorTests.Cell(800,"B4","DBTool.Covers").Source]);
        var result=new ManholeValidator(new NumericCellReader(),new NumericPolicy("en-US",false,false)).Validate(
            new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[new RawManholeRow(4,cells)],config,
                new Dictionary<string,IEnumerable<RawManholeRow>>(),[]),
            new CatalogSet(c.Pipes,c.Angles,c.Hatches,c.Aliases,new Dictionary<string,CoverCatalogEntry>{{"C",cover}}),
            new(raw.ProjectId,[ManholeValidatorTests.Id],CalculationScope.GeometryOnly,"S2Geometry-1",true),
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="INPUT_FIELD_MISSING" && d.Severity==DiagnosticSeverity.Block && d.Message.Contains(key));
    }
    [Fact]
    public void ExplicitCatchFieldsAreTypedAndChangeS2HashWithoutMergingHeightSources()
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells
            .SetItem("CatchOwner",ManholeValidatorTests.Cell("Body","A20","CF.Ga"))
            .SetItem("CatchWall",ManholeValidatorTests.Cell("T1","B20","CF.Ga"))
            .SetItem("CatchPosition",ManholeValidatorTests.Cell("Center","C20","CF.Ga"))
            .SetItem("CatchOpeningWidth",ManholeValidatorTests.Cell(400,"D20","CF.Ga"))
            .SetItem("CatchOpeningHeight",ManholeValidatorTests.Cell(300,"E20","CF.Ga"))
            .SetItem("CatchChamberWidth",ManholeValidatorTests.Cell(600,"F20","CF.Ga"))
            .SetItem("CatchChamberDepth",ManholeValidatorTests.Cell(500,"G20","CF.Ga"))
            .SetItem("CatchChamberHeight",ManholeValidatorTests.Cell(600,"H20","CF.Ga"))
            .SetItem("CatchWallThickness",ManholeValidatorTests.Cell(100,"I20","CF.Ga"))
            .SetItem("CatchBaseThickness",ManholeValidatorTests.Cell(100,"J20","CF.Ga"))
            .SetItem("CatchOffset",ManholeValidatorTests.Cell(0,"K20","CF.Ga"));
        var a=Validate(raw,config);
        Assert.True(a.Success);
        var catchSpec=a.Value!.SelectedManholes[0].CatchBasinSpec!;
        Assert.Equal(300,catchSpec.OpeningHeightMm);
        Assert.Equal(600,catchSpec.ChamberClearHeightMm);
        Assert.Equal("E20",catchSpec.SourceRefs["OpeningHeight"].CellAddress);
        Assert.Equal("H20",catchSpec.SourceRefs["ChamberHeight"].CellAddress);
        var b=Validate(raw,config.SetItem("CatchOpeningHeight",ManholeValidatorTests.Cell(350,"E20","CF.Ga")));
        Assert.True(b.Success);
        Assert.NotEqual(a.Value.InputHash,b.Value!.InputHash);
    }

    [Fact]
    public void CatchChamberTooShortBlocksWithChamberHeightSource()
    {
        var raw=ManholeValidatorTests.Raw();
        var config=CatchConfig(raw).SetItem("CatchChamberHeight",ManholeValidatorTests.Cell(200,"H20","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CATCH_CHAMBER_TOO_SHORT" &&
            d.Severity==DiagnosticSeverity.Block && d.Source?.CellAddress=="H20");
    }

    [Fact]
    public void NeckCatchLeftPositionBlocksAtPositionSource()
    {
        var raw=ManholeValidatorTests.Raw();
        var config=CatchConfig(raw)
            .SetItem("CatchOwner",ManholeValidatorTests.Cell("Neck","A20","CF.Ga"))
            .SetItem("CatchPosition",ManholeValidatorTests.Cell("Left","C20","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="CATCH_NECK_POSITION_UNSUPPORTED" &&
            d.Severity==DiagnosticSeverity.Block && d.Source?.CellAddress=="C20");
    }

    [Fact]
    public void ExplicitS2PipeIdChangesHashAndIsKeptTyped()
    {
        var raw=ManholeValidatorTests.Raw();
        var a=Validate(raw,raw.ConfigCells.SetItem("PipeIdT1",ManholeValidatorTests.Cell("Pipe/A","L20","CF.Ga")));
        var b=Validate(raw,raw.ConfigCells.SetItem("PipeIdT1",ManholeValidatorTests.Cell("Pipe/B","L20","CF.Ga")));
        Assert.True(a.Success);
        Assert.True(b.Success);
        Assert.Equal("Pipe/A",Assert.Single(a.Value!.SelectedManholes[0].Input.PipeConnections).Id);
        Assert.Equal("L20",a.Value.SelectedManholes[0].Input.PipeConnections[0].SourceRefs["Id"].CellAddress);
        Assert.NotEqual(a.Value.InputHash,b.Value!.InputHash);
    }

    [Fact]
    public void TwoActivePipesCannotShareOneExplicitId()
    {
        var raw=ManholeValidatorTests.Raw();
        var row=new RawManholeRow(4,raw.Rows[0].Cells
            .SetItem("PipeCodeT2",ManholeValidatorTests.Cell("D400","V4"))
            .SetItem("InvertT2",ManholeValidatorTests.Cell(300,"W4")));
        raw=new RawProjectInput(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[row],raw.ConfigCells,
            new Dictionary<string,IEnumerable<RawManholeRow>>(),[]);
        var config=raw.ConfigCells
            .SetItem("PipeIdT1",ManholeValidatorTests.Cell("Same","L20","CF.Ga"))
            .SetItem("PipeIdT2",ManholeValidatorTests.Cell("Same","M20","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="PIPE_ID_DUPLICATE" &&
            d.Severity==DiagnosticSeverity.Block && d.Source?.CellAddress=="M20");
    }

    private static System.Collections.Immutable.ImmutableDictionary<string,RawCell> CatchConfig(RawProjectInput raw) =>
        raw.ConfigCells.SetItem("CatchOwner",ManholeValidatorTests.Cell("Body","A20","CF.Ga"))
            .SetItem("CatchWall",ManholeValidatorTests.Cell("T1","B20","CF.Ga"))
            .SetItem("CatchPosition",ManholeValidatorTests.Cell("Center","C20","CF.Ga"))
            .SetItem("CatchOpeningWidth",ManholeValidatorTests.Cell(400,"D20","CF.Ga"))
            .SetItem("CatchOpeningHeight",ManholeValidatorTests.Cell(300,"E20","CF.Ga"))
            .SetItem("CatchChamberWidth",ManholeValidatorTests.Cell(600,"F20","CF.Ga"))
            .SetItem("CatchChamberDepth",ManholeValidatorTests.Cell(500,"G20","CF.Ga"))
            .SetItem("CatchChamberHeight",ManholeValidatorTests.Cell(600,"H20","CF.Ga"))
            .SetItem("CatchWallThickness",ManholeValidatorTests.Cell(100,"I20","CF.Ga"))
            .SetItem("CatchBaseThickness",ManholeValidatorTests.Cell(100,"J20","CF.Ga"))
            .SetItem("CatchOffset",ManholeValidatorTests.Cell(0,"K20","CF.Ga"));

    [Fact]
    public void BlankPanelCountIsDifferentFromZeroAndBothAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CoverPanelSpec(0, 5, SplitAxis.X, PanelSupportMode.Recess, 100, []));
        var raw = ManholeValidatorTests.Raw();
        var blank = raw.ConfigCells.SetItem("PanelCount", ManholeValidatorTests.Cell(null, "I13", "CF.Ga"))
            .SetItem("CoverType",ManholeValidatorTests.Cell("Panel","C13","CF.Ga"));
        Assert.Null(Validate(raw, blank).Value);
        Assert.Contains(Validate(raw, blank).Diagnostics, d => d.Code == "PANEL_COUNT_REQUIRED");
    }

    [Fact]
    public void NineNamedPositionsHaveDistinctCentersAndRoundTrip()
    {
        var expected = new (NeckPosition Position, double X, double Y)[]
        {
            (NeckPosition.Center,0,0), (NeckPosition.North,0,325), (NeckPosition.East,150,0),
            (NeckPosition.South,0,-325), (NeckPosition.West,-150,0),
            (NeckPosition.NorthEast,150,325), (NeckPosition.SouthEast,150,-325),
            (NeckPosition.SouthWest,-150,-325), (NeckPosition.NorthWest,-150,325)
        };
        foreach (var (position,x,y) in expected)
        {
            Assert.True(NeckPositionMap.TryParse(NeckPositionMap.Name(position), out var parsed));
            Assert.Equal(position,parsed);
            Assert.Equal((x,y),NeckPositionMap.Center(position,1000,1250,700,600));
        }
    }

    [Fact]
    public void LegacyPositionAndPanelLabelsMapByName()
    {
        var labels=new (string,NeckPosition)[]
        {
            ("Giữa ga",NeckPosition.Center),("Giữa T1",NeckPosition.North),("Giữa T2",NeckPosition.East),
            ("Giữa T3",NeckPosition.South),("Giữa T4",NeckPosition.West),
            ("Góc T1-T2",NeckPosition.NorthEast),("Góc T2-T3",NeckPosition.SouthEast),
            ("Góc T3-T4",NeckPosition.SouthWest),("Góc T4-T1",NeckPosition.NorthWest)
        };
        foreach(var (label,expected) in labels)
        { Assert.True(NeckPositionMap.TryParse(label,out var actual)); Assert.Equal(expected,actual); }
        Assert.True(PanelDirectionMap.TryParse("Dọc",out var vertical)); Assert.Equal(SplitAxis.X,vertical);
        Assert.True(PanelDirectionMap.TryParse("Ngang",out var horizontal)); Assert.Equal(SplitAxis.Y,horizontal);
    }

    [Fact]
    public void SeatChangesInputHashButNotManholeIdAndScaleDoesNotChangeHash()
    {
        var raw=ManholeValidatorTests.Raw();
        var row=new RawManholeRow(4,raw.Rows[0].Cells.SetItem("NeckHeight",ManholeValidatorTests.Cell(600,"W4"))
            .SetItem("NeckThickness",ManholeValidatorTests.Cell(100,"X4")));
        raw=new RawProjectInput(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[row],raw.ConfigCells,
            new Dictionary<string,IEnumerable<RawManholeRow>>(),[]);
        var a=Validate(raw, raw.ConfigCells.SetItem("NeckSeatKind",ManholeValidatorTests.Cell("None","Z11","CF.Ga"))
            .SetItem("NeckSeatElevation",ManholeValidatorTests.Cell(2150,"AC11","CF.Ga"))).Value!;
        var b=Validate(raw, raw.ConfigCells.SetItem("NeckSeatKind",ManholeValidatorTests.Cell("Step","Z11","CF.Ga"))
            .SetItem("NeckSeatExpansion",ManholeValidatorTests.Cell(50,"AA11","CF.Ga"))
            .SetItem("NeckSeatHeight",ManholeValidatorTests.Cell(50,"AB11","CF.Ga"))
            .SetItem("NeckSeatElevation",ManholeValidatorTests.Cell(2100,"AC11","CF.Ga"))).Value!;
        Assert.Equal(a.SelectedManholes[0].Input.Id,b.SelectedManholes[0].Input.Id);
        Assert.NotEqual(a.InputHash,b.InputHash);
        Assert.Equal(["Z11","AC11","AA11","AB11"],b.SelectedManholes[0].NeckSeatSpec!.Sources.Select(s=>s.CellAddress));
        var scale=raw.ConfigCells.SetItem("DrawingScale",ManholeValidatorTests.Cell(30,"Z1","CF.Ga"));
        Assert.Equal(a.InputHash,Validate(raw,scale.SetItem("NeckSeatKind",ManholeValidatorTests.Cell("None","Z11","CF.Ga"))
            .SetItem("NeckSeatElevation",ManholeValidatorTests.Cell(2150,"AC11","CF.Ga"))).Value!.InputHash);
    }

    [Fact]
    public void SeatWithoutExplicitElevationBlocksEvenWhenKindNone()
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells.SetItem("NeckSeatKind",ManholeValidatorTests.Cell("None","Z11","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="NECK_SEAT_ELEVATION_REQUIRED");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2.5)]
    public void RawPanelCountMustBePositiveInteger(double value)
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells.SetItem("CoverType",ManholeValidatorTests.Cell("Panel","C13","CF.Ga"))
            .SetItem("PanelCount",ManholeValidatorTests.Cell(value,"I13","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="PANEL_COUNT_INVALID" && d.Source?.CellAddress=="I13");
    }

    [Fact]
    public void SeatCannotConsumeAllNeckWall()
    {
        var raw=ManholeValidatorTests.Raw();
        var row=new RawManholeRow(4,raw.Rows[0].Cells.SetItem("NeckHeight",ManholeValidatorTests.Cell(600,"W4"))
            .SetItem("NeckThickness",ManholeValidatorTests.Cell(50,"X4")));
        raw=new RawProjectInput(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[row],raw.ConfigCells,
            new Dictionary<string,IEnumerable<RawManholeRow>>(),[]);
        var config=raw.ConfigCells.SetItem("NeckSeatKind",ManholeValidatorTests.Cell("Step","Z11","CF.Ga"))
            .SetItem("NeckSeatExpansion",ManholeValidatorTests.Cell(50,"AA11","CF.Ga"))
            .SetItem("NeckSeatHeight",ManholeValidatorTests.Cell(50,"AB11","CF.Ga"))
            .SetItem("NeckSeatElevation",ManholeValidatorTests.Cell(2100,"AC11","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="NECK_SEAT_WALL_INVALID");
    }

    [Fact]
    public void ActiveUnmappedNeckHaunchSwitchCannotDisappearIntoUnsupportedRawFields()
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells.SetItem("NeckHaunchFlag",ManholeValidatorTests.Cell("Có","F11","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="NECK_SEAT_REQUIRES_NECK" && d.Source?.CellAddress=="F11");
    }

    [Fact]
    public void NegativePanelHeightBlocksBeforeLayout()
    {
        var raw=ManholeValidatorTests.Raw();
        var config=raw.ConfigCells.SetItem("CoverType",ManholeValidatorTests.Cell("Panel","C13","CF.Ga"))
            .SetItem("PanelCount",ManholeValidatorTests.Cell(2,"I13","CF.Ga"))
            .SetItem("PanelGap",ManholeValidatorTests.Cell(5,"J13","CF.Ga"))
            .SetItem("PanelDirection",ManholeValidatorTests.Cell("Dọc","K13","CF.Ga"))
            .SetItem("PanelSupportMode",ManholeValidatorTests.Cell("Recess","Z13","CF.Ga"))
            .SetItem("CoverHeight",ManholeValidatorTests.Cell(-1,"D13","CF.Ga"));
        var result=Validate(raw,config);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="DIMENSION_NONPOSITIVE" && d.Source?.CellAddress=="D13");
    }

    [Fact]
    public void NumericEnumOrdinalsAreNotAcceptedAsSeatOrSupportNames()
    {
        var raw=ManholeValidatorTests.Raw();
        var seat=Validate(raw,raw.ConfigCells.SetItem("NeckSeatKind",ManholeValidatorTests.Cell("0","Z11","CF.Ga"))
            .SetItem("NeckSeatElevation",ManholeValidatorTests.Cell(1550,"AC11","CF.Ga")));
        Assert.False(seat.Success); Assert.Contains(seat.Diagnostics,d=>d.Code=="NECK_SEAT_KIND_INVALID");
        var panel=Validate(raw,raw.ConfigCells.SetItem("CoverType",ManholeValidatorTests.Cell("Panel","C13","CF.Ga"))
            .SetItem("PanelCount",ManholeValidatorTests.Cell(2,"I13","CF.Ga"))
            .SetItem("PanelGap",ManholeValidatorTests.Cell(5,"J13","CF.Ga"))
            .SetItem("PanelDirection",ManholeValidatorTests.Cell("Dọc","K13","CF.Ga"))
            .SetItem("PanelSupportMode",ManholeValidatorTests.Cell("0","Z13","CF.Ga"))
            .SetItem("CoverHeight",ManholeValidatorTests.Cell(100,"D13","CF.Ga")));
        Assert.False(panel.Success); Assert.Contains(panel.Diagnostics,d=>d.Code=="PANEL_SUPPORT_UNRESOLVED");
    }

    [Fact]
    public void ActiveOutOfScopeFeatureKeepsRawCellAndReviewDiagnostic()
    {
        var raw=ManholeValidatorTests.Raw();
        var row=new RawManholeRow(4,raw.Rows[0].Cells.SetItem("WallOuterMesh",ManholeValidatorTests.Cell("D10A150","J4")));
        raw=new RawProjectInput(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,[row],raw.ConfigCells,
            new Dictionary<string,IEnumerable<RawManholeRow>>(),[]);
        var result=Validate(raw,raw.ConfigCells);
        Assert.True(result.Success);
        Assert.True(result.Value!.SelectedManholes[0].Input.UnsupportedRawFields.ContainsKey("Row.WallOuterMesh"));
        Assert.Contains(result.Diagnostics,d=>d.Code=="S2_FEATURE_DEFERRED" && d.Severity==DiagnosticSeverity.Review && d.Source?.CellAddress=="J4");
    }

    [Fact]
    public void GeometryOnlyAbsoluteStillRequiresBaseElevation()
    {
        var raw=ManholeValidatorTests.Raw("BaseElevationM",null);
        var result=Validate(raw,raw.ConfigCells);
        Assert.False(result.Success); Assert.Null(result.Value);
        Assert.Contains(result.Diagnostics,d=>d.Code=="ELEVATION_REQUIRED" && d.Source?.CellAddress=="AA4");
    }

    private static OperationResult<ValidatedProjectInput> Validate(RawProjectInput raw, System.Collections.Immutable.ImmutableDictionary<string,RawCell> config)
    {
        var name=raw.Rows[0].Cells["Name"].Text!;
        var id=ManholeIdentity.Create(raw.ProjectId,raw.Profile,name);
        return new ManholeValidator(new NumericCellReader(),new NumericPolicy("en-US",false,false)).Validate(
            new(raw.ProjectId,raw.WorkbookHash,raw.Profile,raw.MainSheet,raw.Rows,config,new Dictionary<string,IEnumerable<RawManholeRow>>(),[]),
            ManholeValidatorTests.Catalogs(),new(raw.ProjectId,[id],CalculationScope.GeometryOnly,"S2Geometry-1",true),
            CalculationPolicy.Select("S2Geometry-1",true).Value!);
    }
}
