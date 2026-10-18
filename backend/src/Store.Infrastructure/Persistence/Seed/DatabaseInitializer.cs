using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Store.Application.Abstractions;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Infrastructure.Persistence.Seed;

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations and seeds the user accounts.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        logger.LogInformation("Applying database migrations");
        await db.Database.MigrateAsync(cancellationToken);

        var options = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;
        if (!options.Enabled)
            return;

        await SeedUsersAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), options, logger, cancellationToken);
    }

    private static async Task SeedUsersAsync(
        AppDbContext db, IPasswordHasher hasher, SeedOptions options, ILogger logger, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
            return;

        if (string.IsNullOrWhiteSpace(options.AdminPassword) || string.IsNullOrWhiteSpace(options.CustomerPassword))
        {
            logger.LogWarning("Seed passwords are not configured - skipping user seeding");
            return;
        }

        var now = DateTime.UtcNow;
        var users = new[]
        {
            NewUser("Store Admin", "admin@store.local", UserRole.Admin),
            NewUser("Aarav Sharma", "aarav@store.local", UserRole.Customer),
            NewUser("Priya Patel", "priya@store.local", UserRole.Customer),
            NewUser("Rohan Mehta", "rohan@store.local", UserRole.Customer)
        };

        foreach (var user in users)
        {
            var password = user.Role == UserRole.Admin ? options.AdminPassword : options.CustomerPassword;
            user.PasswordHash = hasher.Hash(user, password);
        }

        db.Users.AddRange(users);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} users", users.Length);

        User NewUser(string name, string email, UserRole role) =>
            new() { FullName = name, Email = email, Role = role, CreatedAt = now };
    }
}
