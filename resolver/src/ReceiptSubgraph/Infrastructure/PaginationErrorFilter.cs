using HotChocolate;
using HotChocolate.Execution;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.Infrastructure;

/// <summary>
/// Turns invalid client pagination arguments into a clear, coded GraphQL error instead of the generic
/// "Unexpected Execution Error" that unhandled exceptions are reported as.
/// </summary>
public sealed class PaginationErrorFilter : IErrorFilter
{
    public const string ErrorCode = "INVALID_PAGINATION";

    public IError OnError(IError error)
        => error.Exception is InvalidPaginationException exception
            ? ErrorBuilder
                .FromError(error)
                .SetMessage(exception.Message)
                .SetCode(ErrorCode)
                .SetException(null)
                .Build()
            : error;
}
