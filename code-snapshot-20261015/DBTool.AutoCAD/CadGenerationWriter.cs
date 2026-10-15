using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Application;
using DBTool.Application.Templates;
using DBTool.Domain;
using DBTool.Drawing;
using DBTool.AutoCAD.Generation;
using DBTool.AutoCAD.Render;
using DBTool.Domain.StageContractsV2;
using DBTool.Drawing.StageContractsV2;
using DBTool.Application.StageContractsV2;

namespace DBTool.AutoCAD;

public sealed class CadGenerationWriter : ICadGenerationWriter, INativeGenerationPort
{
    private readonly Document _document;
    private readonly GenerationRepository _repository;
    private readonly CadRenderer _renderer;
    private readonly SheetLayout? _layout;
    private readonly SheetPlan? _sheetPlan;
    private readonly PresentationProfile? _profile;

    public CadGenerationWriter(Document document, GenerationRepository? repository = null, CadRenderer? renderer = null, SheetLayout? layout = null, SheetPlan? sheetPlan = null, PresentationProfile? profile = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _repository = repository ?? new GenerationRepository();
        _renderer = renderer ?? new CadRenderer();
        if (sheetPlan is not null && (profile is null || layout is not null))
            throw new ArgumentException("A SheetPlan needs a presentation profile and cannot accompany a precomputed layout.");
        _layout = layout;
        _sheetPlan = sheetPlan;
        _profile = profile;
    }

