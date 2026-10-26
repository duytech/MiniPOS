using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common;
using Pos.Application.Orders;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ApplyDiscount;
using Pos.Application.Orders.CreateOrder;
using Pos.Application.Orders.GetOrder;
using Pos.Application.Orders.ListOrders;
using Pos.Application.Orders.RemoveDiscount;
using Pos.Application.Orders.RemoveTip;
using Pos.Application.Orders.SetPaymentMethod;
using Pos.Application.Orders.SetTip;
using Pos.Domain.Common;
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

    [HttpPut("{id:guid}/discount")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> ApplyDiscount(
        Guid id,
        ApplyDiscountRequest request,
        [FromServices] ICommandHandler<ApplyDiscountCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new ApplyDiscountCommand(id, request.Rate, request.Amount), cancellationToken);

    [HttpDelete("{id:guid}/discount")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> RemoveDiscount(
        Guid id,
        [FromServices] ICommandHandler<RemoveDiscountCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new RemoveDiscountCommand(id), cancellationToken);

    [HttpPut("{id:guid}/tip")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> SetTip(
        Guid id,
        SetTipRequest request,
        [FromServices] ICommandHandler<SetTipCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new SetTipCommand(id, request.Rate!.Value), cancellationToken);

    [HttpDelete("{id:guid}/tip")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> RemoveTip(
        Guid id,
        [FromServices] ICommandHandler<RemoveTipCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new RemoveTipCommand(id), cancellationToken);

    [HttpPut("{id:guid}/payment-method")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> SetPaymentMethod(
        Guid id,
        SetPaymentMethodRequest request,
        [FromServices] ICommandHandler<SetPaymentMethodCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new SetPaymentMethodCommand(id, request.PaymentMethod!.Value), cancellationToken);
}

public sealed record AddOrderItemRequest(Guid MenuItemId, int Quantity, string? Notes);

/// <summary>Exactly one of <paramref name="Rate"/> (percent of subtotal) or <paramref name="Amount"/> (fixed).</summary>
public sealed record ApplyDiscountRequest(Percentage? Rate, Money? Amount);

public sealed record SetTipRequest([Required] Percentage? Rate);

public sealed record SetPaymentMethodRequest([Required] PaymentMethod? PaymentMethod);
