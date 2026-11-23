using HotChocolate.Types;

namespace ReceiptSubgraph.Domain;

/// <summary>The result of the <c>transaction</c> query: the receipt, or one of the errors.</summary>
[UnionType("TransactionResponse")]
public interface IReceiptResponse;

/// <summary>The result of the <c>transactions</c> query: a page of receipts, or one of the errors.</summary>
[UnionType("TransactionsResponse")]
public interface IReceiptsResponse;
