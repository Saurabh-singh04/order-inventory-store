using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Store.Application.Abstractions;
using Store.Application.Orders;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Infrastructure.Caching;
using Store.Infrastructure.Persistence;

namespace Store.UnitTests.TestHelpers;

/// <summary>
/// Sets up an in-memory database with two customers and a few products,
/// and builds OrderService instances on top of it.
/// </summary>
public sealed class OrderTestContext : IDisposable
{
    public const int CustomerId = 1;
    public const int OtherCustomerId = 2;
    public const int AdminId = 3;

    public const int LaptopId = 1;   // 50,000 x 10
    public const int MouseId = 2;    // 500 x 20
    public const int CableId = 3;    // 100 x 1

    private readonly string _databaseName = $"orders-{Guid.NewGuid()}";
    private readonly List<AppDbContext> _contexts = new();

    public OrderTestContext()
    {
        Clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));

        using var db = NewDbContext();
        db.Users.AddRange(
            new User { Id = CustomerId, FullName = "Test Customer", Email = "c1@test.local", Role = UserRole.Customer },
            new User { Id = OtherCustomerId, FullName = "Other Customer", Email = "c2@test.local", Role = UserRole.Customer },
            new User { Id = AdminId, FullName = "Admin", Email = "admin@test.local", Role = UserRole.Admin });

        db.Products.AddRange(
            new Product { Id = LaptopId, Sku = "LAP-1", Name = "Laptop", Price = 50_000m, StockQuantity = 10, IsActive = true },
            new Product { Id = MouseId, Sku = "MOU-1", Name = "Mouse", Price = 500m, StockQuantity = 20, IsActive = true },
            new Product { Id = CableId, Sku = "CAB-1", Name = "Cable", Price = 100m, StockQuantity = 1, IsActive = true });

        db.SaveChanges();
    }

    public FakeTimeProvider Clock { get; }

    public AppDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(_databaseName)
            // The in-memory provider has no transactions; the service still calls BeginTransaction.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var db = new AppDbContext(options);
        _contexts.Add(db);
        return db;
    }

    public OrderService CreateService(AppDbContext? db = null) =>
        new(
            db ?? NewDbContext(),
            new NoDbErrors(),
            new ProductCache(new MemoryCache(new MemoryCacheOptions()), NullLogger<ProductCache>.Instance),
            new NullOrderNotifier(),
            Clock,
            Options.Create(new OrderOptions { PendingTimeoutMinutes = 30 }),
            NullLogger<OrderService>.Instance);

    public int StockOf(int productId)
    {
        using var db = NewDbContext();
        return db.Products.AsNoTracking().Single(p => p.Id == productId).StockQuantity;
    }

    public int OrderCount()
    {
        using var db = NewDbContext();
        return db.Orders.Count();
    }

    public static CreateOrderRequest Request(params (int ProductId, int Quantity)[] items) =>
        new() { Items = items.Select(i => new OrderItemRequest { ProductId = i.ProductId, Quantity = i.Quantity }).ToList() };

    public static ICurrentUser Customer(int id = CustomerId) => new TestUser(id, UserRole.Customer);
    public static ICurrentUser Admin() => new TestUser(AdminId, UserRole.Admin);

    public void Dispose()
    {
        foreach (var db in _contexts)
            db.Dispose();
    }

    private sealed record TestUser(int UserId, UserRole Role) : ICurrentUser;

    private sealed class NoDbErrors : IDbExceptionClassifier
    {
        public bool IsUniqueConstraintViolation(DbUpdateException exception) => false;
        public bool IsDeadlock(Exception exception) => false;
    }
}
