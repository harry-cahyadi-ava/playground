using HotChocolate.Types;

namespace ReceiptSubgraph.Domain;

/// <summary>The result of the <c>receipt</c> query: the receipt, or one of the errors.</summary>
[UnionType("ReceiptResponse")]
public interface IReceiptResponse;

/// <summary>The result of the <c>receipts</c> query: a page of receipts, or one of the errors.</summary>
[UnionType("ReceiptsResponse")]
public interface IReceiptsResponse;
