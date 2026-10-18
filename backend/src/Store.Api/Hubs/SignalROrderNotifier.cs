using Microsoft.AspNetCore.SignalR;
using Store.Application.Abstractions;
using Store.Application.Orders;

namespace Store.Api.Hubs;

public sealed class SignalROrderNotifier : IOrderNotifier
{
    private readonly IHubContext<OrderHub> _hub;

    public SignalROrderNotifier(IHubContext<OrderHub> hub)
    {
        _hub = hub;
    }

    public Task OrderStatusChangedAsync(OrderStatusChangedEvent change, CancellationToken cancellationToken) =>
        _hub.Clients
            .Groups(OrderHub.UserGroup(change.CustomerId), OrderHub.AdminsGroup)
            .SendAsync("orderStatusChanged", change, cancellationToken);

    public Task OrderPlacedAsync(OrderPlacedEvent placed, CancellationToken cancellationToken) =>
        _hub.Clients
            .Group(OrderHub.AdminsGroup)
            .SendAsync("orderPlaced", placed, cancellationToken);
}
