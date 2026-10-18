using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Store.Api.Infrastructure;
using Store.Application.Abstractions;
using Store.Application.Common;
using Store.Application.Orders;

namespace Store.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    public const string IdempotencyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    private readonly IOrderService _orders;
    private readonly ICurrentUser _currentUser;

    public OrdersController(IOrderService orders, ICurrentUser currentUser)
    {
        _orders = orders;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Places an order. Sending the same Idempotency-Key again returns the original order (200)
    /// instead of creating a new one.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = SecuritySetup.CustomerRole)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDto>> PlaceOrder(
        CreateOrderRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _orders.PlaceOrderAsync(_currentUser.UserId, request, idempotencyKey, cancellationToken);

        if (!result.Created)
        {
            Response.Headers[ReplayedHeader] = "true";
            return Ok(result.Order);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Order.Id }, result.Order);
    }

    /// <summary>The signed-in customer's orders, newest first.</summary>
    [HttpGet("my")]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetMyOrders(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default) =>
        Ok(await _orders.GetMyOrdersAsync(_currentUser.UserId, page, pageSize, cancellationToken));

    /// <summary>All orders (Admin).</summary>
    [HttpGet]
    [Authorize(Roles = SecuritySetup.AdminRole)]
    [ProducesResponseType<PagedResult<OrderDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetOrders([FromQuery] OrderListQuery query, CancellationToken cancellationToken) =>
        Ok(await _orders.GetOrdersAsync(query, cancellationToken));

    /// <summary>Order details - owner or Admin only.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _orders.GetOrderAsync(id, _currentUser, cancellationToken));

    /// <summary>Admin: any valid transition. Customer: may only cancel their own Pending order.</summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> ChangeStatus(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await _orders.ChangeStatusAsync(id, request.Status!.Value, _currentUser, cancellationToken));
}
