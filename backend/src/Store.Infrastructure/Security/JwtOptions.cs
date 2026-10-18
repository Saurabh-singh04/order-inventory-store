namespace Store.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Store.Api";
    public string Audience { get; set; } = "Store.Client";

    /// <summary>Symmetric signing key, at least 32 bytes. Supplied via user-secrets / environment.</summary>
    public string Key { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
