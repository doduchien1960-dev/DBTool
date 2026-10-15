using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using DBTool.Application;
using DBTool.Domain;
using DBTool.Domain.Geometry;
using DBTool.AutoCAD.Generation;
using DBTool.AutoCAD.Render;

namespace DBTool.AutoCAD.Generation;

public sealed class CadWriteContext
{
    internal CadWriteContext(
        Document document,
        Database database,
        Transaction transaction,
        BlockTableRecord modelSpace,
        GenerationRequest request,
        string generationId,
        string inputHash,
        string templateHash,
        ICadPrimitiveSink sink)
    {
        Document = document;
        Database = database;
        Transaction = transaction;
        ModelSpace = modelSpace;
        Request = request;
        GenerationId = generationId;
        InputHash = inputHash;
        TemplateHash = templateHash;
        Sink = sink;
    }

    public Document Document { get; }
    public Database Database { get; }
    public Transaction Transaction { get; }
    public BlockTableRecord ModelSpace { get; }
    public GenerationRequest Request { get; }
    public string GenerationId { get; }
    public string InputHash { get; }
    public string TemplateHash { get; }
    public ICadPrimitiveSink Sink { get; }
    internal OperationResult<RenderedEntityMap>? RenderResult { get; private set; }

    public void SetRenderResult(OperationResult<RenderedEntityMap> result)
    {
        RenderResult = result ?? throw new ArgumentNullException(nameof(result));
    }
}

/// <summary>
/// Owns the sole DocumentLock and Transaction for a CAD write operation.
/// </summary>
public sealed class CadWriteSession
{
    private readonly GenerationRepository _repository;
    private readonly string _inputHash;
    private readonly string _templateHash;

    public CadWriteSession(GenerationRepository repository, string inputHash, string templateHash)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _inputHash = ContractGuard.Hash(inputHash);
        _templateHash = ContractGuard.Hash(templateHash);
    }

    public OperationResult<GenerationArtifact> Run(
        Document document,
        GenerationRequest request,
        Action<CadWriteContext> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(operation);

        var committed = false;
        GenerationArtifact? committedArtifact = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentToken = _repository.GetDocumentToken(document);
            if (request.ExpectedDocumentToken is not null &&
                !string.Equals(request.ExpectedDocumentToken, currentToken, StringComparison.Ordinal))
                return Block("DOC_CONTEXT_CHANGED", "The expected document context is no longer active.");
            if (!string.Equals(request.ExpectedInputHash, _inputHash, StringComparison.OrdinalIgnoreCase))
                return Block("GENERATION_INPUT_HASH_MISMATCH", "The write request does not match the package input hash.");
            if (request.Mode == GenerationMode.Replace && string.IsNullOrWhiteSpace(request.ExpectedGenerationId))
                return Block("GENERATION_EXPECTATION_MISSING", "Replace requires an expected generation identity.");

            using var documentLock = document.LockDocument();
            using var transaction = document.Database.TransactionManager.StartTransaction();
            _repository.EnsureSchema(document.Database, transaction);

            if (request.Mode == GenerationMode.Replace)
            {
                var existing = _repository.ReadGeneration(document.Database, transaction, request.ExpectedGenerationId!);
                if (existing is null)
                    return Block("GENERATION_NOT_FOUND", "The expected generation is not present in this document.");
                if (!string.Equals(existing.InputHash, request.ExpectedInputHash, StringComparison.OrdinalIgnoreCase))
                    return Block("GENERATION_STALE", "The expected generation belongs to a different input hash.");
                _repository.EraseGeneration(document.Database, transaction, request.ExpectedGenerationId!);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var generationId = request.Mode == GenerationMode.Replace
                ? request.ExpectedGenerationId!
                : request.RequestedGenerationId ?? Guid.NewGuid().ToString("N");
            if (request.Mode == GenerationMode.Create && _repository.ReadGeneration(document.Database, transaction, generationId) is not null)
                return Block("GENERATION_ALREADY_EXISTS", "Create cannot reuse an existing generation identity; use Replace.");
            var blockTable = (BlockTable)transaction.GetObject(document.Database.BlockTableId, OpenMode.ForRead);
            var modelSpace = (BlockTableRecord)transaction.GetObject(
                blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var sink = new CadPrimitiveSink(document.Database, transaction, modelSpace, request.InsertionPoint,
                _repository, generationId, _inputHash, _templateHash);
            var context = new CadWriteContext(document, document.Database, transaction, modelSpace, request,
                generationId, _inputHash, _templateHash, sink);
            operation(context);

            if (context.RenderResult is null)
                return Block("CAD_RENDER_RESULT_MISSING", "The renderer did not publish a result.");
            if (!context.RenderResult.Success)
                return new(false, null, context.RenderResult.Diagnostics);

            cancellationToken.ThrowIfCancellationRequested();
            transaction.Commit();
            committed = true;
            committedArtifact = new GenerationArtifact(currentToken, generationId, _inputHash, _templateHash,
                GenerationRepository.SnapshotVersion, context.RenderResult.Value!.Entities, true);
            var diagnostics = cancellationToken.IsCancellationRequested
                ? new Diagnostic("OPERATION_CANCELLED_AFTER_COMMIT", DiagnosticSeverity.Review, DiagnosticScope.Global,
                    null, null, null, "committed", "cancelled", "Cancellation arrived after the CAD transaction committed.")
                : null;
            return diagnostics is null ? new(true, committedArtifact, []) : new(true, committedArtifact, [diagnostics]);
        }
        catch (OperationCanceledException)
        {
            return committed
                ? new(true, committedArtifact!, [new Diagnostic("OPERATION_CANCELLED_AFTER_COMMIT", DiagnosticSeverity.Review,
                    DiagnosticScope.Global, null, null, null, "committed", "cancelled",
                    "Cancellation arrived after the CAD transaction committed.")])
                : Block("OPERATION_CANCELLED", "CAD write cancelled before commit.");
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            return Block("CAD_WRITE_FAILED", ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return Block("CAD_WRITE_FAILED", ex.Message);
        }
    }

    private static OperationResult<GenerationArtifact> Block(string code, string message) =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message)]);
}
