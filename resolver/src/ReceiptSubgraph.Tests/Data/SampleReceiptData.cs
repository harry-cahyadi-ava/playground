using ReceiptSubgraph.Domain;

namespace ReceiptSubgraph.Tests.Data;

/// <summary>Deterministic sample receipts used by the GraphQL tests in place of a backing service.</summary>
internal static class SampleReceiptData
{
    public const string CustomerId = "customer-1";
    public const string OnlineOrderId = "order-1001";

    public static IReadOnlyList<Receipt> Create() =>
    [
        // In-store purchase paid with a card, with Flybuys points.
        new Receipt
        {
            Id = "receipt-1",
            StoreId = "store-0042",
            StoreBrand = StoreBrand.ColesSupermarket,
            SalesChannel = TransactionSalesChannel.ColesSupermarket,
            CustomerId = CustomerId,
            CreatedAt = new DateTimeOffset(2025, 11, 2, 9, 15, 0, TimeSpan.Zero),
            Amount = new ReceiptAmount
            {
                Total = 14.80m,
                Gst = 0.45m,
                Savings = 1.20m,
                ShippingCharge = 0m,
                FinancialServicesDiscount = 0m,
                LoyaltyDiscount = 0m,
                TeamMemberDiscount = 0m,
            },
            Items =
            [
                new ReceiptItem
                {
                    Details = new ReceiptItemProduct
                    {
                        ProductId = "product-milk",
                        Name = "Full Cream Milk 2L",
                        Quantity = 2,
                        QuantityType = QuantityType.Count,
                        UnitPrice = 3.40m,
                        UnitOfMeasure = ProductUnitOfMeasure.Each,
                        TotalPrice = 6.80m,
                        Taxable = false,
                    },
                },
                new ReceiptItem
                {
                    Details = new ReceiptItemProduct
                    {
                        ProductId = "product-apples",
                        Name = "Pink Lady Apples",
                        Quantity = 0.8,
                        QuantityType = QuantityType.Weighted,
                        UnitPrice = 6.00m,
                        UnitOfMeasure = ProductUnitOfMeasure.Grams,
                        TotalPrice = 4.80m,
                        Taxable = false,
                    },
                },
                new ReceiptItem { Details = new ReceiptItemBag { Quantity = 1, UnitPrice = 0.15m, TotalPrice = 0.15m } },
            ],
            Tenders =
            [
                new ReceiptTender { Id = "tender-1", Type = ReceiptPaymentMethodType.Eftpos, Name = "Eftpos", Amount = 14.80m },
            ],
            Flybuys = new ReceiptFlybuys
            {
                Barcode = "9876543210123",
                MemberNumber = "1234567890123456",
                BasePointsEarned = 14,
                BonusPointsEarned = 10,
            },
        },

        // Online order, delivered, with a substituted item and a tax invoice.
        new Receipt
        {
            Id = "receipt-2",
            StoreId = "store-0100",
            StoreBrand = StoreBrand.ColesSupermarket,
            SalesChannel = TransactionSalesChannel.ColesOnline,
            CustomerId = CustomerId,
            OrderId = OnlineOrderId,
            CreatedAt = new DateTimeOffset(2025, 11, 5, 18, 30, 0, TimeSpan.Zero),
            Amount = new ReceiptAmount
            {
                Total = 86.45m,
                Gst = 2.10m,
                Savings = 7.55m,
                ShippingCharge = 12m,
                FinancialServicesDiscount = 0m,
                LoyaltyDiscount = 1m,
                TeamMemberDiscount = 0m,
            },
            CreditApplied = 5m,
            InvoiceType = ReceiptInvoiceType.Created,
            InvoiceDetails = new ReceiptInvoiceDetails { Name = "Sample Pty Ltd", Abn = "51 824 753 556", PoNumber = "PO-7781" },
            IsTaxInvoiceAvailable = true,
            Items =
            [
                new ReceiptItem
                {
                    Details = new ReceiptItemProduct
                    {
                        ProductId = "product-bread",
                        Name = "Wholemeal Bread",
                        Quantity = 1,
                        QuantityOrdered = 1,
                        QuantityType = QuantityType.Count,
                        UnitPrice = 4.50m,
                        TotalPrice = 4.50m,
                    },
                    Promotion = new ReceiptItemPromotion
                    {
                        Type = ReceiptItemPromotionType.ColesPlusSaverDiscount,
                        OriginalUnitPrice = 5.00m,
                    },
                },
                new ReceiptItem
                {
                    Details = new ReceiptItemProduct
                    {
                        ProductId = "product-oat-milk",
                        Name = "Oat Milk 1L",
                        Quantity = 1,
                        QuantityOrdered = 1,
                        QuantityType = QuantityType.Count,
                        UnitPrice = 3.20m,
                        TotalPrice = 3.20m,
                    },
                    Substitutions =
                    [
                        new ReceiptSubstitutedItem
                        {
                            Product = new ReceiptItemProduct
                            {
                                ProductId = "product-almond-milk",
                                Name = "Almond Milk 1L",
                                Quantity = 1,
                                UnitPrice = 3.80m,
                                TotalPrice = 3.80m,
                            },
                        },
                    ],
                },
            ],
            Payments =
            [
                new ReceiptPayment
                {
                    PurchaseAmount = 81.45m,
                    PaymentMethod = new ReceiptPaymentMethod
                    {
                        Type = ReceiptPaymentMethodType.Credit,
                        Scheme = ReceiptPaymentMethodScheme.Visa,
                        TruncatedCard = "**** 4242",
                    },
                    Metadata = new ReceiptPaymentMetadata
                    {
                        MerchantName = "Coles Online",
                        MerchantState = "VIC",
                        MerchantCountry = "AU",
                        TransactionDate = new DateTimeOffset(2025, 11, 5, 18, 29, 0, TimeSpan.Zero),
                        AuthorizationCode = "A1B2C3",
                        ResponseCode = "00",
                        ResponseMessage = "Approved",
                    },
                },
            ],
            Flybuys = new ReceiptFlybuys { MemberNumber = "1234567890123456", BasePointsEarned = 86 },
        },

        // Partial return against the same online order.
        new Receipt
        {
            Id = "receipt-3",
            StoreId = "store-0100",
            StoreBrand = StoreBrand.ColesSupermarket,
            SalesChannel = TransactionSalesChannel.ColesOnline,
            CustomerId = CustomerId,
            OrderId = OnlineOrderId,
            CreatedAt = new DateTimeOffset(2025, 11, 7, 10, 0, 0, TimeSpan.Zero),
            IsReturn = true,
            InvoiceType = ReceiptInvoiceType.Refunded,
            Amount = new ReceiptAmount
            {
                Total = -4.50m,
                Gst = 0m,
                Savings = 0m,
                ShippingCharge = 0m,
                FinancialServicesDiscount = 0m,
                LoyaltyDiscount = 0m,
                TeamMemberDiscount = 0m,
            },
            Items =
            [
                new ReceiptItem
                {
                    Details = new ReceiptItemProduct
                    {
                        ProductId = "product-bread",
                        Name = "Wholemeal Bread",
                        Quantity = -1,
                        UnitPrice = 4.50m,
                        TotalPrice = -4.50m,
                        IsReturn = true,
                    },
                },
            ],
            Payments =
            [
                new ReceiptPayment
                {
                    PurchaseAmount = 0m,
                    RefundAmount = 4.50m,
                    PaymentMethod = new ReceiptPaymentMethod
                    {
                        Type = ReceiptPaymentMethodType.Credit,
                        Scheme = ReceiptPaymentMethodScheme.Visa,
                        TruncatedCard = "**** 4242",
                    },
                },
            ],
        },

        // Anonymous in-store liquor purchase: no customer, paid in cash.
        new Receipt
        {
            Id = "receipt-4",
            StoreId = "store-9001",
            StoreBrand = StoreBrand.Liquorland,
            SalesChannel = TransactionSalesChannel.ColesSupermarket,
            CreatedAt = new DateTimeOffset(2025, 10, 20, 20, 5, 0, TimeSpan.Zero),
            Amount = new ReceiptAmount
            {
                Total = 25.00m,
                Gst = 2.27m,
                Savings = 0m,
                ShippingCharge = 0m,
                FinancialServicesDiscount = 0m,
                LoyaltyDiscount = 0m,
                TeamMemberDiscount = 0m,
            },
            Tenders =
            [
                new ReceiptTender
                {
                    Id = "tender-4",
                    Type = ReceiptPaymentMethodType.Cash,
                    Name = "Cash",
                    Amount = 25.00m,
                    ChangeReturned = true,
                    CashRoundingApplied = true,
                },
            ],
        },
    ];
}
