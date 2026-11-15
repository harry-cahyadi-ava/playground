using System.Text.Json;
using HotChocolate.Features;
using HotChocolate.Language;
using HotChocolate.Text.Json;
using HotChocolate.Types;

namespace ReceiptSubgraph.GraphQL.Scalars;

/// <summary>
/// Monetary amount backed by <see cref="decimal"/> so that currency values never suffer from
/// binary floating point rounding. Serialized as a JSON number.
/// </summary>
public sealed class MoneyType : ScalarType<decimal, IValueNode>
{
    public MoneyType() : base("Money")
    {
        Description = "A monetary amount in the store currency, serialised as a JSON number, e.g. 12.35.";
    }

    protected override decimal OnCoerceInputLiteral(IValueNode valueLiteral)
        => valueLiteral switch
        {
            IntValueNode intValue => intValue.ToDecimal(),
            FloatValueNode floatValue => floatValue.ToDecimal(),
            _ => throw new LeafCoercionException("Money must be a number literal.", this),
        };

    protected override decimal OnCoerceInputValue(JsonElement inputValue, IFeatureProvider context)
        => inputValue.ValueKind == JsonValueKind.Number && inputValue.TryGetDecimal(out var value)
            ? value
            : throw new LeafCoercionException("Money must be a number.", this);

    protected override void OnCoerceOutputValue(decimal runtimeValue, ResultElement resultValue)
        => resultValue.SetNumberValue(runtimeValue);

    protected override IValueNode OnValueToLiteral(decimal runtimeValue)
        => new FloatValueNode(runtimeValue);
}
