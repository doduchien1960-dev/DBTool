using System.Globalization;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;

namespace DBTool.Infrastructure.Excel;

public sealed class CatalogReader : ICatalogReader
{
    private readonly NumericCellReader _numbers = new();

    public OperationResult<CatalogSet> Read(RawProjectInput input, NumericPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(policy);
        var diagnostics = new List<Diagnostic>();
        var textReadings = new Dictionary<RawCell, OperationResult<string>>();
        string Text(RawCell? cell)
        {
            if (cell is null) return "";
            if (!textReadings.TryGetValue(cell, out var result))
            {
                result = RawCellReader.ReadText(cell, policy);
                textReadings.Add(cell, result);
                diagnostics.AddRange(result.Diagnostics);
            }
            return result.Success ? result.Value! : "";
        }
        bool HasValue(RawCell? cell) => cell is not null &&
            (cell.Kind == RawCellKind.Formula || cell.Kind != RawCellKind.Blank && !IsNone(Text(cell)));
        var pipes = new Dictionary<string,PipeCatalogEntry>(StringComparer.Ordinal);
        var angles = new Dictionary<string,SteelAngleEntry>(StringComparer.Ordinal);
        var hatches = new Dictionary<string,HatchEntry>(StringComparer.Ordinal);
        var aliases = new Dictionary<string,string>(StringComparer.Ordinal);
        var covers = new Dictionary<string,CoverCatalogEntry>(StringComparer.Ordinal);

        if (!input.CatalogCells.TryGetValue("CF.Cong",out var pipeRows) ||
            !input.CatalogCells.TryGetValue("CF.Data",out var dataRows))
            return Fail("CATALOG_SHEET_MISSING",null);

        foreach (var cell in pipeRows.Concat(dataRows).SelectMany(row=>row.Cells.Values))
            if (cell.Kind == RawCellKind.Error) diagnostics.Add(Issue("CELL_ERROR",cell.Source));
        foreach (var row in pipeRows)
            if (Cell(row,Vhg32Schema.PipeCatalogColumns["DoubleFlag"]) is { Kind: RawCellKind.Formula } flag)
                diagnostics.Add(Issue("CATALOG_FLAG_INVALID",flag.Source));
        foreach (var row in dataRows)
            foreach (string column in new[] { Vhg32Schema.DataCatalogColumns["AngleCode"],Vhg32Schema.DataCatalogColumns["HatchName"] })
                if (Cell(row,column) is { Kind: RawCellKind.Formula } code)
                    diagnostics.Add(Issue("CATALOG_CODE_FORMULA_UNSUPPORTED",code.Source));
        if (diagnostics.Count > 0) return new(false,null,diagnostics);

        foreach (var row in pipeRows)
        {
            var round = Cell(row,Vhg32Schema.PipeCatalogColumns["RoundDiameter"]);
            var roundWall = Cell(row,Vhg32Schema.PipeCatalogColumns["RoundThickness"]);
            var box = Cell(row,Vhg32Schema.PipeCatalogColumns["BoxWidth"]);
            var boxHeight = Cell(row,Vhg32Schema.PipeCatalogColumns["BoxHeight"]);
            var boxWall = Cell(row,Vhg32Schema.PipeCatalogColumns["BoxThickness"]);
            var chamfer = Cell(row,Vhg32Schema.PipeCatalogColumns["BoxChamfer"]);
            var doubleFlag = Cell(row,Vhg32Schema.PipeCatalogColumns["DoubleFlag"]);
            bool hasRound = HasValue(round);
            bool hasBox = HasValue(box);
            if (!hasRound && HasValue(roundWall)) diagnostics.Add(Issue("CATALOG_DIMENSION_INVALID",roundWall?.Source));
            if (!hasBox && (HasValue(boxHeight) || HasValue(boxWall))) diagnostics.Add(Issue("CATALOG_DIMENSION_INVALID",boxHeight?.Source ?? boxWall?.Source));
            if (hasRound)
            {
                double? diameter = Positive(round,policy,diagnostics);
                double? thickness = Nonnegative(roundWall,policy,diagnostics);
                if (diameter.HasValue && thickness.HasValue)
                {
                    string size = CodeNumber(diameter.Value);
                    string code = "D"+size;
                    if (pipes.ContainsKey(code)) diagnostics.Add(Issue("CATALOG_DUPLICATE",round!.Source));
                    else
                    {
                        pipes.Add(code,new PipeCatalogEntry(code,PipeKind.Round,diameter,null,null,thickness.Value,null,0));
                        aliases[size]=code;
                    }
                }
            }
            if (hasBox)
            {
                double? width = Positive(box,policy,diagnostics);
                double? height = Positive(boxHeight,policy,diagnostics);
                double? thickness = Nonnegative(boxWall,policy,diagnostics);
                double? bevel = Nonnegative(chamfer,policy,diagnostics);
                string flag = Text(doubleFlag);
                bool isDouble = flag.Equals("Có",StringComparison.OrdinalIgnoreCase) ||
                    flag.Equals("Co",StringComparison.OrdinalIgnoreCase);
                if (!isDouble && flag != "" && flag != "-" && !flag.Equals("Không",StringComparison.OrdinalIgnoreCase))
                    diagnostics.Add(Issue("CATALOG_FLAG_INVALID",doubleFlag?.Source));
                if (width.HasValue && height.HasValue && thickness.HasValue && bevel.HasValue)
                {
                    string code = CodeNumber(width.Value)+"x"+CodeNumber(height.Value);
                    if (pipes.ContainsKey(code)) diagnostics.Add(Issue("CATALOG_DUPLICATE",box!.Source));
                    else
                    {
                        try
                        {
                            pipes.Add(code,new PipeCatalogEntry(code,isDouble ? PipeKind.DoubleBox : PipeKind.Box,
                                null,width,height,thickness.Value,null,bevel.Value));
                            aliases["H"+code]=code;
                            aliases["B"+code]=code;
                        }
                        catch (ArgumentException) { diagnostics.Add(Issue("CATALOG_DIMENSION_INVALID",box!.Source)); }
                    }
                }
            }
        }

        foreach (var row in dataRows)
        {
            var angle = Cell(row,Vhg32Schema.DataCatalogColumns["AngleCode"]);
            var weight = Cell(row,Vhg32Schema.DataCatalogColumns["KgPerMetre"]);
            var hatch = Cell(row,Vhg32Schema.DataCatalogColumns["HatchName"]);
            var scale = Cell(row,Vhg32Schema.DataCatalogColumns["HatchScale"]);
            string angleCode = Text(angle);
            string hatchName = Text(hatch);
            if (angleCode.Length == 0 && HasValue(weight)) diagnostics.Add(Issue("CATALOG_CODE_MISSING",weight?.Source));
            if (hatchName.Length == 0 && HasValue(scale)) diagnostics.Add(Issue("CATALOG_CODE_MISSING",scale?.Source));
            if (!IsNone(angleCode))
            {
                double? mass = Positive(weight,policy,diagnostics);
                if (mass.HasValue)
                {
                    if (angles.ContainsKey(angleCode)) diagnostics.Add(Issue("CATALOG_DUPLICATE",angle!.Source));
                    else angles.Add(angleCode,new SteelAngleEntry(angleCode,mass.Value));
                }
            }
            if (!IsNone(hatchName))
            {
                double? factor = Positive(scale,policy,diagnostics);
                if (factor.HasValue)
                {
                    if (hatches.ContainsKey(hatchName)) diagnostics.Add(Issue("CATALOG_DUPLICATE",hatch!.Source));
                    else hatches.Add(hatchName,new HatchEntry(hatchName,hatchName,factor.Value));
                }
            }
        }
        if(input.CatalogCells.TryGetValue("DBTool.Covers",out var coverRows))
            foreach(var row in coverRows)
            {
                RawCell? Field(string name)=>Cell(row,Vhg32Schema.CoverCatalogColumns[name]);
                var codeCell=Field("Code");
                string code=Text(codeCell);
                if(codeCell?.Kind!=RawCellKind.Text || string.IsNullOrWhiteSpace(code))
                { diagnostics.Add(Issue("COVER_CATALOG_CODE_REQUIRED",codeCell?.Source)); continue; }
                if(covers.Keys.Any(k=>k.Equals(code,StringComparison.OrdinalIgnoreCase)))
                { diagnostics.Add(Issue("CATALOG_DUPLICATE",codeCell.Source)); continue; }
                var units=Field("Units");
                if(units?.Kind!=RawCellKind.Text || Text(units)!="mm")
                    diagnostics.Add(Issue("COVER_CATALOG_UNITS_UNRESOLVED",units?.Source));
                var datumCell=Field("PlacementDatum");
                CoverPlacementDatum datum=Text(datumCell) switch
                { "NeckTop"=>CoverPlacementDatum.NeckTop,"SeatPlane"=>CoverPlacementDatum.SeatPlane,_=>(CoverPlacementDatum)(-1) };
                if(datumCell?.Kind!=RawCellKind.Text || !Enum.IsDefined(datum))
                    diagnostics.Add(Issue("COVER_CATALOG_DATUM_UNRESOLVED",datumCell?.Source));
                int before=diagnostics.Count;
                var values=new Dictionary<string,double>();
                foreach(string key in new[]{"WidthMm","LengthMm","OffsetX","OffsetY","OffsetZ","RotationDegrees","MinX","MinY","MaxX","MaxY"})
                {
                    var cell=Field(key);
                    if(cell is null || cell.Kind==RawCellKind.Blank)
                    { diagnostics.Add(Issue("COVER_CATALOG_FIELD_REQUIRED",cell?.Source ?? codeCell.Source)); continue; }
                    var value=key is "WidthMm" or "LengthMm" ? Positive(cell,policy,diagnostics) : Parse(cell,policy,diagnostics);
                    if(value.HasValue) values[key]=value.Value;
                }
                if(diagnostics.Skip(before).Any(d => d.Severity == DiagnosticSeverity.Block) || values.Count!=10 || !Enum.IsDefined(datum) || Text(units)!="mm") continue;
                CoverOpeningContract? openingContract = null;
                string[] assemblyKeys = ["ClearMinX", "ClearMinY", "ClearMaxX", "ClearMaxY", "MinimumBearingMm"];
                if (assemblyKeys.Any(k => Field(k) is { Kind: not RawCellKind.Blank }))
                {
                    var assemblyValues = new Dictionary<string, double>();
                    foreach (string key in assemblyKeys)
                    {
                        var cell = Field(key);
                        if (cell is null || cell.Kind == RawCellKind.Blank)
                        { diagnostics.Add(Issue("COVER_ASSEMBLY_FIELD_REQUIRED", cell?.Source ?? codeCell.Source)); continue; }
                        var value = key == "MinimumBearingMm" ? Positive(cell, policy, diagnostics) : Parse(cell, policy, diagnostics);
                        if (value.HasValue) assemblyValues[key] = value.Value;
                    }
                    if (assemblyValues.Count != 5) continue;
                    try
                    {
                        openingContract = new(new Rectangle2Mm(assemblyValues["ClearMinX"], assemblyValues["ClearMinY"],
                            assemblyValues["ClearMaxX"], assemblyValues["ClearMaxY"]), assemblyValues["MinimumBearingMm"],
                            assemblyKeys.Select(k => Field(k)!.Source));
                    }
                    catch (ArgumentException) { diagnostics.Add(Issue("COVER_ASSEMBLY_BOUNDARY_INVALID", Field("ClearMinX")?.Source)); continue; }
                }
                try
                {
                    covers.Add(code,new CoverCatalogEntry(code,new(values["WidthMm"],values["LengthMm"]),
                        new(values["OffsetX"],values["OffsetY"],values["OffsetZ"]),values["RotationDegrees"],datum,
                        new(values["MinX"],values["MinY"],values["MaxX"],values["MaxY"]),
                        Vhg32Schema.CoverCatalogColumns.Values.Where(row.Cells.ContainsKey).Select(c=>row.Cells[c].Source)
                            .Concat(input.ConfigCells.TryGetValue("DBTool.Schema!B1",out var declaration) ? new[]{declaration.Source} : Array.Empty<SourceRef>()),
                        openingContract));
                }
                catch(ArgumentException) { diagnostics.Add(Issue("COVER_CATALOG_BOUNDARY_INVALID",Field("MinX")?.Source)); }
            }
        foreach (var native in NativeCoverCatalog.Read())
            if (!covers.ContainsKey(native.Code)) covers.Add(native.Code,native);
        if (diagnostics.Any(d=>d.Severity==DiagnosticSeverity.Block)) return new(false,null,diagnostics);
        return new(true,new CatalogSet(pipes,angles,hatches,aliases,covers),diagnostics);
    }

