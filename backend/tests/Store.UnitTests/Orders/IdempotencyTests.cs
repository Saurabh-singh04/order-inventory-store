using Store.Application.Common;
using Store.UnitTests.TestHelpers;
using static Store.UnitTests.TestHelpers.OrderTestContext;

namespace Store.UnitTests.Orders;

// Rule 3: the same Idempotency-Key returns the original order and never reserves stock twice.
public class IdempotencyTests : IDisposable
{
    private readonly OrderTestContext _ctx = new();

    [Fact]
    public async Task Repeating_a_request_with_the_same_key_returns_the_original_order()
    {
        var first = await _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 2)), "same-key", default);
        var second = await _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 2)), "same-key", default);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Order.Id, second.Order.Id);
        Assert.Equal(first.Order.Total, second.Order.Total);
        Assert.Equal(18, _ctx.StockOf(MouseId)); // reduced once, not twice
        Assert.Equal(1, _ctx.OrderCount());
    }

    [Fact]
    public async Task Same_key_with_a_different_body_is_rejected()
    {
        await _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 1)), "same-key", default);

        await Assert.ThrowsAsync<IdempotencyKeyReuseException>(() =>
            _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 5)), "same-key", default));

        Assert.Equal(19, _ctx.StockOf(MouseId));
        Assert.Equal(1, _ctx.OrderCount());
    }

    [Fact]
    public async Task Keys_are_scoped_per_customer()
    {
        var mine = await _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 1)), "shared-key", default);
        var theirs = await _ctx.CreateService().PlaceOrderAsync(OtherCustomerId, Request((MouseId, 1)), "shared-key", default);

        Assert.True(theirs.Created);
        Assert.NotEqual(mine.Order.Id, theirs.Order.Id);
        Assert.Equal(2, _ctx.OrderCount());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_key_is_a_validation_error(string? key)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _ctx.CreateService().PlaceOrderAsync(CustomerId, Request((MouseId, 1)), key, default));

        Assert.Equal(0, _ctx.OrderCount());
    }

    public void Dispose() => _ctx.Dispose();
}
