using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Store.Application.Abstractions;
using Store.Domain.Entities;
using Store.Infrastructure.Persistence;

namespace Store.IntegrationTests;

/// <summary>
/// Boots the real API against a throw-away SQL Server database.
/// rowversion and the filtered unique index only exist on SQL Server, which is why
/// these tests don't use the in-memory provider.
///
/// Server comes from STORE_TEST_SQL (any connection string; the database name is replaced),
/// default: local SQL Server with Windows authentication.
/// </summary>
public sealed class StoreApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString;

    public StoreApiFactory()
    {
        var baseConnection = Environment.GetEnvironmentVariable("STORE_TEST_SQL")
            ?? "Server=localhost;Trusted_Connection=True;TrustServerCertificate=True";

        _connectionString = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"StoreDb_Tests_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _connectionString);
        builder.UseSetting("Jwt:Key", "integration-tests-only-signing-key-0123456789");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:AdminPassword", "Admin@123");
        builder.UseSetting("Seed:CustomerPassword", "Customer@123");
        builder.UseSetting("BackgroundJobs:Enabled", "false");
    }

    public Task InitializeAsync()
    {
        // Touching Services starts the app, which runs the migrations + seed.
        _ = Services;
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }

    /// <summary>HttpClient authenticated as a seeded user (token issued directly, so the login rate limit isn't used up).</summary>
    public async Task<HttpClient> CreateClientForAsync(string email)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = await db.Users.SingleAsync(u => u.Email == email);
        var (token, _) = tokens.CreateAccessToken(user);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public async Task<int> CreateProductAsync(int stock, decimal price = 999m)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var product = new Product
        {
            Sku = $"TST-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            Name = "Integration test product",
            Price = price,
            StockQuantity = stock,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    public async Task<(int Stock, int OrderCount)> GetProductStateAsync(int productId)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var stock = await db.Products.Where(p => p.Id == productId).Select(p => p.StockQuantity).SingleAsync();
        var orders = await db.OrderItems.CountAsync(i => i.ProductId == productId);
        return (stock, orders);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<StoreApiFactory>
{
    public const string Name = "api";
}
