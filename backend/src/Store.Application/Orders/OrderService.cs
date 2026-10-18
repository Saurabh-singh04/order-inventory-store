using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Store.Application.Abstractions;
using Store.Application.Common;
using Store.Domain.Entities;
using Store.Domain.Enums;
using Store.Domain.Exceptions;
using Store.Domain.Services;

namespace Store.Application.Orders;

public interface IOrderService
{
    Task<PlaceOrderResult> PlaceOrderAsync(int customerId, CreateOrderRequest request, string? idempotencyKey, CancellationToken cancellationToken);
    Task<PagedResult<OrderDto>> GetMyOrdersAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<OrderDto>> GetOrdersAsync(OrderListQuery query, CancellationToken cancellationToken);
    Task<OrderDto> GetOrderAsync(int orderId, ICurrentUser currentUser, CancellationToken cancellationToken);
    Task<OrderDto> ChangeStatusAsync(int orderId, OrderStatus newStatus, ICurrentUser currentUser, CancellationToken cancellationToken);
    Task<int> CancelExpiredPendingOrdersAsync(CancellationToken cancellationToken);
}

public sealed class OrderService : IOrderService
{
    public const string OrderPlacedMessageType = "OrderPlaced";
    private const int MaxStatusChangeAttempts = 3;
    private const int ExpiredBatchSize = 100;

