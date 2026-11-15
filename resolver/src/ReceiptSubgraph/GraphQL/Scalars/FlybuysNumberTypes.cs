using System.Text.Json;
using HotChocolate.Features;
using HotChocolate.Language;
using HotChocolate.Text.Json;
using HotChocolate.Types;

namespace ReceiptSubgraph.GraphQL.Scalars;

/// <summary>
/// Base class for scalars that are a fixed-length string of digits.
/// Values are validated when they come from a client; output is passed through untouched so that
/// one malformed upstream value can never fail an otherwise valid receipt.
/// </summary>
public abstract class FixedLengthDigitsType : ScalarType<string, StringValueNode>
{
    private readonly int _length;

    protected FixedLengthDigitsType(string name, int length, string description) : base(name)
    {
        _length = length;
        Description = description;
    }

    internal static bool IsValid(ReadOnlySpan<char> value, int length)
    {
        if (value.Length != length)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    protected override string OnCoerceInputLiteral(StringValueNode valueLiteral)
        => Validate(valueLiteral.Value);

    protected override string OnCoerceInputValue(JsonElement inputValue, IFeatureProvider context)
        => Validate(inputValue.GetString());

    protected override void OnCoerceOutputValue(string runtimeValue, ResultElement resultValue)
        => resultValue.SetStringValue(runtimeValue);

    protected override StringValueNode OnValueToLiteral(string runtimeValue)
        => new(runtimeValue);

    private string Validate(string? value)
        => value is not null && IsValid(value, _length)
            ? value
            : throw new LeafCoercionException($"{Name} must be exactly {_length} digits.", this);
}

/// <summary>
/// The scannable 13-digit EAN-13 Flybuys barcode. There is a one-to-one mapping between a barcode
/// number and a member number.
/// </summary>
public sealed class FlybuysBarcodeNumberType()
    : FixedLengthDigitsType(
        "FlybuysBarcodeNumber",
        13,
        "The 13-digit EAN-13 Flybuys barcode number.");

/// <summary>The 16-digit Flybuys member account number.</summary>
public sealed class FlybuysMemberNumberType()
    : FixedLengthDigitsType(
        "FlybuysMemberNumber",
        16,
        "The 16-digit Flybuys member account number.");
