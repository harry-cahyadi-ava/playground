using HotChocolate;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using Microsoft.Extensions.Logging;
using ReceiptSubgraph.Authorization;
using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.GraphQL.DataLoaders;
using ReceiptSubgraph.Paging;

namespace ReceiptSubgraph.GraphQL.Types;

/// <summary>
/// The root queries. Both are scoped to the signed-in customer taken from the request's <see cref="UserContext"/>:
/// a receipt that belongs to anyone else is reported exactly like one that does not exist.
/// </summary>
[QueryType]
public static partial class ReceiptQueries
{
    private const string UnauthorizedMessage = "A signed-in customer is required to view receipts.";
    private const string InternalErrorMessage = "An unexpected error occurred while retrieving receipts.";

    /// <summary>Get the receipt for the given ID.</summary>
    public static async Task<IReceiptResponse> GetReceiptAsync(
        [ID] string id,
        IResolverContext context,
        IReceiptByIdDataLoader receiptById,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (GetCustomerId(context) is not { } customerId)
        {
            return new UnauthorizedError(UnauthorizedMessage);
        }

        try
        {
            var receipt = await receiptById.LoadAsync(id, cancellationToken);

            return receipt is not null && receipt.CustomerId == customerId
                ? receipt
                : new NotFoundError($"Receipt '{id}' was not found.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggerFactory.CreateLogger(typeof(ReceiptQueries)).LogError(exception, "Failed to load receipt {ReceiptId}.", id);
            return new InternalServerError(InternalErrorMessage);
        }
    }

    /// <summary>Get a paginated list of the signed-in customer's receipts, newest first.</summary>
    public static async Task<IReceiptsResponse> GetReceiptsAsync(
        ReceiptResultInput? input,
        IResolverContext context,
        IReceiptRepository repository,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (GetCustomerId(context) is not { } customerId)
        {
            return new UnauthorizedError(UnauthorizedMessage);
        }

        try
        {
            var page = await repository.GetByCustomerAsync(customerId, input, cancellationToken);

            return ReceiptConnection.From(page);
        }
        // Invalid paging arguments are the client's mistake and are reported by PaginationErrorFilter.
        catch (Exception exception) when (exception is not (OperationCanceledException or InvalidPaginationException))
        {
            loggerFactory.CreateLogger(typeof(ReceiptQueries)).LogError(exception, "Failed to load receipts for customer {CustomerId}.", customerId);
            return new InternalServerError(InternalErrorMessage);
        }
    }

    private static string? GetCustomerId(IResolverContext context)
        => context.GetGlobalStateOrDefault<UserContext>(UserContext.GlobalStateKey)?.CustomerId is { Length: > 0 } customerId
            ? customerId
            : null;
}
