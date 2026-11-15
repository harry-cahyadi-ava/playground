using HotChocolate;
using HotChocolate.Types;
using ReceiptSubgraph.GraphQL.Scalars;

namespace ReceiptSubgraph.Domain;

/// <summary>The amount that the receipt resolves to (broken down into its various components).</summary>
public sealed record ReceiptAmount
{
    /// <summary>The total amount of the receipt.</summary>
    public required decimal Total { get; init; }

    /// <summary>The GST component of the total amount.</summary>
    public required decimal Gst { get; init; }

    /// <summary>The amount that the total amount was reduced by through the application of promotions.</summary>
    public required decimal Savings { get; init; }

    /// <summary>The shipping charge component of the total amount.</summary>
    public required decimal ShippingCharge { get; init; }

    /// <summary>The amount that the total amount was reduced by through Coles Financial Services discounts.</summary>
    public required decimal FinancialServicesDiscount { get; init; }

    /// <summary>The amount that the total amount was reduced by through the application of a loyalty card.</summary>
    public required decimal LoyaltyDiscount { get; init; }

    /// <summary>The amount that the total amount was reduced by through the application of a team member card.</summary>
    public required decimal TeamMemberDiscount { get; init; }
}

/// <summary>The details captured on a receipt for tax invoice purposes, as supplied at the time of purchase.</summary>
public sealed record ReceiptInvoiceDetails
{
    /// <summary>The customer's name as supplied for the order or receipt.</summary>
    public string? Name { get; init; }

    /// <summary>The Australian Business Number associated with the customer or business account.</summary>
    public string? Abn { get; init; }

    /// <summary>The customer's business cost centre number used for internal account allocation or reporting.</summary>
    public string? CostCentreNumber { get; init; }

    /// <summary>The purchase order number provided by the customer or business account.</summary>
    public string? PoNumber { get; init; }

    /// <summary>The customer's email address as supplied for the order or receipt.</summary>
    public string? Email { get; init; }

    /// <summary>The customer's billing, delivery, or account address as supplied for the order or receipt.</summary>
    public string? Address { get; init; }
}

/// <summary>The properties of the receipt related to Flybuys.</summary>
public sealed record ReceiptFlybuys
{
    /// <summary>The scannable 13-digit EAN-13 barcode number of the Flybuys card attached to the transaction.</summary>
    [GraphQLType<FlybuysBarcodeNumberType>]
    public string? Barcode { get; init; }

    /// <summary>The Flybuys card barcode number of a team member attached to the transaction.</summary>
    [GraphQLType<FlybuysBarcodeNumberType>]
    public string? TeamMemberBarcode { get; init; }

    /// <summary>The customer's 16-digit Flybuys member account number.</summary>
    [GraphQLType<FlybuysMemberNumberType>]
    public string? MemberNumber { get; init; }

    /// <summary>The standard (base) Flybuys points earned, excluding promotional or campaign bonus points.</summary>
    public int? BasePointsEarned { get; init; }

    /// <summary>The additional promotional Flybuys points awarded on top of the standard (base) points.</summary>
    public int? BonusPointsEarned { get; init; }

    /// <summary>The number of Flybuys points redeemed to pay toward the order total.</summary>
    public int? PointsRedeemed { get; init; }

    /// <summary>The points clawed back from the loyalty account, e.g. when items are returned or refunded.</summary>
    public int? PointsDeducted { get; init; }
}

/// <summary>A single payment method or instrument applied to a transaction at checkout.</summary>
public sealed record ReceiptTender
{
    /// <summary>The unique identifier for the tender line, used to tell split payments apart.</summary>
    [ID]
    public required string Id { get; init; }

    /// <summary>The category or classification code of the payment instrument.</summary>
    public required ReceiptPaymentMethodType Type { get; init; }

    /// <summary>The human-readable display name of the payment method shown on the customer-facing receipt.</summary>
    public string? Name { get; init; }

