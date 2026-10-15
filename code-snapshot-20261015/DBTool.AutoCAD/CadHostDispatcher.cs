using Autodesk.AutoCAD.ApplicationServices;
using AutodeskApplication = Autodesk.AutoCAD.ApplicationServices.Application;
using DBTool.Application;
using DBTool.Domain;
using DBTool.AutoCAD.Generation;

namespace DBTool.AutoCAD;

public sealed class CadHostDispatcher : ICadHostDispatcher
{
    private readonly GenerationRepository _repository = new();

    public OperationResult<T> Execute<T>(Func<T> operation) where T : class
    {
        ArgumentNullException.ThrowIfNull(operation);
        try
        {
            var value = operation();
            return new(true, value, []);
        }
        catch (OperationCanceledException)
        {
            return Fail<T>("OPERATION_CANCELLED", "The AutoCAD command context operation was cancelled.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return Fail<T>("CAD_HOST_DISPATCH_FAILED", ex.Message);
        }
    }

    public Task<OperationResult<T>> ExecuteAsync<T>(string expectedDocumentToken, Func<T> operation, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = AutodeskApplication.DocumentManager.MdiActiveDocument;
            if (document is null)
                return Task.FromResult(Fail<T>("CAD_DOCUMENT_MISSING", "No active AutoCAD document is available."));
            var actual = _repository.GetDocumentToken(document);
            if (!string.Equals(actual, expectedDocumentToken, StringComparison.Ordinal))
                return Task.FromResult(Fail<T>("DOC_CONTEXT_CHANGED", "The active document changed before dispatch."));
            return Task.FromResult(Execute(operation));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Fail<T>("OPERATION_CANCELLED", "The AutoCAD command context operation was cancelled."));
        }
    }

    private static OperationResult<T> Fail<T>(string code, string message) where T : class =>
        new(false, null, [new Diagnostic(code, DiagnosticSeverity.Block, DiagnosticScope.Global,
            null, null, null, null, null, message)]);
}
