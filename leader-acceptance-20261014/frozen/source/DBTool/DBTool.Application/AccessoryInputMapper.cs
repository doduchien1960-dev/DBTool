using System.Globalization;
using System.Text.RegularExpressions;
using DBTool.Domain;

namespace DBTool.Application;

public static class AccessoryInputMapper
{
    public static OperationResult<AccessoryRebarInput> Parse(RawProjectInput raw, string id, NumericPolicy policy)
    {
        var diagnostics = new List<Diagnostic>();
        var sources = new List<SourceRef>();

        RawCell? Cell(string key) => raw.ConfigCells.GetValueOrDefault(key);
        var readings = new Dictionary<string, OperationResult<string>>();

        string RawString(RawCell? c)
        {
            if (c == null) return "";
            return c.Kind switch
            {
                RawCellKind.Text => c.Text?.Trim() ?? "",
                RawCellKind.Number => c.Number?.ToString("R", CultureInfo.InvariantCulture) ?? "",
                RawCellKind.Formula => c.CachedText?.Trim() ?? c.Formula ?? "",
                RawCellKind.Boolean => c.Boolean == true ? "Có" : "Không",
                RawCellKind.Error => c.Text?.Trim() ?? "#ERROR",
                _ => ""
            };
        }

        string Text(string key)
        {
            var c = Cell(key);
            if (c == null) return "";
            if (!readings.TryGetValue(key, out var result))
            {
                result = RawCellReader.ReadText(c, policy);
                readings.Add(key, result);
                diagnostics.AddRange(result.Diagnostics.Select(d => new Diagnostic(d.Code, d.Severity,
                    DiagnosticScope.Manhole, id, null, d.Source, d.Expected, d.Actual, d.Message)));
                sources.Add(c.Source);
                if (!result.Success) Error(key, c.Kind == RawCellKind.Error ? "Cell contains evaluation error." :
                    "Formula cached evaluation value is unavailable or invalid under the input policy.");
            }
            return result.Success ? result.Value! : "";
        }

        void Error(string key, string message) =>
            diagnostics.Add(new("ACCESSORY_INPUT_INVALID", DiagnosticSeverity.Block, DiagnosticScope.Manhole, id, null, Cell(key)?.Source, null, RawString(Cell(key)), message));

        bool Active(string key)
        {
            var c = Cell(key);
            if (c == null) return false;
            string text = Text(key);
            return text is not ("" or "0" or "Không" or "Khong") && !Regex.IsMatch(text, "^0D[0-9]+$", RegexOptions.IgnoreCase);
        }

        double Number(string key, bool positive = true)
        {
            var c = Cell(key);
            Text(key);
            if (c == null || !readings[key].Success) { if (c == null) Error(key, "Accessory dimension required."); return 0; }
            var parsed = RawCellReader.ReadNumber(c, policy);
            diagnostics.AddRange(parsed.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Block));
            if (!parsed.Success) return 0;
            var v = parsed.Value!.Value;
            if (positive ? v <= 0 : v < 0)
            {
                Error(key, "Finite valid accessory dimension required.");
                return 0;
            }
            sources.Add(Cell(key)!.Source);
            return v;
        }

        int Integer(string key, bool positive = true)
        {
            var v = Number(key, positive);
            if (v != Math.Truncate(v) || v > int.MaxValue) { Error(key, "Integer required."); return 0; }
            return (int)v;
        }

        SourceRef[] Refs(params string[] keys) => keys.Where(k => Cell(k) != null).Select(k => Cell(k)!.Source).ToArray();

        ParsedRebar? Rebar(string key, ParseContext context)
        {
            var cell = Cell(key);
            if (cell == null) { Error(key, "Accessory specification required."); return null; }
            var text = Text(key);
            if (!readings[key].Success) return null;
            sources.Add(cell.Source);
            var p = RebarSpecParser.Parse(text, context, cell.Source);
            diagnostics.AddRange(p.Diagnostics);
            return p.Value;
        }

        AnchorSpec? anchor = Active("Anchor") ? Rebar("Anchor", ParseContext.AnchorOptional)?.Anchor : null;
        DowelSpec? dowel = null;
        double? dowelHole = null;
        if (Active("HasDowel"))
        {
            if (Text("HasDowel") != "Có") Error("HasDowel", "Expected Có/Không.");
            var mesh = Rebar("DowelSpec", ParseContext.MeshRequired)?.Mesh;
            var length = Number("DowelLength");
            var hole = Number("DowelHoleDia");
            if (mesh != null && hole <= mesh.DiameterMm) Error("DowelHoleDia", "Dowel hole must exceed bar diameter.");
            dowelHole = hole;
            if (mesh != null && length > 0) dowel = new(mesh.SpacingMm, mesh.DiameterMm, length, Round50Mode.ResolvedVhgRule, Refs("HasDowel", "DowelSpec", "DowelLength", "DowelHoleDia"));
        }

