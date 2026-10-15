using DBTool.Domain;

namespace DBTool.Application;

public interface ICadHostDispatcher
{
    OperationResult<T> Execute<T>(Func<T> operation) where T : class;

    Task<OperationResult<T>> ExecuteAsync<T>(
        string expectedDocumentToken,
        Func<T> operation,
        CancellationToken cancellationToken) where T : class;
}
