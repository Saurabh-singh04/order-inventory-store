using Store.Domain.Services;

namespace Store.UnitTests.Domain;

// Rule 5: discounts are calculated on the server, decimal, rounded to 2 places.
public class DiscountCalculatorTests
{
    [Theory]
    [InlineData(500, 1)]
    [InlineData(10_000, 1)]      // "above 10,000" - exactly 10,000 does not qualify
    [InlineData(9_999.99, 4)]
    public void No_discount_at_or_below_threshold_and_not_fifth_order(decimal subTotal, int orderNumber)
    {
        var result = DiscountCalculator.Calculate(subTotal, orderNumber);

        Assert.Equal(0m, result.Discount);
        Assert.Equal(subTotal, result.Total);
    }

    [Fact]
    public void Five_percent_off_when_sub_total_is_above_10000()
    {
        var result = DiscountCalculator.Calculate(20_000m, customerOrderNumber: 1);

        Assert.Equal(1_000m, result.Discount);
        Assert.Equal(19_000m, result.Total);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    public void Extra_ten_percent_on_every_fifth_order(int orderNumber)
    {
        var result = DiscountCalculator.Calculate(1_000m, orderNumber);

        Assert.Equal(100m, result.Discount);
        Assert.Equal(900m, result.Total);
    }

    [Fact]
    public void Both_discounts_stack_on_a_large_fifth_order()
    {
        var result = DiscountCalculator.Calculate(20_000m, customerOrderNumber: 5);

        Assert.Equal(3_000m, result.Discount); // 5% + 10% of 20,000
        Assert.Equal(17_000m, result.Total);
    }

    [Fact]
    public void Discount_is_rounded_to_two_decimal_places()
    {
        // 12,345.67 * 5% = 617.2835 -> 617.28
        var result = DiscountCalculator.Calculate(12_345.67m, customerOrderNumber: 1);

        Assert.Equal(617.28m, result.Discount);
        Assert.Equal(11_728.39m, result.Total);
        Assert.Equal(result.SubTotal, result.Discount + result.Total);
    }

    [Fact]
    public void Midpoint_is_rounded_away_from_zero()
    {
        // 10,000.10 * 5% = 500.005 -> 500.01 (banker's rounding would give 500.00)
        var result = DiscountCalculator.Calculate(10_000.10m, customerOrderNumber: 1);

        Assert.Equal(500.01m, result.Discount);
    }
}
