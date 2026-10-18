using System.ComponentModel.DataAnnotations;
using Store.Domain.Enums;

namespace Store.Application.Orders;

public sealed class CreateOrderRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "An order needs at least one item.")]
    [MaxLength(50, ErrorMessage = "An order can have at most 50 different products.")]
    public List<OrderItemRequest> Items { get; set; } = new();
}

public sealed class OrderItemRequest
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 1000)]
    public int Quantity { get; set; }
}

public sealed class UpdateOrderStatusRequest
{
    // EnumDataType rejects numbers that aren't a defined status, e.g. {"status": 99}.
    [Required, EnumDataType(typeof(OrderStatus))]
    public OrderStatus? Status { get; set; }
}

public sealed class OrderListQuery
{
    [EnumDataType(typeof(OrderStatus))]
    public OrderStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed record OrderItemDto(
    int ProductId,
    string Sku,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDto(
    int Id,
    int CustomerId,
    string CustomerName,
    string Status,
    decimal SubTotal,
    decimal Discount,
    decimal Total,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    IReadOnlyList<OrderItemDto> Items);

public sealed record PlaceOrderResult(OrderDto Order, bool Created);

public sealed record OrderStatusChangedEvent(int OrderId, int CustomerId, string OldStatus, string NewStatus, DateTime ChangedAt);

public sealed record OrderPlacedEvent(int OrderId, int CustomerId, decimal Total, DateTime CreatedAt);
