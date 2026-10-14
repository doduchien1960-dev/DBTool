using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Colors;
using DBTool.Application.Templates;

namespace DBTool.AutoCAD.Template;

/// <summary>Handle-independent native symbol properties. All reads stay inside the caller transaction.</summary>
public static class NativeAssetSignatures
{
    public const string CanonicalBundleVersion = "leader-field-contracts-20261013-r2";
    public const string CustomizationVersion = "VHGpro-20261013";

    public static TemplateAssetSignature DimensionProperties(Transaction tr, Dimension dimension)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in typeof(Dimension).GetProperties().Where(p =>
            p.Name.StartsWith("Dim", StringComparison.Ordinal) && p.Name != "DimBlockId" && p.Name != "DimensionBlockId" &&
            p.CanRead && p.GetIndexParameters().Length == 0))
            values.Add(property.Name, Format(tr, property.GetValue(dimension)));
        values["CanonicalVersion"] = CanonicalBundleVersion;
        return new(values);
    }

    public static TemplateAssetSignature ReadBlock(Transaction tr, BlockTableRecord btr)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        values["Name"] = btr.Name;
        values["Origin"] = $"{btr.Origin.X.ToString("R", CultureInfo.InvariantCulture)},{btr.Origin.Y.ToString("R", CultureInfo.InvariantCulture)},{btr.Origin.Z.ToString("R", CultureInfo.InvariantCulture)}";
        values["CanonicalVersion"] = CanonicalBundleVersion;
        var tagList = new List<string>();
        foreach (ObjectId id in btr)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is AttributeDefinition att)
            {
                tagList.Add(att.Tag);
                values[$"AttDef/{att.Tag}/Height"] = att.Height.ToString("R", CultureInfo.InvariantCulture);
                values[$"AttDef/{att.Tag}/Style"] = tr.GetObject(att.TextStyleId, OpenMode.ForRead) is TextStyleTableRecord st ? st.Name : "";
            }
        }
        tagList.Sort(StringComparer.Ordinal);
        values["AttributeTags"] = string.Join(",", tagList);
        return new(values);
    }

    public static TemplateAssetSignature Read(Transaction tr, DBObject record)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string key, object? value) => values.Add(key, Format(tr, value));
        void ReadApplicable(string key, Func<object?> read)
        {
            try { Add(key, read()); }
            catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.NotApplicable)
            { values.Add(key, "<not-applicable>"); }
        }
        values["CanonicalVersion"] = CanonicalBundleVersion;
        switch (record)
        {
            case BlockTableRecord btr:
                return ReadBlock(tr, btr);
            case LayerTableRecord layer:
                Add("Color", layer.Color); Add("ColorIndex", layer.Color.ColorIndex); Add("Linetype", layer.LinetypeObjectId);
                Add("LineWeight", layer.LineWeight); Add("IsPlottable", layer.IsPlottable);
                Add("IsOff", layer.IsOff); Add("IsFrozen", layer.IsFrozen); Add("IsLocked", layer.IsLocked);
                Add("Transparency/ByLayer", layer.Transparency.IsByLayer); Add("Transparency/ByBlock", layer.Transparency.IsByBlock);
                Add("Transparency/Alpha", layer.Transparency.IsByAlpha ? layer.Transparency.Alpha.ToString(CultureInfo.InvariantCulture) : "<not-alpha>");
                break;
            case TextStyleTableRecord text:
                Add("FileName", text.FileName.ToLowerInvariant()); Add("BigFontFileName", text.BigFontFileName.ToLowerInvariant());
                Add("XScale", text.XScale); Add("TextSize", text.TextSize); Add("ObliquingAngle", text.ObliquingAngle);
                Add("IsVertical", text.IsVertical); Add("FlagBits", text.FlagBits);
                var font = text.Font;
                Add("Font/Typeface", font.TypeFace); Add("Font/Bold", font.Bold); Add("Font/Italic", font.Italic);
                Add("Font/CharacterSet", font.CharacterSet); Add("Font/PitchAndFamily", font.PitchAndFamily);
                break;
            case DimStyleTableRecord dim:
                Add("Dimclrd/ColorIndex", dim.Dimclrd.ColorIndex); Add("Dimclre/ColorIndex", dim.Dimclre.ColorIndex);
                Add("Dimclrt/ColorIndex", dim.Dimclrt.ColorIndex);
                foreach (var property in typeof(DimStyleTableRecord).GetProperties()
                    .Where(p => p.Name.StartsWith("Dim", StringComparison.Ordinal) && p.CanRead && p.GetIndexParameters().Length == 0))
                    Add(property.Name, property.GetValue(dim));
                break;
            case LinetypeTableRecord line:
                Add("PatternLength", line.PatternLength); Add("NumDashes", line.NumDashes);
                for (var i = 0; i < line.NumDashes; i++)
                {
                    Add($"Dash/{i}/Length", line.DashLengthAt(i)); ReadApplicable($"Dash/{i}/Text", () => line.TextAt(i));
                    ReadApplicable($"Dash/{i}/Style", () => line.ShapeStyleAt(i)); ReadApplicable($"Dash/{i}/Number", () => line.ShapeNumberAt(i));
                    ReadApplicable($"Dash/{i}/Scale", () => line.ShapeScaleAt(i)); ReadApplicable($"Dash/{i}/Rotation", () => line.ShapeRotationAt(i));
                    ReadApplicable($"Dash/{i}/Offset", () => line.ShapeOffsetAt(i)); ReadApplicable($"Dash/{i}/Ucs", () => line.ShapeIsUcsOrientedAt(i));
                }
                break;
            default:
                throw new InvalidOperationException($"Native signature unsupported: {record.GetType().Name}");
        }
        return new(values);
    }

    private static string Format(Transaction tr, object? value) => value switch
    {
        null => "",
        ObjectId id when id.IsNull => "null",
        ObjectId id => tr.GetObject(id, OpenMode.ForRead) is SymbolTableRecord symbol ? symbol.Name :
            throw new InvalidOperationException("Signature references a non-symbol native object."),
        Color color => $"{color.ColorMethod}:{color.ColorIndex}:{color.Red},{color.Green},{color.Blue}",
        double number => number.ToString("R", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    public static TemplateAssetSignature? Find(Database db, Transaction tr, AssetKind kind, string name)
    {
        ObjectId tableId = kind switch
        {
            AssetKind.Layer => db.LayerTableId, AssetKind.TextStyle => db.TextStyleTableId,
            AssetKind.DimStyle => db.DimStyleTableId, AssetKind.Linetype => db.LinetypeTableId,
            AssetKind.Block => db.BlockTableId,
            _ => ObjectId.Null
        };
        if (tableId.IsNull) return null;
        var table = (SymbolTable)tr.GetObject(tableId, OpenMode.ForRead);
        return table.Has(name) ? Read(tr, tr.GetObject(table[name], OpenMode.ForRead)) : null;
    }

    public static string ComputeAssetHash(string name, AssetKind kind, TemplateAssetSignature signature, IEnumerable<string>? dependencyHashes = null)
    {
        var sb = new StringBuilder();
        sb.Append(name).Append(':').Append(kind).Append(':').Append(signature.Hash);
        if (dependencyHashes != null)
        {
            foreach (var h in dependencyHashes.OrderBy(x => x, StringComparer.Ordinal))
                sb.Append(':').Append(h);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}