    public Task<OperationResult<GenerationArtifact>> WriteAsync(
        DrawingPackage drawingPackage,
        ValidatedTemplateManifest template,
        GenerationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(drawingPackage);
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(request);
            if (!template.CanRender)
                return Task.FromResult(Fail("TEMPLATE_NOT_RENDERABLE", "The template validation mode does not authorize CAD rendering."));
            if (!string.Equals(drawingPackage.InputHash, request.ExpectedInputHash, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Fail("GENERATION_INPUT_HASH_MISMATCH", "The request does not match the drawing package."));

            var layout = _layout;
            if (_sheetPlan is not null)
            {
                var arranged = SheetLayoutPlanner.Arrange(drawingPackage, _profile!, _sheetPlan);
                if (!arranged.Success)
                    return Task.FromResult(new OperationResult<GenerationArtifact>(false, null, arranged.Diagnostics));
                layout = arranged.Value;
            }
            var session = new CadWriteSession(_repository, drawingPackage.InputHash, template.Manifest.SourceFileHash);
            var result = session.Run(_document, request, context =>
            {
                var render = _renderer.Render(drawingPackage, template, context, layout);
                context.SetRenderResult(render);
            }, cancellationToken);
            return Task.FromResult(result);
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Fail("OPERATION_CANCELLED", "CAD write cancelled before the host operation started."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return Task.FromResult(Fail("CAD_WRITE_FAILED", ex.Message));
        }
    }

    public NativeV2 Write(S9StateV2 input, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        token.ThrowIfCancellationRequested();

        string M = input.context.manhole_id;
        var request = input.request;
        bool committed = false;
        bool saved = false;
        bool reopened = false;

        string targetDwg = request.target_dwg_path;
        if (string.IsNullOrWhiteSpace(targetDwg))
        {
            throw new StageFault("OUTPUT_PATH_SOURCE_CONFLICT", M, ["target_dwg_path"], "output copy", "empty");
        }

        var handleMap = new List<HandleV2>();
        IReadOnlyList<string> beforeIds = [];
        IReadOnlyList<string> beforeHashes = [];

        try
        {
            using (var docLock = _document.LockDocument())
            using (var tr = _document.Database.TransactionManager.StartTransaction())
            {
                _repository.EnsureSchema(_document.Database, tr);

                var before = _repository.ReadUntouchedGenerationManifests(_document.Database, tr, M);
                beforeIds = before.ids;
                beforeHashes = before.hashes;

                if (string.Equals(request.generation_mode, "Replace", StringComparison.OrdinalIgnoreCase))
                {
                    _repository.EraseGeneration(_document.Database, tr, M);
                }
                else if (string.Equals(request.generation_mode, "Create", StringComparison.OrdinalIgnoreCase))
                {
                    var existing = _repository.ReadGeneration(_document.Database, tr, M);
                    if (existing != null)
                    {
                        throw new StageFault("GENERATION_ALREADY_EXISTS", M, ["generation_id"], "new unique ID", M);
                    }
                }

                _repository.StoreGenerationManifest(_document.Database, tr, M,
                    input.geometry.physical_hash, input.rebar.physical_hash, input.sheet.sheet_hash);

                tr.Commit();
                committed = true;
            }

            string? outDir = Path.GetDirectoryName(targetDwg);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            _document.Database.SaveAs(targetDwg, DwgVersion.Current);
            saved = true;

            string artifactDir = input.context.artifact_directory;
            string nativeDir = Path.Combine(artifactDir, "native");
            if (!Directory.Exists(nativeDir)) Directory.CreateDirectory(nativeDir);
            string rowSnapshot = Path.Combine(nativeDir, $"{M}_{input.context.run_id}.dwg");
            File.Copy(targetDwg, rowSnapshot, true);

            byte[] actualSavedBytes = File.ReadAllBytes(rowSnapshot);
            using var sha = System.Security.Cryptography.SHA256.Create();
            string savedSha = Convert.ToHexString(sha.ComputeHash(actualSavedBytes));

            IReadOnlyList<string> afterIds = [];
            IReadOnlyList<string> afterHashes = [];

            using (var reopenDb = new Database(false, true))
            {
                reopenDb.ReadDwgFile(rowSnapshot, FileOpenMode.OpenForReadAndAllShare, true, "");
                reopened = true;
                using var readTr = reopenDb.TransactionManager.StartTransaction();
                var after = _repository.ReadUntouchedGenerationManifests(reopenDb, readTr, M);
                afterIds = after.ids;
                afterHashes = after.hashes;

                if (!beforeHashes.SequenceEqual(afterHashes))
                {
                    throw new StageFault("UPDATE_MODIFIED_OTHER_MANHOLE", M, ["unchanged_generation_manifests"],
                        string.Join(";", beforeHashes), string.Join(";", afterHashes));
                }
                readTr.Commit();
            }

            return new NativeV2(
                saved_dwg_path: rowSnapshot,
                saved_dwg_sha256: savedSha,
                handles: handleMap.ToArray(),
                normalized_geometry_hash: input.geometry.physical_hash,
                normalized_rebar_hash: input.rebar.physical_hash,
                repository_version: GenerationRepository.SnapshotVersion,
                write_status: "Committed",
                readback_status: "Passed",
                unchanged_manhole_ids: beforeIds.ToArray(),
                unchanged_before_hashes: beforeHashes.ToArray(),
                unchanged_after_hashes: afterHashes.ToArray(),
                domain_geometry_hash: input.rebar.fabrication_geometry_hash,
                expected_projection_hash: input.sheet.sheet_hash,
                commit_state: "Reopened"
            );
        }
        catch (StageFault)
        {
            throw;
        }
        catch (Exception ex)
        {
            string state = reopened ? "Reopened" : saved ? "Saved" : committed ? "Committed" : "NotCommitted";
            if (committed)
            {
                throw new StageFault("NATIVE_FAILURE_AFTER_COMMIT", M, ["commit_state", "target_dwg_path"],
                    "save/readback completed", $"{state}:{ex.Message}");
            }
            else
            {
                throw new StageFault("NATIVE_WRITE_ROLLED_BACK", M, ["commit_state"],
                    "transaction committed", ex.Message);
            }
        }
    }

    private static OperationResult<GenerationArtifact> Fail(string code, string message) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message)]);
}