    private readonly IAppDbContext _db;
    private readonly IDbExceptionClassifier _dbErrors;
    private readonly IProductCache _productCache;
    private readonly IOrderNotifier _notifier;
    private readonly TimeProvider _clock;
    private readonly OrderOptions _options;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IAppDbContext db,
        IDbExceptionClassifier dbErrors,
        IProductCache productCache,
        IOrderNotifier notifier,
        TimeProvider clock,
        IOptions<OrderOptions> options,
        ILogger<OrderService> logger)
    {
        _db = db;
        _dbErrors = dbErrors;
        _productCache = productCache;
        _notifier = notifier;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    // ---------------------------------------------------------------- place order

    public async Task<PlaceOrderResult> PlaceOrderAsync(
        int customerId, CreateOrderRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var key = ValidateIdempotencyKey(idempotencyKey);
        var lines = NormalizeLines(request);
        var requestHash = ComputeRequestHash(lines);

        // Fast path: the same request was already processed.
        var existing = await FindByIdempotencyKeyAsync(customerId, key, cancellationToken);
        if (existing is not null)
            return await ReplayAsync(existing, requestHash, cancellationToken);

        try
        {
            var order = await CreateOrderInTransactionAsync(customerId, key, requestHash, lines, cancellationToken);

            _productCache.Invalidate();
            _logger.LogInformation(
                "Order {OrderId} placed by customer {CustomerId}: SubTotal={SubTotal} Discount={Discount} Total={Total}",
                order.Id, customerId, order.SubTotal, order.Discount, order.Total);

            var dto = await GetDtoAsync(order.Id, cancellationToken);
            return new PlaceOrderResult(dto, Created: true);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another order changed one of the products (RowVersion mismatch) between our read and our write.
            _db.ChangeTracker.Clear();

            var winner = await FindByIdempotencyKeyAsync(customerId, key, cancellationToken);
            if (winner is not null)
                return await ReplayAsync(winner, requestHash, cancellationToken);

            _logger.LogWarning("Order for customer {CustomerId} lost a stock race (optimistic concurrency)", customerId);
            throw new ConflictException(
                "Stock for one or more products changed while your order was being placed and is no longer available.",
                "out_of_stock");
        }
        catch (DbUpdateException ex) when (_dbErrors.IsUniqueConstraintViolation(ex))
        {
            // Two requests with the same Idempotency-Key raced; the unique index let only one in.
            _db.ChangeTracker.Clear();

            var winner = await FindByIdempotencyKeyAsync(customerId, key, cancellationToken);
            if (winner is not null)
                return await ReplayAsync(winner, requestHash, cancellationToken);

            throw;
        }
        catch (Exception ex) when (_dbErrors.IsDeadlock(ex))
        {
            _db.ChangeTracker.Clear();
            _logger.LogWarning(ex, "Deadlock while placing order for customer {CustomerId}", customerId);
            throw new ConflictException("The order could not be placed because of a concurrent update. Please try again.", "concurrency");
        }
    }

    private async Task<Order> CreateOrderInTransactionAsync(
        int customerId, string key, string requestHash, IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken)
    {
        // Disposing without commit rolls everything back, so a failure anywhere leaves stock untouched.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var productIds = lines.Select(l => l.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var unavailable = productIds
            .Where(id => !products.TryGetValue(id, out var p) || !p.IsActive)
            .ToList();
        if (unavailable.Count > 0)
            throw new ValidationException($"These products do not exist or are not available: {string.Join(", ", unavailable)}.");

        // Check every line first - one short item fails the whole order.
        var shortages = lines
            .Where(l => !products[l.ProductId].HasStock(l.Quantity))
            .Select(l =>
            {
                var p = products[l.ProductId];
                return $"{p.Name} (requested {l.Quantity}, available {p.StockQuantity})";
            })
            .ToList();
        if (shortages.Count > 0)
            throw new ConflictException($"Not enough stock for: {string.Join("; ", shortages)}.", "out_of_stock");

        var previousOrders = await _db.Orders
            .CountAsync(o => o.CustomerId == customerId && o.Status != OrderStatus.Cancelled, cancellationToken);

        var now = UtcNow;
        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.Pending,
            CreatedAt = now,
            IdempotencyKey = key,
            RequestHash = requestHash
        };

        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            product.ReserveStock(line.Quantity);
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                UnitPrice = product.Price
            });
        }

        var pricing = DiscountCalculator.Calculate(
            order.Items.Sum(i => i.UnitPrice * i.Quantity),
            customerOrderNumber: previousOrders + 1);
        order.SubTotal = pricing.SubTotal;
        order.Discount = pricing.Discount;
        order.Total = pricing.Total;

        _db.Orders.Add(order);

        // UPDATE Products ... WHERE Id = @id AND RowVersion = @original  -> 0 rows = concurrency exception
        await _db.SaveChangesAsync(cancellationToken);

        // Outbox row in the same transaction: the notification exists if and only if the order does.
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Type = OrderPlacedMessageType,
            Payload = JsonSerializer.Serialize(new OrderPlacedEvent(order.Id, customerId, order.Total, now)),
            OccurredAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return order;
    }

    private string ValidateIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ValidationException("The Idempotency-Key header is required when placing an order.");

        var key = idempotencyKey.Trim();
        if (key.Length > _options.MaxIdempotencyKeyLength)
            throw new ValidationException($"Idempotency-Key must be at most {_options.MaxIdempotencyKeyLength} characters.");

        return key;
    }

    internal static IReadOnlyList<OrderLine> NormalizeLines(CreateOrderRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new ValidationException("An order needs at least one item.");

        if (request.Items.Any(i => i.Quantity <= 0))
            throw new ValidationException("Quantity must be greater than zero for every item.");

        // Same product twice in the request -> one line with the summed quantity.
        return request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new OrderLine(g.Key, g.Sum(i => i.Quantity)))
            .OrderBy(l => l.ProductId)
            .ToList();
    }

    internal static string ComputeRequestHash(IReadOnlyList<OrderLine> lines)
    {
        var canonical = string.Join('|', lines.Select(l => $"{l.ProductId}:{l.Quantity}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private async Task<Order?> FindByIdempotencyKeyAsync(int customerId, string key, CancellationToken cancellationToken) =>
        await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.CustomerId == customerId && o.IdempotencyKey == key, cancellationToken);

    private async Task<PlaceOrderResult> ReplayAsync(Order existing, string requestHash, CancellationToken cancellationToken)
    {
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
            throw new IdempotencyKeyReuseException();

        _logger.LogInformation("Idempotent replay of order {OrderId}", existing.Id);
        var dto = await GetDtoAsync(existing.Id, cancellationToken);
        return new PlaceOrderResult(dto, Created: false);
    }

    internal sealed record OrderLine(int ProductId, int Quantity);

    // ---------------------------------------------------------------- queries

    public Task<PagedResult<OrderDto>> GetMyOrdersAsync(int customerId, int page, int pageSize, CancellationToken cancellationToken) =>
        _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Select(ToDtoExpression)
            .ToPagedResultAsync(page, pageSize, cancellationToken);

    public Task<PagedResult<OrderDto>> GetOrdersAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        var orders = _db.Orders.AsNoTracking();
        if (query.Status is not null)
            orders = orders.Where(o => o.Status == query.Status);

        return orders
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Select(ToDtoExpression)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);
    }

    public async Task<OrderDto> GetOrderAsync(int orderId, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var dto = await GetDtoAsync(orderId, cancellationToken);

        if (!currentUser.IsAdmin && dto.CustomerId != currentUser.UserId)
            throw new ForbiddenException("You can only view your own orders.");

        return dto;
    }

    private async Task<OrderDto> GetDtoAsync(int orderId, CancellationToken cancellationToken) =>
        await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("Order", orderId);

    private static readonly Expression<Func<Order, OrderDto>> ToDtoExpression = o => new OrderDto(
        o.Id,
        o.CustomerId,
        o.Customer!.FullName,
        o.Status.ToString(),
        o.SubTotal,
        o.Discount,
        o.Total,
        o.CreatedAt,
        o.UpdatedAt,
        o.Items
            .OrderBy(i => i.Id)
            .Select(i => new OrderItemDto(i.ProductId, i.Product!.Sku, i.Product.Name, i.Quantity, i.UnitPrice, i.UnitPrice * i.Quantity))
            .ToList());

    // ---------------------------------------------------------------- status changes

    public async Task<OrderDto> ChangeStatusAsync(
        int orderId, OrderStatus newStatus, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        await ApplyStatusChangeAsync(orderId, newStatus, order =>
        {
            if (!currentUser.IsAdmin)
            {
                if (order.CustomerId != currentUser.UserId)
                    throw new ForbiddenException("You can only change your own orders.");
                if (newStatus != OrderStatus.Cancelled)
                    throw new ForbiddenException("Customers can only cancel orders.");
            }

            // Generic state machine check first so Shipped/Cancelled get the normal message.
            if (!Order.CanTransition(order.Status, newStatus))
                throw new InvalidStatusTransitionException(order.Status, newStatus);

            if (!currentUser.IsAdmin && order.Status != OrderStatus.Pending)
                throw new DomainException($"Only Pending orders can be cancelled by the customer. This order is {order.Status}.");

            return true;
        }, actor: currentUser.IsAdmin ? "admin" : "customer", cancellationToken);

        return await GetDtoAsync(orderId, cancellationToken);
    }

    public async Task<int> CancelExpiredPendingOrdersAsync(CancellationToken cancellationToken)
    {
        var cutoff = UtcNow.AddMinutes(-_options.PendingTimeoutMinutes);

        var expiredIds = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .Take(ExpiredBatchSize)
            .ToListAsync(cancellationToken);

        var cancelled = 0;
        foreach (var id in expiredIds)
        {
            try
            {
                // Re-checked after reload: if an admin confirmed it meanwhile, leave it alone.
                var changed = await ApplyStatusChangeAsync(
                    id,
                    OrderStatus.Cancelled,
                    order => order.Status == OrderStatus.Pending && order.CreatedAt < cutoff,
                    actor: "system",
                    cancellationToken);

                if (changed)
                    cancelled++;
            }
            catch (ConflictException ex)
            {
                _logger.LogWarning(ex, "Could not auto-cancel order {OrderId}, will retry on next run", id);
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }

        return cancelled;
    }

    /// <summary>
    /// Loads the order, lets <paramref name="authorize"/> veto (false = skip, exception = reject),
    /// applies the transition and restores stock on cancel - all in one SaveChanges (one transaction).
    /// Retries when the order or a product row was changed concurrently.
    /// </summary>
    private async Task<bool> ApplyStatusChangeAsync(
        int orderId, OrderStatus newStatus, Func<Order, bool> authorize, string actor, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
                ?? throw new NotFoundException("Order", orderId);

            if (!authorize(order))
                return false;

            var oldStatus = order.Status;
            var now = UtcNow;
            order.ChangeStatus(newStatus, now);

            if (newStatus == OrderStatus.Cancelled)
                await RestoreStockAsync(order, cancellationToken);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxStatusChangeAttempts)
            {
                _logger.LogInformation("Concurrent update on order {OrderId}, retrying (attempt {Attempt})", orderId, attempt);
                _db.ChangeTracker.Clear();
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException("The order was updated by someone else at the same time. Please reload and try again.", "concurrency");
            }

            _logger.LogInformation(
                "Order {OrderId} status changed {OldStatus} -> {NewStatus} by {Actor}",
                orderId, oldStatus, newStatus, actor);

            if (newStatus == OrderStatus.Cancelled)
                _productCache.Invalidate();

            await NotifyAsync(new OrderStatusChangedEvent(order.Id, order.CustomerId, oldStatus.ToString(), newStatus.ToString(), now));
            return true;
        }
    }

    private async Task RestoreStockAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in order.Items)
        {
            if (products.TryGetValue(item.ProductId, out var product))
                product.ReleaseStock(item.Quantity);
        }
    }

    private async Task NotifyAsync(OrderStatusChangedEvent change)
    {
        try
        {
            await _notifier.OrderStatusChangedAsync(change, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A failed push must never undo or fail a committed status change.
            _logger.LogWarning(ex, "Failed to push status change for order {OrderId}", change.OrderId);
        }
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;
}
