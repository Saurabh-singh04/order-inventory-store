using System.Security.Claims;
using Store.Application.Abstractions;
using Store.Application.Common;
using Store.Domain.Enums;
using Store.Infrastructure.Security;

namespace Store.Api.Infrastructure;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal Principal =>
        _accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user
            : throw new UnauthorizedException("Authentication is required.");

    public int UserId =>
        int.TryParse(Principal.FindFirstValue(StoreClaims.UserId), out var id)
            ? id
            : throw new UnauthorizedException("Access token has no valid user id.");

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal.FindFirstValue(StoreClaims.Role), out var role)
            ? role
            : UserRole.Customer;
}
