using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Store.Application.Abstractions;
using Store.Infrastructure.BackgroundJobs;
using Store.Infrastructure.Caching;
using Store.Infrastructure.Persistence;
using Store.Infrastructure.Persistence.Seed;
using Store.Infrastructure.Security;

namespace Store.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IDbExceptionClassifier, SqlServerExceptionClassifier>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();

        // Bounded, so random search strings can't grow the cache without limit.
        services.AddMemoryCache(options => options.SizeLimit = 2_000);
        services.AddSingleton<IProductCache, ProductCache>();

        // The API replaces this with the SignalR implementation.
        services.TryAddSingleton<IOrderNotifier, NullOrderNotifier>();

        if (configuration.GetValue("BackgroundJobs:Enabled", true))
        {
            services.AddHostedService<PendingOrderCleanupService>();
            services.AddHostedService<OutboxProcessor>();
        }

        return services;
    }
}
