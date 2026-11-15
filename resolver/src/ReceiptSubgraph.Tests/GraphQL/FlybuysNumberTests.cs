using ReceiptSubgraph.GraphQL.Scalars;

namespace ReceiptSubgraph.Tests.GraphQL;

public class FlybuysNumberTests
{
    [Theory]
    [InlineData("9876543210123", 13, true)]
    [InlineData("0000000000000", 13, true)]
    [InlineData("987654321012", 13, false)] // too short
    [InlineData("98765432101234", 13, false)] // too long
    [InlineData("98765432101a3", 13, false)] // not a digit
    [InlineData("", 13, false)]
    [InlineData("1234567890123456", 16, true)]
    [InlineData("١٢٣٤٥٦٧٨٩٠١٢٣٤٥٦", 16, false)] // non-ASCII digits are not accepted
    public void Digit_strings_must_have_the_exact_length_and_only_ASCII_digits(string value, int length, bool expected)
    {
        Assert.Equal(expected, FixedLengthDigitsType.IsValid(value, length));
    }

    [Fact]
    public void Barcode_and_member_number_scalars_have_the_names_used_in_the_contract()
    {
        Assert.Equal("FlybuysBarcodeNumber", new FlybuysBarcodeNumberType().Name);
        Assert.Equal("FlybuysMemberNumber", new FlybuysMemberNumberType().Name);
        Assert.Equal("Money", new MoneyType().Name);
    }
}
