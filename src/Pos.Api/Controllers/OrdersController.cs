using Microsoft.AspNetCore.Mvc;
using Pos.Application.Orders;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController(OrderService orders) : ControllerBase
{
    /// <summary>Creates a new, empty open order.</summary>
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderDto>> Create(CancellationToken cancellationToken)
    {
        var order = await orders.CreateAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    /// <summary>
    /// Lists orders, newest first. Optional filters: <c>status</c> (Open, Paid, Cancelled; case-insensitive),
    /// <c>search</c> (case-insensitive match on order id or item name) and <c>limit</c> (1-200, default 50).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<OrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int? limit,
        CancellationToken cancellationToken) =>
        Ok(await orders.ListAsync(new ListOrdersRequest(status, search, limit), cancellationToken));

    /// <summary>Gets an order with its items and total.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await orders.GetAsync(id, cancellationToken);

    /// <summary>Adds a menu item to an order (merges with an existing line for the same menu item).</summary>
    [HttpPost("{id:guid}/items")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderDto>> AddItem(Guid id, AddOrderItemRequest request, CancellationToken cancellationToken) =>
        await orders.AddItemAsync(id, request, cancellationToken);

    /// <summary>Calculates the order total.</summary>
    [HttpGet("{id:guid}/total")]
    [ProducesResponseType<OrderTotalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderTotalDto>> GetTotal(Guid id, CancellationToken cancellationToken) =>
        await orders.GetTotalAsync(id, cancellationToken);
}