    /// <summary>The amount applied against the order total by this tender instrument.</summary>
    public required decimal Amount { get; init; }

    /// <summary>True if change was given back to the customer for this tender. Relevant for cash transactions.</summary>
    public bool? ChangeReturned { get; init; }

    /// <summary>The cash-back amount requested and dispensed to the customer as part of this tender.</summary>
    public decimal? CashBackAmount { get; init; }

    /// <summary>Indicates whether Australian cash rounding (to the nearest 5 cents) was applied to this tender.</summary>
    public bool? CashRoundingApplied { get; init; }
}

/// <summary>The payment summary for a receipt, including purchase, refund and cash-out amounts and payment details.</summary>
public sealed record ReceiptPayment
{
    /// <summary>The total purchase amount paid by the customer, excluding any refund or cash-out component.</summary>
    public required decimal PurchaseAmount { get; init; }

    /// <summary>The amount refunded to the customer. Null if nothing was refunded.</summary>
    public decimal? RefundAmount { get; init; }

    /// <summary>The cash-out amount dispensed to the customer. Null if there was no cash-out.</summary>
    public decimal? CashOutAmount { get; init; }

    /// <summary>Details of the payment method or card used for this payment.</summary>
    public required ReceiptPaymentMethod PaymentMethod { get; init; }

    /// <summary>Additional payment processing metadata captured from the merchant, terminal or card.</summary>
    public ReceiptPaymentMetadata? Metadata { get; init; }
}

/// <summary>The payment method or card details used for a payment transaction.</summary>
public sealed record ReceiptPaymentMethod
{
    /// <summary>The payment method type.</summary>
    public required ReceiptPaymentMethodType Type { get; init; }

    /// <summary>The card scheme (or network). Null for non-card payment methods.</summary>
    public ReceiptPaymentMethodScheme? Scheme { get; init; }

    /// <summary>The masked card number shown on the receipt, typically only the last few digits.</summary>
    public string? TruncatedCard { get; init; }
}

/// <summary>Additional merchant, terminal, card and authorisation metadata captured for a payment.</summary>
public sealed record ReceiptPaymentMetadata
{
    /// <summary>The merchant display name associated with the payment transaction.</summary>
    public string? MerchantName { get; init; }

    /// <summary>The Australian state or territory where the merchant or store processed the transaction.</summary>
    public string? MerchantState { get; init; }

    /// <summary>The country where the merchant or store processed the transaction.</summary>
    public string? MerchantCountry { get; init; }

    /// <summary>The date and time when the payment was processed by the merchant or terminal.</summary>
    public DateTimeOffset? TransactionDate { get; init; }

    /// <summary>The merchant number or identifier assigned by the payment acquirer.</summary>
    public string? MerchantNumber { get; init; }

    /// <summary>The card acceptor terminal ID identifying the payment terminal.</summary>
    public string? CatId { get; init; }

    /// <summary>The debit account type selected for the transaction, such as cheque, savings, or credit.</summary>
    public string? DebitAccountType { get; init; }

    /// <summary>The card sequence number identifying a specific card issued under the same card account.</summary>
    public string? CardSequenceNumber { get; init; }

    /// <summary>The EMV application label displayed for the card application used.</summary>
    public string? AppLabel { get; init; }

    /// <summary>The EMV application transaction counter value.</summary>
    public string? Atc { get; init; }

    /// <summary>The EMV application identifier for the card application used.</summary>
    public string? Aid { get; init; }

    /// <summary>The retrieval reference number used to trace the payment through acquirer and card network systems.</summary>
    public string? Rrn { get; init; }

    /// <summary>The authorisation code returned by the payment processor or card issuer.</summary>
    public string? AuthorizationCode { get; init; }

    /// <summary>The response code returned by the processor or issuer indicating the authorisation outcome.</summary>
    public string? ResponseCode { get; init; }

    /// <summary>The human-readable response message associated with the authorisation result.</summary>
    public string? ResponseMessage { get; init; }
}