    private double? Positive(RawCell? cell,NumericPolicy policy,List<Diagnostic> diagnostics)
    {
        double? value = Parse(cell,policy,diagnostics);
        if (value.HasValue && value.Value <= 0)
        { diagnostics.Add(Issue("CATALOG_DIMENSION_INVALID",cell!.Source)); return null; }
        return value;
    }

    private double? Nonnegative(RawCell? cell,NumericPolicy policy,List<Diagnostic> diagnostics)
    {
        double? value = Parse(cell,policy,diagnostics);
        if (value.HasValue && value.Value < 0)
        { diagnostics.Add(Issue("CATALOG_DIMENSION_INVALID",cell!.Source)); return null; }
        return value;
    }

    private double? Parse(RawCell? cell,NumericPolicy policy,List<Diagnostic> diagnostics)
    {
        if (cell is null) { diagnostics.Add(Issue("NUMBER_MISSING",null)); return null; }
        var result = _numbers.ReadNumber(cell,policy);
        diagnostics.AddRange(result.Diagnostics);
        return result.Value?.Value;
    }

    private static RawCell? Cell(RawManholeRow row,string column) => row.Cells.GetValueOrDefault(column);
    private static bool IsNone(string text) => string.IsNullOrWhiteSpace(text) || text.Equals("Không",StringComparison.OrdinalIgnoreCase);
    private static string CodeNumber(double value) => value.ToString("R",CultureInfo.InvariantCulture);
    private static Diagnostic Issue(string code,SourceRef? source) =>
        new(code,DiagnosticSeverity.Block,DiagnosticScope.Global,null,null,source,null,source?.RawText,code);
    private static OperationResult<CatalogSet> Fail(string code,SourceRef? source) =>
        new(false,null,new[] { Issue(code,source) });
}
