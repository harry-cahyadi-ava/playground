using ReceiptSubgraph.Domain;

namespace ReceiptSubgraph.Tests.Domain;

public class ReceiptFilterEqualityTests
{
    [Fact]
    public void Filters_with_the_same_values_are_equal_even_when_the_id_lists_are_different_instances()
    {
        var a = new ReceiptFilter { OrderIds = ["o1", "o2"], StoreId = "s1" };
        var b = new ReceiptFilter { OrderIds = ["o1", "o2"], StoreId = "s1" };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Filters_with_different_order_ids_are_not_equal()
    {
        Assert.NotEqual(
            new ReceiptFilter { OrderIds = ["o1"] },
            new ReceiptFilter { OrderIds = ["o2"] });
    }

    [Fact]
    public void A_filter_with_order_ids_differs_from_one_without()
    {
        Assert.NotEqual(new ReceiptFilter { OrderIds = ["o1"] }, new ReceiptFilter());
    }
}