        VhgLiftingInput? lifting = null;
        if (Active("LiftingSpec"))
        {
            var spec = Rebar("LiftingSpec", ParseContext.LiftingHook)?.Counted;
            var parts = Regex.Split(Text("LiftingSize"), "[xX×]");
            if (parts.Length != 3) Error("LiftingSize", "Expected width x height x foot.");
            else
            {
                var dims = parts.Select(p => RawCellReader.ReadNumber(new(RawCellKind.Text, p, null, null, null, null, Cell("LiftingSize")!.Source), policy))
                    .Select(p => p.Success && p.Value!.Value > 0 ? p.Value.Value : 0).ToArray();
                if (dims.Any(d => d <= 0)) Error("LiftingSize", "Positive lifting dimensions required.");
                if (spec != null && spec.Count is not (2 or 4)) Error("LiftingSpec", "Lifting count must be 2 or 4.");
                if (spec != null && spec.Count is 2 or 4 && dims.All(d => d > 0)) lifting = new(spec.Count, spec.DiameterMm, dims[0], dims[1], dims[2], Refs("LiftingSpec", "LiftingSize"));
            }
        }

        VhgTieInput? ties = null;
        if (Active("Ties.H"))
        {
            var mesh = Rebar("Ties.H", ParseContext.MeshRequired)?.Mesh;
            var shapes = new Dictionary<ComponentKind, VhgTieShape>();
            foreach (var (kind, key) in new[] { (ComponentKind.Base, "Ties.D"), (ComponentKind.Wall, "Ties.E"), (ComponentKind.Roof, "Ties.F"), (ComponentKind.Neck, "Ties.G") })
            {
                var shape = Text(key) switch { "Chữ C" => VhgTieShape.C, "Chữ Z" => VhgTieShape.Z, "Chữ U" => VhgTieShape.U, _ => (VhgTieShape)(-1) };
                if ((int)shape < 0) Error(key, "Expected Chữ C/Z/U."); else shapes[kind] = shape;
            }
            if (Text("Ties.C") is not ("Đều" or "Lệch")) Error("Ties.C", "Expected Đều/Lệch.");
            var c = Number("Ties.J");
            var zu = Number("Ties.K");
            var middle = Number("Ties.L");
            if (mesh != null && shapes.Count == 4) ties = new(mesh, Text("Ties.C") == "Lệch", shapes, c, zu, middle, Refs("Ties.C", "Ties.D", "Ties.E", "Ties.F", "Ties.G", "Ties.H", "Ties.J", "Ties.K", "Ties.L"));
        }

        VhgOpeningInput? Opening(bool roof)
        {
            string d = roof ? "RoofRebar.C" : "Pipe.D", count = roof ? "RoofRebar.E" : "Pipe.F", shape = roof ? "RoofRebar.D" : "Pipe.E", ext = roof ? "RoofRebar.F" : "Pipe.G", cover = roof ? "CoverTop" : "Protect.H";
            if (!Active(d)) return null;
            int diameter = Integer(d), n = Integer(count, false);
            var expansion = Number(ext, false);
            var lap = Number("Ties.I", false);
            var c = Number(cover, false);
            var composition = Text(shape) switch
            {
                "Bo" => VhgOpeningComposition.Bo,
                "Vuông" => VhgOpeningComposition.Square,
                "Xiên" => VhgOpeningComposition.Diagonal,
                "Bo+Vuông" => VhgOpeningComposition.BoSquare,
                "Bo+Xiên" => VhgOpeningComposition.BoDiagonal,
                _ => (VhgOpeningComposition)(-1)
            };
            if (!Enum.IsDefined(composition)) Error(shape, "Unknown opening reinforcement shape.");
            if (roof && composition is VhgOpeningComposition.Diagonal or VhgOpeningComposition.BoDiagonal)
                Error(shape, "Roof opening supports Bo/Vuông/Bo+Vuông only (HDSD 2.3.14).");
            if (n == 0 && composition is VhgOpeningComposition.Square or VhgOpeningComposition.Diagonal) return null;
            if (diameter > 0 && Enum.IsDefined(composition)) return new(diameter, n, expansion, lap, c, composition, Refs(d, count, shape, ext, "Ties.I", cover));
            return null;
        }

        var pipe = Opening(false);
        var roof = Opening(true);

        double? lapMultiple = null;
        if (Active("Ties.I"))
        {
            lapMultiple = Number("Ties.I", false);
        }

        MeshSpec? catchMesh = null;
        if (Active("Catch.M"))
        {
            catchMesh = Rebar("Catch.M", ParseContext.MeshRequired)?.Mesh;
        }

        var prefixes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, role) in new[] {
            ("Annotation.Prefix.C", "BaseBottom"),
            ("Annotation.Prefix.D", "BaseTop"),
            ("Annotation.Prefix.E", "WallOuter"),
            ("Annotation.Prefix.F", "WallInner"),
            ("Annotation.Prefix.G", "RoofTop"),
            ("Annotation.Prefix.H", "RoofBottom"),
            ("Annotation.Prefix.I", "NeckOuter"),
            ("Annotation.Prefix.J", "NeckInner")
        })
        {
            if (Cell(key) is not null)
            {
                string pfx = Text(key);
                if (!string.IsNullOrWhiteSpace(pfx))
                {
                    prefixes[role] = pfx;
                    sources.Add(Cell(key)!.Source);
                }
            }
        }

        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Block)) return new(false, null, diagnostics);
        return new(true, new AccessoryRebarInput(anchor, dowel, lifting, ties, pipe, roof, sources.Concat(Refs("Anchor")), dowelHole, lapMultiple, catchMesh, prefixes), diagnostics);
    }
}
