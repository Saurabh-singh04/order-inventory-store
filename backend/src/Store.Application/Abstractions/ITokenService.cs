using Store.Domain.Entities;

namespace Store.Application.Abstractions;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);

    /// <summary>Creates a cryptographically random refresh token (raw value, sent to the client).</summary>
    string CreateRefreshToken();

    /// <summary>Hash used to store / look up a refresh token.</summary>
    string HashToken(string token);

    TimeSpan RefreshTokenLifetime { get; }
}
