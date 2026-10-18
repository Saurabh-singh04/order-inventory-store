using Store.Application.Common;
using Store.Domain.Enums;
using Store.Domain.Exceptions;
using Store.UnitTests.TestHelpers;
using static Store.UnitTests.TestHelpers.OrderTestContext;

namespace Store.UnitTests.Orders;

// Rule 4 at service level + rule 6 (auto-cancel of stale Pending orders).
public class StatusChangeTests : IDisposable
{
    private readonly OrderTestContext _ctx = new();

    private async Task<int> PlaceAsync(int quantity = 4, string key = "k1", int customerId = CustomerId)
    {
        var result = await _ctx.CreateService().PlaceOrderAsync(customerId, Request((MouseId, quantity)), key, default);
        return result.Order.Id;
    }

    [Fact]
    public async Task Cancelling_restores_stock()
    {
        var orderId = await PlaceAsync(quantity: 4);
        Assert.Equal(16, _ctx.StockOf(MouseId));

        var dto = await _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Cancelled, Customer(), default);

        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal(20, _ctx.StockOf(MouseId));
    }

    [Fact]
    public async Task Admin_can_cancel_a_confirmed_order_and_stock_comes_back()
    {
        var orderId = await PlaceAsync(quantity: 2);
        await _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Confirmed, Admin(), default);

        await _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Cancelled, Admin(), default);

        Assert.Equal(20, _ctx.StockOf(MouseId));
    }

    [Fact]
    public async Task Cancelled_order_cannot_be_cancelled_again_so_stock_is_not_restored_twice()
    {
        var orderId = await PlaceAsync(quantity: 4);
        await _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Cancelled, Admin(), default);

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Cancelled, Admin(), default));

        Assert.Equal(20, _ctx.StockOf(MouseId));
    }

    [Fact]
    public async Task Shipped_order_cannot_be_cancelled()
    {
        var orderId = await PlaceAsync();
        await _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Confirmed, Admin(), default);
        await _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Shipped, Admin(), default);

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(() =>
            _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Cancelled, Admin(), default));

        Assert.Equal(16, _ctx.StockOf(MouseId));
    }

    [Fact]
    public async Task Customer_cannot_cancel_after_confirmation()
    {
        var orderId = await PlaceAsync();
        await _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Confirmed, Admin(), default);

        await Assert.ThrowsAsync<DomainException>(() =>
            _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Cancelled, Customer(), default));
    }

    [Fact]
    public async Task Customer_cannot_touch_someone_elses_order_or_confirm_their_own()
    {
        var orderId = await PlaceAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Cancelled, Customer(OtherCustomerId), default));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _ctx.CreateService().ChangeStatusAsync(orderId, OrderStatus.Confirmed, Customer(), default));
    }

    [Fact]
    public async Task Expired_pending_orders_are_auto_cancelled_and_restocked()
    {
        var oldOrder = await PlaceAsync(quantity: 3, key: "old");
        _ctx.Clock.Advance(TimeSpan.FromMinutes(20));
        var confirmedOrder = await PlaceAsync(quantity: 2, key: "confirmed");
        await _ctx.CreateService().ChangeStatusAsync(confirmedOrder, OrderStatus.Confirmed, Admin(), default);
        _ctx.Clock.Advance(TimeSpan.FromMinutes(15));
        var freshOrder = await PlaceAsync(quantity: 1, key: "fresh");

        // old: 35 min Pending -> cancel. confirmed: not Pending -> keep. fresh: 0 min -> keep.
        var cancelled = await _ctx.CreateService().CancelExpiredPendingOrdersAsync(default);

        Assert.Equal(1, cancelled);
        var service = _ctx.CreateService();
        Assert.Equal("Cancelled", (await service.GetOrderAsync(oldOrder, Admin(), default)).Status);
        Assert.Equal("Confirmed", (await service.GetOrderAsync(confirmedOrder, Admin(), default)).Status);
        Assert.Equal("Pending", (await service.GetOrderAsync(freshOrder, Admin(), default)).Status);
        Assert.Equal(20 - 2 - 1, _ctx.StockOf(MouseId));
    }

    public void Dispose() => _ctx.Dispose();
}
