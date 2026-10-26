using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common;
using Pos.Application.Orders;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.CreateOrder;
using Pos.Application.Orders.GetOrder;
using Pos.Application.Orders.ListOrders;
using Pos.Domain.Orders;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<OrderDto>> Create(
        [FromServices] ICommandHandler<CreateOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken)
    {
        var order = await handler.HandleAsync(new CreateOrderCommand(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpGet]
    [ProducesResponseType<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<OrderSummaryDto>> List(
        [FromServices] IQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>> handler,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        handler.HandleAsync(new ListOrdersQuery(search, status, page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OrderDto> Get(
        Guid id,
        [FromServices] IQueryHandler<GetOrderQuery, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetOrderQuery(id), cancellationToken);

    [HttpPost("{id:guid}/items")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> AddItem(
        Guid id,
        AddOrderItemRequest request,
        [FromServices] ICommandHandler<AddOrderItemCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(
            new AddOrderItemCommand(id, request.MenuItemId, request.Quantity, request.Notes),
            cancellationToken);
}

public sealed record AddOrderItemRequest(Guid MenuItemId, int Quantity, string? Notes);
