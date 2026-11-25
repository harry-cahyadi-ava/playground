using HotChocolate;
using ReceiptSubgraph.GraphQL.Directives;

namespace ReceiptSubgraph.Domain;

/// <summary>Represents the types of invoice associated with a receipt.</summary>
[GraphQLName("TransactionInvoiceType")]
public enum ReceiptInvoiceType
{
    Created,
    Amended,
    Redelivered,
    Refunded,
    ColesplusSubscription,
    ColesplusSubscriptionRefunded,
}

/// <summary>Represents the types of promotions applicable to a receipt item.</summary>
[GraphQLName("TransactionItemPromotionType")]
public enum ReceiptItemPromotionType
{
    ColesPlusSaverDiscount,
}

/// <summary>Represents the types of quantity measurements for a receipt item.</summary>
[GraphQLName("TransactionQuantityType")]
public enum QuantityType
{
    Count,
    Weighted,
}

/// <summary>Represents the types of payment methods available for a receipt payment.</summary>
[GraphQLName("TransactionPaymentMethodType")]
public enum ReceiptPaymentMethodType
{
    Credit,
    Debit,
    Eftpos,
    Cash,
    GiftCard,
    FlybuysDollars,
    Cheque,
    Paypal,
    Flypay,
    Card,
    ApplePay,
    GooglePay,
    SavedCard,
    SavedPaypal,
    BillMeLater,
}

/// <summary>Represents the card schemes (or networks) available for a receipt payment.</summary>
[GraphQLName("TransactionPaymentMethodScheme")]
public enum ReceiptPaymentMethodScheme
{
    Visa,
    Mastercard,
    Amex,
    Eftpos,
    Diners,
    Jcb,
    Unionpay,
}

/// <summary>
/// The brand of the store.
/// Each value carries @metadata describing the brand ABN, context, entity name and Store Locator id.
/// </summary>
public enum StoreBrand
{
    [Metadata("abn", "45 004 189 708")]
    [Metadata("ctx", "SMKT_CS")]
    [Metadata("entity", "COLES SUPERMARKETS AUSTRALIA PTY LTD")]
    [Metadata("id", "2")]
    ColesSupermarket,

    [Metadata("abn", "78 104 811 216")]
    [Metadata("ctx", "CEXP_CE")]
    [Metadata("entity", "Eureka Operations Pty Ltd")]
    [Metadata("id", "1")]
    ColesExpress,

    [Metadata("abn", "82 007 512 414")]
    [Metadata("ctx", "LIQ_LL")]
    [Metadata("entity", "LIQUORLAND (AUSTRALIA) PTY. LTD.")]
    [Metadata("id", "3")]
    Liquorland,

    [Metadata("abn", "82 007 512 414")]
    [Metadata("ctx", "LIQ_1C")]
    [Metadata("entity", "LIQUORLAND (AUSTRALIA) PTY. LTD.")]
    [Metadata("id", "4")]
    FirstChoiceLiquor,

    [Metadata("abn", "82 007 512 414")]
    [Metadata("ctx", "LIQ_VC")]
    [Metadata("entity", "LIQUORLAND (AUSTRALIA) PTY. LTD.")]
    [Metadata("id", "5")]
    VintageCellars,
}

/// <summary>The sales channel of the store.</summary>
public enum TransactionSalesChannel
{
    [Metadata("id", "CS")]
    ColesSupermarket,

    [Metadata("id", "CO_CS")]
    ColesOnline,
}

/// <summary>The standard unit used to measure a product.</summary>
public enum ProductUnitOfMeasure
{
    Each,
    Grams,
}
