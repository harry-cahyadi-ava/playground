using ReceiptSubgraph.Data;
using ReceiptSubgraph.Domain;
using ReceiptSubgraph.GraphQL.DataLoaders;
using ReceiptSubgraph.Paging;

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

    [Fact]
    public void Receipt_result_keys_with_equal_inputs_are_equal_so_they_share_a_DataLoader_cache_entry()
    {
        var first = new ReceiptResultKey(
            ReceiptOwnerKind.Customer,
            "c1",
            new ReceiptResultInput(new ReceiptFilter { OrderIds = ["o1"] }, new PageRequest(First: 5)));
        var second = new ReceiptResultKey(
            ReceiptOwnerKind.Customer,
            "c1",
            new ReceiptResultInput(new ReceiptFilter { OrderIds = ["o1"] }, new PageRequest(First: 5)));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
