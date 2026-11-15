using HotChocolate.ApolloFederation.Types;
using HotChocolate.Types;

namespace ReceiptSubgraph.Domain;

[InterfaceType("Error")]
public interface IError
{
    string Message { get; }
}

// The Customer subgraph owns the root query and returns these in its response unions,
// so they are declared @shareable here to keep supergraph composition valid.

[Shareable]
public sealed record NotFoundError(string Message) : IError;

[Shareable]
public sealed record UnauthorizedError(string Message) : IError;

[Shareable]
public sealed record InternalServerError(string Message) : IError;
