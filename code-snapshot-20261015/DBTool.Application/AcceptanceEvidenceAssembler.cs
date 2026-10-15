#nullable enable
using System.Security.Cryptography;
using System.Text.Json;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Application.StageContractsV2;

namespace DBTool.Application;

public sealed class AcceptanceEvidenceAssembler
{
    private readonly IPlotEvidencePort? _plotPort;
    private readonly IVisualReviewReader? _visualReviewReader;
    private readonly IArtifactStore? _artifactStore;

    public AcceptanceEvidenceAssembler(
        IPlotEvidencePort? plotPort = null,
        IVisualReviewReader? visualReviewReader = null,
        IArtifactStore? artifactStore = null)
    {
        _plotPort = plotPort;
        _visualReviewReader = visualReviewReader;
        _artifactStore = artifactStore;
    }

    public BatchV2 Assemble(S10StateV2 input, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        token.ThrowIfCancellationRequested();

        string M = input.context.manhole_id;

        if (input.native.readback_status != "Passed")
        {
            throw new StageFault(
                "ACCEPTANCE_NATIVE_NOT_READY",
                M,
                ["native.readback_status"],
                "Passed",
                input.native.readback_status);
        }

        if (input.request.reference_bundle_ids == null || input.request.reference_bundle_ids.Length == 0)
        {
            throw new StageFault(
                "REFERENCE_BUNDLE_MISMATCH",
                M,
                ["request.reference_bundle_ids"],
                "non-empty reference bundle IDs",
                "empty",
                input.context.sources.Select(s => s.id).ToArray());
        }

        var checks = new List<AcceptanceCheckV2>();
        var pages = new List<PlotPageV2>();

        var subjects = BuildAcceptanceSubjects(input);
        foreach (var subject in subjects)
        {
            token.ThrowIfCancellationRequested();

            var expectedVal = subject.Expected;
            var actualVal = subject.Actual;
            string unit = subject.Unit;
            double? tolerance = GetUnitTolerance(unit, input.rules);

            string verdict;
            string? reason = null;

            if (!actualVal.HasValue && subject.ActualText == null)
            {
                verdict = "NotRun";
                reason = "missing actual saved native evidence";
            }
            else if (expectedVal.HasValue)
            {
                if (!tolerance.HasValue)
                {
                    verdict = "Blocked";
                    reason = "tolerance missing for unit: " + unit;
                }
                else if (actualVal.HasValue && Math.Abs(actualVal.Value - expectedVal.Value) <= tolerance.Value)
                {
                    verdict = "Passed";
                    reason = null;
                }
                else
                {
                    verdict = "Failed";
                    reason = "outside approved unit tolerance";
                }
            }
            else
            {
                if (subject.ActualText != null && subject.ExpectedText != null &&
                    string.Equals(subject.ActualText, subject.ExpectedText, StringComparison.Ordinal))
                {
                    verdict = "Passed";
                    reason = null;
                }
                else
                {
                    verdict = "Failed";
                    reason = "semantic mismatch";
                }
            }

            checks.Add(new AcceptanceCheckV2(
                subject.Name,
                subject.SubjectId,
                expectedVal,
                actualVal,
                subject.ExpectedText,
                subject.ActualText,
                unit,
                expectedVal.HasValue ? "NR-18" : null,
                subject.SourceIds,
                subject.NativeHandles,
                subject.EvidencePaths,
                verdict,
                reason));
        }

        if (input.context.plot_session_id == null)
        {
            throw new StageFault(
                "PLOT_SESSION_MISSING",
                M,
                ["context.plot_session_id"],
                "one shared healthy plot host",
                "null");
        }

        if (input.sheet?.frames != null)
        {
            foreach (var frame in input.sheet.frames)
            {
                token.ThrowIfCancellationRequested();

                if (_plotPort != null)
                {
                    var result = _plotPort.Plot(input.native.saved_dwg_path, frame, input.request, input.context, token);
                    if (result.host_session_id != input.context.plot_session_id || result.frame_id != frame.id)
                    {
                        throw new StageFault(
                            "PLOT_FRAME_SESSION_IDENTITY",
                            frame.id,
                            ["frame_id", "host_session_id"],
                            $"{frame.id},{input.context.plot_session_id}",
                            $"{result.frame_id},{result.host_session_id}");
                    }

                    PlotPageV2? reviewed = null;
                    if (_visualReviewReader != null)
                    {
                        reviewed = _visualReviewReader.ReadReview(result, input.request.reference_bundle_ids);
                    }

                    string visualStatus = "NotRun";
                    if (reviewed != null)
                    {
                        visualStatus = reviewed.visual_status;
                    }

                    var page = result with
                    {
                        visual_status = visualStatus,
                        reviewer = reviewed?.reviewer,
                        reviewed_regions = reviewed?.reviewed_regions ?? []
                    };
                    pages.Add(page);

                    checks.Add(new AcceptanceCheckV2(
                        "PDF_Frame_Identity",
                        frame.id,
                        frame.scale_denominator,
                        page.actual_scale,
                        null,
                        null,
                        "1:S",
                        "NR-18",
                        [],
                        [],
                        [page.pdf_path],
                        Math.Abs(frame.scale_denominator - page.actual_scale) < 1e-4 ? "Passed" : "Failed",
                        null));
                }
                else
                {
                    pages.Add(new PlotPageV2(
                        frame.id,
                        "",
                        "",
                        1,
                        input.context.plot_session_id,
                        0,
                        input.request.plot_profile_id,
                        frame.scale_denominator,
                        frame.scale_denominator,
                        "",
                        "NotRun",
                        null,
                        []));
                }
            }
        }

        string rowStatus;
        if (checks.Any(c => c.verdict == "Failed"))
        {
            rowStatus = "Failed";
        }
        else if (checks.Any(c => c.verdict == "Blocked"))
        {
            rowStatus = "Blocked";
        }
        else if (checks.Any(c => c.verdict == "NotRun") || pages.Any(p => p.visual_status != "Passed"))
        {
            rowStatus = "Partial";
        }
        else
        {
            rowStatus = "Complete";
        }

        var rowVerdict = new RowVerdictV2(
            input.context.job_id,
            input.context.row_index,
            M,
            rowStatus,
            input.sheet?.frames != null ? input.sheet.frames.Select(f => f.id).ToArray() : [],
            checks.ToArray(),
            input.context.diagnostics);

        var priorRows = input.context.prior_row_results ?? [];
        var allRows = new List<RowVerdictV2>(priorRows) { rowVerdict };

        var seenKeys = new HashSet<string>();
        foreach (var r in allRows)
        {
            string key = $"{r.job_id}|{r.row_index}|{r.manhole_id}";
            if (!seenKeys.Add(key))
            {
                throw new StageFault(
                    "BATCH_RECEIPT_DUPLICATE",
                    M,
                    ["rows"],
                    "one receipt per selected row",
                    $"Duplicate found for row: {key}");
            }
        }

        var priorPages = input.context.prior_plot_pages ?? [];
        var allPages = new List<PlotPageV2>(priorPages);
        allPages.AddRange(pages);

        var allTargetIds = input.request.jobs
            .SelectMany(j => j.targets)
            .Select(t => t.manhole_id)
            .ToHashSet();

        bool batchComplete = allRows.Select(r => r.manhole_id).ToHashSet().SetEquals(allTargetIds) &&
                             allRows.All(r => r.status == "Complete");
        string batchStatus = batchComplete ? "Complete" : "Partial";

        string manifestPath = Path.Combine(input.context.artifact_directory, "delivery-manifest.json");

        string? aggregateSha256 = null;
        if (!string.IsNullOrEmpty(input.request.target_dwg_path) && File.Exists(input.request.target_dwg_path))
        {
            using var sha = SHA256.Create();
            aggregateSha256 = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(input.request.target_dwg_path)));
        }

        var batchResult = new BatchV2(
            allRows.ToArray(),
            allPages.ToArray(),
            batchStatus,
            manifestPath,
            File.Exists(input.request.target_dwg_path) ? input.request.target_dwg_path : null,
            aggregateSha256);

        PersistDeliveryManifest(manifestPath, batchResult, input.context.sources, input.context.receipts);

        return batchResult;
    }

    private static double? GetUnitTolerance(string unit, RulesV2 rules)
    {
        double? tolerance = unit switch
        {
            "mm" => rules.length_tolerance_mm,
            "mm2" => rules.area_tolerance_mm2,
            "kg" => rules.mass_tolerance_kg,
            "paper_mm" => rules.paper_tolerance_mm,
            "pdf_point" => rules.pdf_tolerance_points,
            "rad" => rules.angle_tolerance_rad,
            "dimensionless" => rules.basis_tolerance,
            "1:S" => null,
            "none" => 0.0,
            _ => null
        };
        return tolerance.HasValue && double.IsFinite(tolerance.Value) && tolerance.Value >= 0 ? tolerance : null;
    }

    private sealed record AcceptanceSubjectItem(
        string Name,
        string SubjectId,
        double? Expected,
        double? Actual,
        string? ExpectedText,
        string? ActualText,
        string Unit,
        string[] SourceIds,
        string[] NativeHandles,
        string[] EvidencePaths);

    private static List<AcceptanceSubjectItem> BuildAcceptanceSubjects(S10StateV2 input)
    {
        var list = new List<AcceptanceSubjectItem>();
        var e = input.effective;
        var handles = input.native.handles ?? [];
        var handleMap = handles.ToDictionary(h => h.semantic_id, StringComparer.OrdinalIgnoreCase);

        // 1. Effective dimension checks
        void AddDim(string name, string subjectId, double expectedVal)
        {
            double? actualVal = null;
            string[] nHandles = [];
            if (handleMap.TryGetValue(subjectId, out var h))
            {
                // A handle proves identity only. No independent numerical readback has been supplied here.
                nHandles = [h.entity_handle];
            }
            list.Add(new AcceptanceSubjectItem(
                Name: name,
                SubjectId: subjectId,
                Expected: expectedVal,
                Actual: actualVal,
                ExpectedText: null,
                ActualText: null,
                Unit: "mm",
                SourceIds: e.source_ids ?? [],
                NativeHandles: nHandles,
                EvidencePaths: [input.native.saved_dwg_path]));
        }

        AddDim("Clear_X", "Effective.clear_x_mm", e.clear_x_mm);
        AddDim("Clear_Y", "Effective.clear_y_mm", e.clear_y_mm);
        AddDim("Clear_H", "Effective.clear_h_mm", e.clear_h_mm);
        AddDim("Base_Thickness", "Effective.base_mm", e.base_mm);
        AddDim("Wall_Thickness", "Effective.wall_mm", e.wall_mm);
        AddDim("Roof_Thickness", "Effective.roof_mm", e.roof_mm);

        if (e.base_haunch_mm > 0)
        {
            AddDim("BaseHaunch", "Effective.base_haunch_mm", e.base_haunch_mm);
        }
        if (e.top_haunch_mm > 0)
        {
            AddDim("TopHaunch", "Effective.top_haunch_mm", e.top_haunch_mm);
        }

        // 2. Rebar groups
        if (input.groups?.items != null)
        {
            foreach (var g in input.groups.items)
            {
                list.Add(new AcceptanceSubjectItem(
                    Name: $"Group_{g.display_mark}_MeanLength",
                    SubjectId: g.id,
                    Expected: g.mean_mm,
                    Actual: null,
                    ExpectedText: g.display_mark,
                    ActualText: null,
                    Unit: "mm",
                    SourceIds: g.source_ids ?? [],
                    NativeHandles: [],
                    EvidencePaths: [input.native.saved_dwg_path]));
            }
        }

        // 3. Openings
        if (input.geometry?.openings != null)
        {
            foreach (var op in input.geometry.openings)
            {
                if (op.invert_mm.HasValue)
                {
                    list.Add(new AcceptanceSubjectItem(
                        Name: $"Opening_{op.id}_Invert",
                        SubjectId: op.id,
                        Expected: op.invert_mm.Value,
                        Actual: null,
                        ExpectedText: null,
                        ActualText: null,
                        Unit: "mm",
                        SourceIds: op.source_ids ?? [],
                        NativeHandles: [],
                        EvidencePaths: [input.native.saved_dwg_path]));
                }
            }
        }

        // 4. Native handles validation
        foreach (var h in handles)
        {
            list.Add(new AcceptanceSubjectItem(
                Name: $"NativeHandle_{h.semantic_id}",
                SubjectId: h.semantic_id,
                Expected: null,
                Actual: null,
                ExpectedText: h.owner_manhole_id,
                ActualText: null,
                Unit: "none",
                SourceIds: [],
                NativeHandles: [h.entity_handle],
                EvidencePaths: [input.native.saved_dwg_path]));
        }

        return list;
    }

    private void PersistDeliveryManifest(
        string manifestPath,
        BatchV2 batch,
        SourceV2[] sources,
        ReceiptV2[] receipts)
    {
        try
        {
            string dir = Path.GetDirectoryName(manifestPath) ?? "";
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var manifestDoc = new
            {
                batch = batch,
                sources = sources,
                receipts = receipts,
                generated_at_utc = DateTime.UtcNow.ToString("O")
            };

            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(manifestDoc, new JsonSerializerOptions { WriteIndented = true });
            if (_artifactStore != null)
            {
                _artifactStore.Write(manifestPath, bytes);
            }
            else
            {
                File.WriteAllBytes(manifestPath, bytes);
            }
        }
        catch
        {
            // Best-effort artifact persistence
        }
    }
}
