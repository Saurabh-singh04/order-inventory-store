using Store.Domain.Enums;

namespace Store.Domain.Exceptions;

public sealed class InvalidStatusTransitionException : DomainException
{
    public InvalidStatusTransitionException(OrderStatus from, OrderStatus to)
        : base(BuildMessage(from, to))
    {
        From = from;
        To = to;
    }

    public OrderStatus From { get; }
    public OrderStatus To { get; }

    private static string BuildMessage(OrderStatus from, OrderStatus to)
    {
        if (from == to)
            return $"Order is already {from}.";

        if (from is OrderStatus.Shipped or OrderStatus.Cancelled)
            return $"Order is {from} and can no longer be changed.";

        return $"Cannot change order status from {from} to {to}. Allowed: Pending -> Confirmed -> Shipped, or Pending/Confirmed -> Cancelled.";
    }
}
