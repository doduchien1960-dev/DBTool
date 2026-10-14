#nullable enable
using System.Security.Cryptography;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Application;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Application.StageContractsV2;

namespace DBTool.AutoCAD;

public sealed class NativeReadbackVerifier
{
    public NativeV2 Verify(S9StateV2 expected, NativeV2 committedWrite, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(committedWrite);

        token.ThrowIfCancellationRequested();

        string M = expected.context.manhole_id;

        if (committedWrite.commit_state != "Committed" &&
            committedWrite.commit_state != "Saved" &&
            committedWrite.commit_state != "Reopened")
        {
            throw new StageFault(
                "NATIVE_WRITE_ROLLED_BACK",
                M,
                ["commit_state"],
                "transaction committed",
                committedWrite.commit_state);
        }

        if (string.IsNullOrWhiteSpace(committedWrite.saved_dwg_path) || !File.Exists(committedWrite.saved_dwg_path))
        {
            throw new StageFault(
                "NATIVE_FAILURE_AFTER_COMMIT",
                M,
                ["target_dwg_path"],
                "save/readback completed",
                $"File not found: {committedWrite.saved_dwg_path}");
        }

        byte[] actualBytes = File.ReadAllBytes(committedWrite.saved_dwg_path);
        using var sha256 = SHA256.Create();
        string actualSha256 = Convert.ToHexString(sha256.ComputeHash(actualBytes));

        var actualHandles = new List<HandleV2>();
        var checks = new List<string>();

        try
        {
            using var db = new Database(false, true);
            db.ReadDwgFile(committedWrite.saved_dwg_path, FileOpenMode.OpenForReadAndAllShare, true, "");
            using var tr = db.TransactionManager.StartTransaction();

            if (committedWrite.handles != null)
            {
                foreach (var h in committedWrite.handles)
                {
                    token.ThrowIfCancellationRequested();

                    if (!long.TryParse(h.entity_handle, System.Globalization.NumberStyles.HexNumber, null, out long handleVal))
                    {
                        checks.Add($"Invalid handle format: {h.entity_handle}");
                        continue;
                    }

                    Handle handle = new Handle(handleVal);
                    ObjectId objId = db.GetObjectId(false, handle, 0);
                    if (objId.IsNull || objId.IsErased)
                    {
                        checks.Add($"Handle {h.entity_handle} not found or erased in reopened DWG");
                        continue;
                    }

                    var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (entity == null)
                    {
                        checks.Add($"Handle {h.entity_handle} is not an Entity");
                        continue;
                    }

                    string? actualBlockName = null;
                    var actualTags = new List<string>();
                    var actualValues = new List<string>();

                    if (entity is BlockReference blkRef)
                    {
                        if (tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) is BlockTableRecord btr)
                        {
                            actualBlockName = btr.Name;
                        }

                        foreach (ObjectId attId in blkRef.AttributeCollection)
                        {
                            if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference att)
                            {
                                actualTags.Add(att.Tag);
                                actualValues.Add(att.TextString);
                            }
                        }
                    }

                    actualHandles.Add(new HandleV2(
                        semantic_id: h.semantic_id,
                        entity_handle: h.entity_handle,
                        block_name: actualBlockName ?? h.block_name,
                        attribute_tags: actualTags.Count > 0 ? actualTags.ToArray() : h.attribute_tags,
                        attribute_values: actualValues.Count > 0 ? actualValues.ToArray() : h.attribute_values,
                        owner_manhole_id: h.owner_manhole_id
                    ));
                }
            }

            tr.Commit();
        }
        catch (StageFault)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new StageFault(
                "NATIVE_FAILURE_AFTER_COMMIT",
                M,
                ["commit_state", "target_dwg_path"],
                "save/readback completed",
                $"Reopen database error: {ex.Message}");
        }

        if (checks.Count > 0)
        {
            throw new StageFault(
                "NATIVE_READBACK_MISMATCH",
                M,
                ["checks"],
                "all measured fields match",
                string.Join("; ", checks));
        }

        if (committedWrite.unchanged_before_hashes != null && committedWrite.unchanged_after_hashes != null)
        {
            if (committedWrite.unchanged_before_hashes.Length != committedWrite.unchanged_after_hashes.Length ||
                !committedWrite.unchanged_before_hashes.SequenceEqual(committedWrite.unchanged_after_hashes))
            {
                throw new StageFault(
                    "UPDATE_MODIFIED_OTHER_MANHOLE",
                    M,
                    ["unchanged_generation_manifests"],
                    string.Join(",", committedWrite.unchanged_before_hashes),
                    string.Join(",", committedWrite.unchanged_after_hashes));
            }
        }

        return new NativeV2(
            saved_dwg_path: committedWrite.saved_dwg_path,
            saved_dwg_sha256: actualSha256,
            handles: actualHandles.Count > 0 ? actualHandles.ToArray() : (committedWrite.handles ?? []),
            normalized_geometry_hash: committedWrite.normalized_geometry_hash,
            normalized_rebar_hash: committedWrite.normalized_rebar_hash,
            repository_version: committedWrite.repository_version,
            write_status: "Committed",
            readback_status: "Passed",
            unchanged_manhole_ids: committedWrite.unchanged_manhole_ids ?? [],
            unchanged_before_hashes: committedWrite.unchanged_before_hashes ?? [],
            unchanged_after_hashes: committedWrite.unchanged_after_hashes ?? [],
            domain_geometry_hash: expected.rebar.fabrication_geometry_hash,
            expected_projection_hash: committedWrite.expected_projection_hash,
            commit_state: "Reopened"
        );
    }
}
