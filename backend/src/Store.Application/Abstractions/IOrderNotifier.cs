using Store.Application.Orders;

namespace Store.Application.Abstractions;

/// <summary>Pushes order events to connected clients (SignalR in the API).</summary>
public interface IOrderNotifier
{
    Task OrderStatusChangedAsync(OrderStatusChangedEvent change, CancellationToken cancellationToken);
    Task OrderPlacedAsync(OrderPlacedEvent placed, CancellationToken cancellationToken);
}

public sealed class NullOrderNotifier : IOrderNotifier
{
    public Task OrderStatusChangedAsync(OrderStatusChangedEvent change, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task OrderPlacedAsync(OrderPlacedEvent placed, CancellationToken cancellationToken) => Task.CompletedTask;
}
