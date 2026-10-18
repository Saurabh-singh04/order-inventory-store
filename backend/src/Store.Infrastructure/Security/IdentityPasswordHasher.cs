using Microsoft.AspNetCore.Identity;
using Store.Application.Abstractions;
using Store.Domain.Entities;

namespace Store.Infrastructure.Security;

/// <summary>Wraps ASP.NET Core Identity's PBKDF2 hasher (salted, versioned hashes).</summary>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(User user, string password) => _inner.HashPassword(user, password);

    public PasswordCheck Verify(User user, string password) =>
        _inner.VerifyHashedPassword(user, user.PasswordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheck.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
            _ => PasswordCheck.Failed
        };
}
