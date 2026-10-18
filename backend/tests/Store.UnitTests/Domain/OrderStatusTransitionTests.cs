using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Domain.Exceptions;

namespace Store.UnitTests.Domain;

// Rule 4: Pending > Confirmed > Shipped, Pending/Confirmed > Cancelled, Shipped and Cancelled are final.
public class OrderStatusTransitionTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    public void Allowed_transitions_succeed(OrderStatus from, OrderStatus to)
    {
        var order = new Order { Status = from };

        order.ChangeStatus(to, Now);

        Assert.Equal(to, order.Status);
        Assert.Equal(Now, order.UpdatedAt);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Pending)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending)]
    public void Invalid_transitions_throw_and_leave_status_unchanged(OrderStatus from, OrderStatus to)
    {
        var order = new Order { Status = from };

        var ex = Assert.Throws<InvalidStatusTransitionException>(() => order.ChangeStatus(to, Now));

        Assert.Equal(from, order.Status);
        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void Final_states_report_a_clear_message()
    {
        var order = new Order { Status = OrderStatus.Shipped };

        var ex = Assert.Throws<InvalidStatusTransitionException>(() => order.ChangeStatus(OrderStatus.Cancelled, Now));

        Assert.Equal("Order is Shipped and can no longer be changed.", ex.Message);
    }
}
