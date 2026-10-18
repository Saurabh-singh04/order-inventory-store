using Store.Domain.Enums;
using Store.Domain.Exceptions;

namespace Store.Domain.Entities;

public class Order
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        [OrderStatus.Shipped] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    public int Id { get; set; }
    public int CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Idempotency support - unique per customer.
    public string? IdempotencyKey { get; set; }
    public string? RequestHash { get; set; }

    // Concurrency token: stops two parallel cancellations from restoring stock twice.
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public User? Customer { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public static bool CanTransition(OrderStatus from, OrderStatus to) =>
        AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public bool CanBeCancelled => CanTransition(Status, OrderStatus.Cancelled);

    /// <summary>
    /// Moves the order to a new status. Stock is not touched here - the caller
    /// releases stock when the order is cancelled.
    /// </summary>
    public void ChangeStatus(OrderStatus newStatus, DateTime utcNow)
    {
        if (!CanTransition(Status, newStatus))
            throw new InvalidStatusTransitionException(Status, newStatus);

        Status = newStatus;
        UpdatedAt = utcNow;
    }
}
