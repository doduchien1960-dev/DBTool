using System.Runtime.CompilerServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Application;

namespace DBTool.AutoCAD.Generation;

public sealed record GenerationMetadata(string GenerationId, string InputHash, string TemplateHash, string SnapshotVersion);

/// <summary>
/// Generation ownership is persisted as a versioned XData schema on every owned entity.
/// Replacement therefore targets generation identity, never a layer or the whole model space.
/// </summary>
public sealed class GenerationRepository
{
    public const string RegAppName = "DBTOOL_GENERATION_V1";
    public const string SnapshotVersion = "generation-xdata/v1";

    public string GetDocumentToken(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var name = string.IsNullOrWhiteSpace(document.Database.Filename) ? "<unsaved>" : Path.GetFullPath(document.Database.Filename);
        return $"{name}|doc:{RuntimeHelpers.GetHashCode(document):X8}";
    }

    public IReadOnlyList<string> ReadOwnedHandles(Document document, string generationId)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureText(generationId, nameof(generationId));
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var handles = FindOwnedEntities(document.Database, transaction, generationId)
            .Select(id => id.Handle.ToString())
            .OrderBy(handle => handle, StringComparer.Ordinal)
            .ToArray();
        transaction.Commit();
        return handles;
    }

    public IReadOnlyDictionary<string, string> ReadOwnedAttributeValues(Document document, string generationId)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureText(generationId, nameof(generationId));
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in FindOwnedEntities(document.Database, transaction, generationId))
        {
            if (transaction.GetObject(id, OpenMode.ForRead) is not BlockReference reference) continue;
            foreach (ObjectId attributeId in reference.AttributeCollection)
            {
                if (transaction.GetObject(attributeId, OpenMode.ForRead) is AttributeReference attribute)
                    values[attribute.Tag] = attribute.TextString;
            }
        }
        transaction.Commit();
        return values;
    }

    public IReadOnlyList<double> ReadOwnedHatchAreas(Document document, string generationId)
    {
        ArgumentNullException.ThrowIfNull(document);
        EnsureText(generationId, nameof(generationId));
        using var transaction = document.Database.TransactionManager.StartTransaction();
        var areas = FindOwnedEntities(document.Database, transaction, generationId)
            .Select(id => transaction.GetObject(id, OpenMode.ForRead))
            .OfType<Hatch>()
            .Select(hatch => hatch.Area)
            .OrderBy(area => area)
            .ToArray();
        transaction.Commit();
        return areas;
    }

    internal void EnsureSchema(Database database, Transaction transaction)
    {
        var table = (RegAppTable)transaction.GetObject(database.RegAppTableId, OpenMode.ForRead);
        if (table.Has(RegAppName)) return;
        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = RegAppName };
        table.Add(record);
        transaction.AddNewlyCreatedDBObject(record, true);
    }

    internal void Tag(DBObject databaseObject, string generationId, string inputHash, string templateHash)
    {
        EnsureText(generationId, nameof(generationId));
        EnsureText(inputHash, nameof(inputHash));
        EnsureText(templateHash, nameof(templateHash));
        databaseObject.XData = new ResultBuffer(
            new TypedValue(1001, RegAppName),
            new TypedValue(1000, generationId),
            new TypedValue(1000, inputHash),
            new TypedValue(1000, templateHash),
            new TypedValue(1000, SnapshotVersion));
    }

    internal IReadOnlyList<ObjectId> FindOwnedEntities(Database database, Transaction transaction, string generationId)
    {
        var modelSpace = (BlockTableRecord)transaction.GetObject(
            SymbolUtilityServices.GetBlockModelSpaceId(database), OpenMode.ForRead);
        var result = new List<ObjectId>();
        foreach (ObjectId id in modelSpace)
        {
            var entity = transaction.GetObject(id, OpenMode.ForRead);
            if (ReadMetadata(entity) is { } metadata && metadata.GenerationId == generationId)
                result.Add(id);
        }
        return result;
    }

    internal GenerationMetadata? ReadGeneration(Database database, Transaction transaction, string generationId)
    {
        var entities = FindOwnedEntities(database, transaction, generationId);
        foreach (var id in entities)
        {
            var entity = transaction.GetObject(id, OpenMode.ForRead);
            var metadata = ReadMetadata(entity);
            if (metadata is not null) return metadata;
        }
        return null;
    }

    internal void EraseGeneration(Database database, Transaction transaction, string generationId)
    {
        foreach (var id in FindOwnedEntities(database, transaction, generationId))
        {
            if (transaction.GetObject(id, OpenMode.ForWrite) is Entity entity && !entity.IsErased)
                entity.Erase();
        }
    }

    internal static GenerationMetadata? ReadMetadata(DBObject databaseObject)
    {
        using var data = databaseObject.XData;
        if (data is null) return null;
        var values = data.AsArray();
        if (values.Length < 5 || values[0].Value is not string app || !string.Equals(app, RegAppName, StringComparison.Ordinal))
            return null;
        if (values[1].Value is not string generationId || values[2].Value is not string inputHash ||
            values[3].Value is not string templateHash || values[4].Value is not string snapshot)
            return null;
        return new GenerationMetadata(generationId, inputHash, templateHash, snapshot);
    }

    public void TagOwned(DBObject databaseObject, string generationId, string semanticId, string runId, string buildHash)
    {
        EnsureText(generationId, nameof(generationId));
        EnsureText(semanticId, nameof(semanticId));
        EnsureText(runId, nameof(runId));
        EnsureText(buildHash, nameof(buildHash));

        databaseObject.XData = new ResultBuffer(
            new TypedValue(1001, RegAppName),
            new TypedValue(1000, generationId),
            new TypedValue(1000, semanticId),
            new TypedValue(1000, runId),
            new TypedValue(1000, buildHash),
            new TypedValue(1000, SnapshotVersion));
    }

    public const string ManifestDictionaryName = "DBTOOL_MANIFESTS";

    public void StoreGenerationManifest(Database database, Transaction transaction, string manholeId, string geometryHash, string rebarHash, string sheetHash)
    {
        EnsureText(manholeId, nameof(manholeId));
        var nod = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        DBDictionary manifestDict;
        if (nod.Contains(ManifestDictionaryName))
        {
            manifestDict = (DBDictionary)transaction.GetObject(nod.GetAt(ManifestDictionaryName), OpenMode.ForWrite);
        }
        else
        {
            nod.UpgradeOpen();
            manifestDict = new DBDictionary();
            nod.SetAt(ManifestDictionaryName, manifestDict);
            transaction.AddNewlyCreatedDBObject(manifestDict, true);
        }

        var xrec = new Xrecord
        {
            Data = new ResultBuffer(
                new TypedValue(1000, manholeId),
                new TypedValue(1000, geometryHash ?? ""),
                new TypedValue(1000, rebarHash ?? ""),
                new TypedValue(1000, sheetHash ?? ""))
        };

        if (manifestDict.Contains(manholeId))
        {
            manifestDict.Remove(manholeId);
        }
        manifestDict.SetAt(manholeId, xrec);
        transaction.AddNewlyCreatedDBObject(xrec, true);
    }

    public (IReadOnlyList<string> ids, IReadOnlyList<string> hashes) ReadUntouchedGenerationManifests(Database database, Transaction transaction, string? excludedId = null)
    {
        var ids = new List<string>();
        var hashes = new List<string>();

        var nod = (DBDictionary)transaction.GetObject(database.NamedObjectsDictionaryId, OpenMode.ForRead);
        if (!nod.Contains(ManifestDictionaryName))
            return (ids, hashes);

        var manifestDict = (DBDictionary)transaction.GetObject(nod.GetAt(ManifestDictionaryName), OpenMode.ForRead);
        foreach (DBDictionaryEntry entry in manifestDict)
        {
            if (excludedId != null && string.Equals(entry.Key, excludedId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (transaction.GetObject(entry.Value, OpenMode.ForRead) is Xrecord xrec && xrec.Data != null)
            {
                var arr = xrec.Data.AsArray();
                string gHash = arr.Length > 1 && arr[1].Value is string s1 ? s1 : "";
                string rHash = arr.Length > 2 && arr[2].Value is string s2 ? s2 : "";
                string sHash = arr.Length > 3 && arr[3].Value is string s3 ? s3 : "";
                ids.Add(entry.Key);
                hashes.Add($"{gHash}|{rHash}|{sHash}");
            }
        }

        return (ids, hashes);
    }

    private static void EnsureText(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Required text.", parameterName);
    }
}
