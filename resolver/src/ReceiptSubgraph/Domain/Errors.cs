using HotChocolate.ApolloFederation.Types;
using HotChocolate.Types;

namespace ReceiptSubgraph.Domain;

[InterfaceType("Error")]
public interface IError
{
    string Message { get; }
}

// Other subgraphs return the same error types in their response unions,
// so they are declared @shareable here to keep supergraph composition valid.

[Shareable]
public sealed record NotFoundError(string Message) : IError, IReceiptResponse;

[Shareable]
public sealed record UnauthorizedError(string Message) : IError, IReceiptResponse, IReceiptsResponse;

[Shareable]
public sealed record InternalServerError(string Message) : IError, IReceiptResponse, IReceiptsResponse;
