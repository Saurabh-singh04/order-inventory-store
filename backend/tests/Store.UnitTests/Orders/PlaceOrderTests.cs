using Microsoft.EntityFrameworkCore;
using Store.Application.Common;
using Store.Domain.Enums;
using Store.UnitTests.TestHelpers;
using static Store.UnitTests.TestHelpers.OrderTestContext;

namespace Store.UnitTests.Orders;

// Rule 1: placing an order reserves stock for all items, or for none of them.
public class PlaceOrderTests : IDisposable
{
    private readonly OrderTestContext _ctx = new();

    [Fact]
    public async Task Reserves_stock_for_every_item_and_captures_unit_price()
    {
        var service = _ctx.CreateService();

        var result = await service.PlaceOrderAsync(CustomerId, Request((LaptopId, 2), (MouseId, 3)), "key-1", default);

        Assert.True(result.Created);
        Assert.Equal("Pending", result.Order.Status);
        Assert.Equal(8, _ctx.StockOf(LaptopId));
        Assert.Equal(17, _ctx.StockOf(MouseId));

        var laptopLine = result.Order.Items.Single(i => i.ProductId == LaptopId);
        Assert.Equal(50_000m, laptopLine.UnitPrice);
        Assert.Equal(101_500m, result.Order.SubTotal);
        Assert.Equal(5_075m, result.Order.Discount); // 5% - above 10,000
        Assert.Equal(96_425m, result.Order.Total);
    }

    [Fact]
    public async Task One_item_short_of_stock_fails_the_whole_order_and_changes_nothing()
    {
        var service = _ctx.CreateService();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.PlaceOrderAsync(CustomerId, Request((LaptopId, 1), (CableId, 2)), "key-1", default));

        Assert.Equal("out_of_stock", ex.Code);
        Assert.Contains("Cable", ex.Message);
        Assert.Equal(10, _ctx.StockOf(LaptopId));
        Assert.Equal(1, _ctx.StockOf(CableId));
        Assert.Equal(0, _ctx.OrderCount());
    }

    [Fact]
    public async Task Unknown_product_is_rejected_without_touching_stock()
    {
        var service = _ctx.CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.PlaceOrderAsync(CustomerId, Request((MouseId, 1), (999, 1)), "key-1", default));

        Assert.Equal(20, _ctx.StockOf(MouseId));
        Assert.Equal(0, _ctx.OrderCount());
    }

    [Fact]
    public async Task Duplicate_lines_for_the_same_product_are_merged_before_the_stock_check()
    {
        var service = _ctx.CreateService();

        // 1 + 1 cables requested, only 1 in stock -> must fail even though each line alone fits.
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.PlaceOrderAsync(CustomerId, Request((CableId, 1), (CableId, 1)), "key-1", default));

        Assert.Equal(1, _ctx.StockOf(CableId));
    }

    [Fact]
    public async Task Every_fifth_order_gets_the_loyalty_discount_and_cancelled_orders_do_not_count()
    {
        var service = _ctx.CreateService();

        for (var i = 1; i <= 4; i++)
            await service.PlaceOrderAsync(CustomerId, Request((MouseId, 1)), $"key-{i}", default);

        // Cancel one -> only 3 orders count, so the next one is the 4th (no loyalty discount).
        var my = await service.GetMyOrdersAsync(CustomerId, 1, 10, default);
        await _ctx.CreateService().ChangeStatusAsync(my.Items[0].Id, OrderStatus.Cancelled, Customer(), default);

        var fourth = await _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 1)), "key-5", default);
        var fifth = await _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 1)), "key-6", default);

        Assert.Equal(0m, fourth.Order.Discount);
        Assert.Equal(50m, fifth.Order.Discount); // 10% of 500
        Assert.Equal(450m, fifth.Order.Total);
    }

    public void Dispose() => _ctx.Dispose();
}
