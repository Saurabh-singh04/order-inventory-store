namespace Store.Infrastructure.Security;

/// <summary>Claim names used in our access tokens (short JWT names, no inbound mapping).</summary>
public static class StoreClaims
{
    public const string UserId = "sub";
    public const string Name = "name";
    public const string Email = "email";
    public const string Role = "role";
}
