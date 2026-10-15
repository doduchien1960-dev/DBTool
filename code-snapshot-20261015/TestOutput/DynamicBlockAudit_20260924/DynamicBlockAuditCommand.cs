using System.Collections;
using System.Reflection;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

namespace DBTool.DynamicBlockAudit;

public sealed class DynamicBlockAuditCommand
{
    private const string OutputPath = @"C:\Users\DucBao\Desktop\Dbtool\Ver 1\DBTool\TestOutput\DynamicBlockAudit_20260924\{0}_TK_block_audit.txt";

    [CommandMethod("DBAUDITTK")]
    public void Audit()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument
            ?? throw new InvalidOperationException("Không có drawing đang mở.");
        Database db = doc.Database;
        var report = new StringBuilder();
        report.AppendLine($"DWG: {db.Filename}");
        report.AppendLine($"Time: {DateTime.Now:O}");

        using (Transaction tr = db.TransactionManager.StartTransaction())
        {
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            report.AppendLine("\n[TK DEFINITIONS]");
            foreach (ObjectId id in blockTable)
            {
                var definition = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (!definition.Name.StartsWith("TK.", StringComparison.OrdinalIgnoreCase))
                    continue;

                object? isDynamic = ReadProperty(definition, "IsDynamicBlock");
                report.AppendLine($"BLOCK|{definition.Name}|Dynamic={isDynamic}|Anonymous={definition.IsAnonymous}|Scale={definition.BlockScaling}|Origin={definition.Origin}|HasAtt={definition.HasAttributeDefinitions}");
                foreach (ObjectId entityId in definition)
                {
                    if (tr.GetObject(entityId, OpenMode.ForRead) is AttributeDefinition att)
                        report.AppendLine($"  ATTDEF|{att.Tag}|Prompt={att.Prompt}|Default={att.TextString}|Pos={att.Position}|Height={att.Height}");
                }
            }

            report.AppendLine("\n[TK REFERENCES IN MODELSPACE]");
            var model = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId entityId in model)
            {
                if (tr.GetObject(entityId, OpenMode.ForRead) is not BlockReference reference)
                    continue;

                object? isDynamic = ReadProperty(reference, "IsDynamicBlock");
                ObjectId effectiveRecordId = reference.BlockTableRecord;
                object? dynamicRecord = ReadProperty(reference, "DynamicBlockTableRecord");
                if (dynamicRecord is ObjectId dynId && !dynId.IsNull)
                    effectiveRecordId = dynId;
                string effectiveName = ((BlockTableRecord)tr.GetObject(effectiveRecordId, OpenMode.ForRead)).Name;
                string storedName = ((BlockTableRecord)tr.GetObject(reference.BlockTableRecord, OpenMode.ForRead)).Name;
                if (!effectiveName.StartsWith("TK.", StringComparison.OrdinalIgnoreCase) &&
                    !storedName.StartsWith("TK.", StringComparison.OrdinalIgnoreCase))
                    continue;

                report.AppendLine($"REF|Effective={effectiveName}|Stored={storedName}|Dynamic={isDynamic}|Scale={reference.ScaleFactors}|Position={reference.Position}");
                foreach (ObjectId attributeId in reference.AttributeCollection)
                {
                    if (tr.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
                        report.AppendLine($"  ATT|{attribute.Tag}|{attribute.TextString}");
                }

                object? dynamicProperties = ReadProperty(reference, "DynamicBlockReferencePropertyCollection");
                if (dynamicProperties is IEnumerable properties)
                {
                    foreach (object? property in properties)
                    {
                        if (property is null) continue;
                        string propertyName = Convert.ToString(ReadProperty(property, "PropertyName")) ?? "?";
                        string value = Convert.ToString(ReadProperty(property, "Value")) ?? "";
                        string units = Convert.ToString(ReadProperty(property, "UnitsType")) ?? "";
                        string readOnly = Convert.ToString(ReadProperty(property, "ReadOnly")) ?? "";
                        object? allowedValues = ReadProperty(property, "AllowedValues");
                        report.AppendLine($"  DYN|{propertyName}|Value={value}|Units={units}|ReadOnly={readOnly}|Allowed={FormatAllowed(allowedValues)}");
                    }
                }
            }

            tr.Commit();
        }

        string suffix = Path.GetFileNameWithoutExtension(db.Filename).Replace(' ', '_');
        string path = string.Format(OutputPath, suffix);
        File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        doc.Editor.WriteMessage($"\nTK block audit saved: {path}\n");
    }

    private static object? ReadProperty(object instance, string propertyName)
    {
        try
        {
            return instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatAllowed(object? allowed)
    {
        if (allowed is not IEnumerable values || allowed is string)
            return Convert.ToString(allowed) ?? "";
        return string.Join(",", values.Cast<object?>().Select(value => Convert.ToString(value) ?? ""));
    }
}
