using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Store.Application.Abstractions;
using Store.Application.Common;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed class AuthService : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "Refresh token is invalid or expired.";

    // Verified against when the email is unknown, so both paths cost one PBKDF2 run
    // and response time doesn't reveal which emails are registered.
    private static string? _dummyPasswordHash;

    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokens;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAppDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokens,
        IDbExceptionClassifier dbErrors,
        TimeProvider clock,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _dbErrors = dbErrors;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException("An account with this email already exists.", "email_taken");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Role = UserRole.Customer, // self registration never creates admins
            CreatedAt = UtcNow
        };
        user.PasswordHash = _passwordHasher.Hash(user, request.Password);

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (_dbErrors.IsUniqueConstraintViolation(ex))
        {
            // Two sign-ups with the same email at the same moment: the unique index decides.
            throw new ConflictException("An account with this email already exists.", "email_taken");
        }

        _logger.LogInformation("User {UserId} registered", user.Id);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            _dummyPasswordHash ??= _passwordHasher.Hash(new User(), "not-a-real-password");
            _passwordHasher.Verify(new User { PasswordHash = _dummyPasswordHash }, request.Password);

            _logger.LogWarning("Login failed: unknown email");
            throw new UnauthorizedException(InvalidCredentials);
        }

        var check = _passwordHasher.Verify(user, request.Password);
        if (check == PasswordCheck.Failed)
        {
            _logger.LogWarning("Login failed for user {UserId}: wrong password", user.Id);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (check == PasswordCheck.SuccessRehashNeeded)
            user.PasswordHash = _passwordHasher.Hash(user, request.Password);

        _logger.LogInformation("User {UserId} logged in", user.Id);
        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (stored?.User is null)
            throw new UnauthorizedException(InvalidRefreshToken);

        if (stored.RevokedAt is not null)
        {
            // A rotated token was used again - most likely stolen. Kill the whole session family.
            _logger.LogWarning("Revoked refresh token reused for user {UserId}; revoking all sessions", stored.UserId);
            await RevokeAllForUserAsync(stored.UserId, cancellationToken);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (!stored.IsActive(UtcNow))
            throw new UnauthorizedException(InvalidRefreshToken);

        try
        {
            return await IssueTokensAsync(stored.User, cancellationToken, replacing: stored);
        }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || _dbErrors.IsDeadlock(ex))
        {
            // RevokedAt is a concurrency token: a parallel refresh with the same token already
            // rotated it, so this one must not mint a second valid refresh token. Under load SQL Server
            // may also resolve the same race as a deadlock - the outcome for this caller is the same.
            _logger.LogWarning("Concurrent refresh with the same token for user {UserId}", stored.UserId);
            throw new UnauthorizedException(InvalidRefreshToken);
        }
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = _tokens.HashToken(refreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null || stored.RevokedAt is not null)
            return;

        stored.RevokedAt = UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken, RefreshToken? replacing = null)
    {
        var now = UtcNow;
        var (accessToken, accessExpires) = _tokens.CreateAccessToken(user);
        var rawRefresh = _tokens.CreateRefreshToken();

        var refresh = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashToken(rawRefresh),
            CreatedAt = now,
            ExpiresAt = now.Add(_tokens.RefreshTokenLifetime)
        };
        _db.RefreshTokens.Add(refresh);

        if (replacing is not null)
        {
            replacing.RevokedAt = now;
            replacing.ReplacedByTokenHash = refresh.TokenHash;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResponse(
            accessToken,
            accessExpires,
            rawRefresh,
            refresh.ExpiresAt,
            new UserDto(user.Id, user.FullName, user.Email, user.Role.ToString()));
    }

    private async Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken)
    {
        var now = UtcNow;
        var active = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
            token.RevokedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
