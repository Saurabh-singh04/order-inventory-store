namespace Store.Domain.Services;

public readonly record struct DiscountResult(decimal SubTotal, decimal Discount, decimal Total);

/// <summary>
/// Discount rules:
///  - 5% off when the sub total is above 10,000
///  - an extra 10% off on every 5th order of a customer
/// Both percentages are taken from the sub total, so a 5th order above 10,000 gets 15% off.
/// </summary>
public static class DiscountCalculator
{
    public const decimal LargeOrderThreshold = 10_000m;
    public const decimal LargeOrderRate = 0.05m;
    public const decimal LoyaltyRate = 0.10m;
    public const int LoyaltyEveryNthOrder = 5;

    public static DiscountResult Calculate(decimal subTotal, int customerOrderNumber)
    {
        if (subTotal < 0)
            throw new ArgumentOutOfRangeException(nameof(subTotal), "Sub total cannot be negative.");
        if (customerOrderNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(customerOrderNumber), "Order number starts at 1.");

        subTotal = Round(subTotal);

        var rate = 0m;
        if (subTotal > LargeOrderThreshold)
            rate += LargeOrderRate;
        if (customerOrderNumber % LoyaltyEveryNthOrder == 0)
            rate += LoyaltyRate;

        var discount = Round(subTotal * rate);
        return new DiscountResult(subTotal, discount, subTotal - discount);
    }

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
