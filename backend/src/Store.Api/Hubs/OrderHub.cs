using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Store.Infrastructure.Security;

namespace Store.Api.Hubs;

/// <summary>
/// Live order updates. Each user joins their own group; admins also join "admins".
/// </summary>
[Authorize]
public sealed class OrderHub : Hub
{
    public const string Path = "/hubs/orders";
    public const string AdminsGroup = "admins";

    public static string UserGroup(int userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(StoreClaims.UserId)?.Value;
        if (int.TryParse(userId, out var id))
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(id));

        if (Context.User?.FindFirst(StoreClaims.Role)?.Value == "Admin")
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);

        await base.OnConnectedAsync();
    }
}
