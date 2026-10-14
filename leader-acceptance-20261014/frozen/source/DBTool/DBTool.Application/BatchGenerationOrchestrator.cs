#nullable enable
using System.Security.Cryptography;
using System.Text.Json;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Application.StageContractsV2;

namespace DBTool.Application;

public sealed class BatchGenerationOrchestrator
{
        private readonly IStagePipelineV2? _pipeline;
        private readonly IPlotSessionPort? _plotSessionPort;
        private readonly IArtifactStore? _artifactStore;

        public BatchGenerationOrchestrator(
            IStagePipelineV2? pipeline = null,
            IPlotSessionPort? plotSessionPort = null,
            IArtifactStore? artifactStore = null)
        {
            _pipeline = pipeline;
            _plotSessionPort = plotSessionPort;
            _artifactStore = artifactStore;
        }

        public BatchV2 Generate(RequestV2 request, IRunContextFactory contextFactory, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(contextFactory);

            var selected = new List<(JobV2 job, TargetV2 target)>();
            if (request.jobs != null)
            {
                foreach (var job in request.jobs)
                {
                    if (job.targets != null)
                    {
                        foreach (var target in job.targets)
                        {
                            selected.Add((job, target));
                        }
                    }
                }
            }

            var results = new List<RowVerdictV2>();
            var pages = new List<PlotPageV2>();

            string? sessionId = null;
            if (_plotSessionPort != null && !string.IsNullOrEmpty(request.plot_profile_id))
            {
                sessionId = _plotSessionPort.OpenHealthySession(request.plot_profile_id);
            }

            bool sessionBecameUnhealthy = false;

            for (int i = 0; i < selected.Count; i++)
            {
                var (job, target) = selected[i];

                if (sessionBecameUnhealthy)
                {
                    var diag = new DiagnosticV2(
                        "HOST_UNHEALTHY",
                        "PLOT",
                        target.manhole_id,
                        target.manhole_id,
                        ["session"],
                        "healthy",
                        "unhealthy",
                        [],
                        [],
                        "Block");
                    results.Add(new RowVerdictV2(job.job_id, target.row_index, target.manhole_id, "NotRun", [], [], [diag]));
                    continue;
                }

                var ctx = contextFactory.Create(request, job, target, results.ToArray(), pages.ToArray(), sessionId);

                if (token.IsCancellationRequested || ctx.cancel_requested)
                {
                    var diag = new DiagnosticV2(
                        "RUN_CANCELLED",
                        "PRECHECK",
                        target.manhole_id,
                        target.manhole_id,
                        [token.IsCancellationRequested ? "token.IsCancellationRequested" : "context.cancel_requested"],
                        "false",
                        "true",
                        [],
                        [],
                        "Block");
                    results.Add(new RowVerdictV2(job.job_id, target.row_index, target.manhole_id, "Cancelled", [], [], [diag]));
                    continue;
                }

                double[] attempts = request.scale_mode == "Auto"
                    ? [request.body_scale, 20.0, 25.0, 30.0, 40.0, 50.0]
                    : [request.body_scale];

                bool rowFinished = false;
                for (int attemptIdx = 0; attemptIdx < attempts.Length; attemptIdx++)
                {
                    double s = attempts[attemptIdx];
                    var rowRequest = request with { body_scale = s };
                    var state = new EntryStateV2(ctx, rowRequest);

                    if (_pipeline == null)
                    {
                        var diag = new DiagnosticV2(
                            "PIPELINE_NOT_INJECTED",
                            "BATCH",
                            target.manhole_id,
                            target.manhole_id,
                            ["pipeline"],
                            "IStagePipelineV2 instance",
                            "null",
                            [],
                            [],
                            "Block");
                        results.Add(new RowVerdictV2(job.job_id, target.row_index, target.manhole_id, "Blocked", [], [], [diag]));
                        rowFinished = true;
                        break;
                    }

                    try
                    {
                        var s11 = _pipeline.RunRow(state, token);
                        results = s11.batch.rows.ToList();
                        pages = s11.batch.pages.ToList();
                        rowFinished = true;
                        break;
                    }
                    catch (StageFault fault) when (fault.Code == "LAYOUT_REBUILD_AT_SCALE" && attemptIdx < attempts.Length - 1)
                    {
                        continue;
                    }
                    catch (StageFault fault)
                    {
                        string verdictStatus = fault.Code == "RUN_CANCELLED" ? "Cancelled"
                            : (fault.Code.Contains("MISSING") || fault.Code.Contains("INVALID") || fault.Code.Contains("BINDING")) ? "Blocked"
                            : "Failed";
                        var diag = new DiagnosticV2(
                            fault.Code,
                            "BATCH",
                            target.manhole_id,
                            fault.Subject,
                            fault.FieldPaths,
                            fault.Expected,
                            fault.Actual,
                            fault.Sources,
                            fault.NeedsRules,
                            "Block");
                        results.Add(new RowVerdictV2(job.job_id, target.row_index, target.manhole_id, verdictStatus, [], [], [diag]));
                        rowFinished = true;
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        var diag = new DiagnosticV2(
                            "RUN_CANCELLED",
                            "BATCH",
                            target.manhole_id,
                            target.manhole_id,
                            ["cancellation"],
                            "completed",
                            "cancelled",
                            [],
                            [],
                            "Block");
                        results.Add(new RowVerdictV2(job.job_id, target.row_index, target.manhole_id, "Cancelled", [], [], [diag]));
                        rowFinished = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        var diag = new DiagnosticV2(
                            "BATCH_ROW_EXECUTION_FAILED",
                            "BATCH",
                            target.manhole_id,
                            target.manhole_id,
                            ["pipeline"],
                            "success",
                            ex.Message,
                            [],
                            [],
                            "Block");
                        results.Add(new RowVerdictV2(job.job_id, target.row_index, target.manhole_id, "Failed", [], [], [diag]));
                        rowFinished = true;
                        break;
                    }
                }

                if (!rowFinished && !results.Any(r => r.job_id == job.job_id && r.row_index == target.row_index && r.manhole_id == target.manhole_id))
                {
                    var diag = new DiagnosticV2(
                        "LAYOUT_REBUILD_AT_SCALE_EXHAUSTED",
                        "BATCH",
                        target.manhole_id,
                        target.manhole_id,
                        ["scale_attempts"],
                        "scale fit",
                        "no fit",
                        [],
                        ["NR-22"],
                        "Block");
                    results.Add(new RowVerdictV2(job.job_id, target.row_index, target.manhole_id, "Failed", [], [], [diag]));
                }

                if (_plotSessionPort != null && sessionId != null && !_plotSessionPort.IsHealthy(sessionId))
                {
                    _plotSessionPort.Close(sessionId);
                    sessionBecameUnhealthy = true;
                }
            }

            if (_plotSessionPort != null && sessionId != null && !sessionBecameUnhealthy)
            {
                _plotSessionPort.Close(sessionId);
            }

            bool allComplete = selected.Count > 0 &&
                               results.Count == selected.Count &&
                               results.All(r => r.status == "Complete");
            string batchStatus = allComplete ? "Complete" : "Partial";

            string runDir = selected.Count > 0
                ? contextFactory.Create(request, selected[0].job, selected[0].target, [], [], null).artifact_directory
                : "./outputs";

            string manifestPath = Path.Combine(runDir, "delivery-manifest.json");

            string? finalDwgSha256 = null;
            if (!string.IsNullOrEmpty(request.target_dwg_path) && File.Exists(request.target_dwg_path))
            {
                using var sha = SHA256.Create();
                finalDwgSha256 = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(request.target_dwg_path)));
            }

            var finalBatch = new BatchV2(
                results.ToArray(),
                pages.ToArray(),
                batchStatus,
                manifestPath,
                File.Exists(request.target_dwg_path) ? request.target_dwg_path : null,
                finalDwgSha256);

            PersistManifest(manifestPath, finalBatch);

            return finalBatch;
        }

        private void PersistManifest(string manifestPath, BatchV2 batch)
        {
            try
            {
                string dir = Path.GetDirectoryName(manifestPath) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(batch, new JsonSerializerOptions { WriteIndented = true });
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
