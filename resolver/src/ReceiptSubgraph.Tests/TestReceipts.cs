using ReceiptSubgraph.Domain;

namespace ReceiptSubgraph.Tests;

/// <summary>Small builders that keep test receipts readable: only the properties that matter are specified.</summary>
internal static class TestReceipts
{
    public static Receipt Create(
        string id,
        DateTimeOffset? createdAt = null,
        string? customerId = "customer-1",
        string? orderId = null,
        string storeId = "store-1",
        StoreBrand brand = StoreBrand.ColesSupermarket,
        TransactionSalesChannel channel = TransactionSalesChannel.ColesSupermarket,
        int itemCount = 0)
        => new()
        {
            Id = id,
            CreatedAt = createdAt ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CustomerId = customerId,
            OrderId = orderId,
            StoreId = storeId,
            StoreBrand = brand,
            SalesChannel = channel,
            Amount = new ReceiptAmount
            {
                Total = 10m,
                Gst = 1m,
                Savings = 0m,
                ShippingCharge = 0m,
                FinancialServicesDiscount = 0m,
                LoyaltyDiscount = 0m,
                TeamMemberDiscount = 0m,
            },
            Items = Enumerable.Range(1, itemCount).Select(CreateItem).ToArray(),
        };

    public static ReceiptItem CreateItem(int number)
        => new()
        {
            Details = new ReceiptItemProduct
            {
                ProductId = $"product-{number}",
                Name = $"Item {number}",
                Quantity = 1,
                UnitPrice = number,
                TotalPrice = number,
            },
        };

    public static DateTimeOffset Day(int day) => new(2025, 1, day, 12, 0, 0, TimeSpan.Zero);
}
