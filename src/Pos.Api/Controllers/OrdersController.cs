using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pos.Application.Common;
using Pos.Application.Orders;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ApplyDiscount;
using Pos.Application.Orders.CancelOrder;
using Pos.Application.Orders.ChangeOrderLineQuantity;
using Pos.Application.Orders.CreateOrder;
using Pos.Application.Orders.GetOrder;
using Pos.Application.Orders.GetSplit;
using Pos.Application.Orders.ListOrders;
using Pos.Application.Orders.PayOrder;
using Pos.Application.Orders.PreviewSplit;
using Pos.Application.Orders.RemoveDiscount;
using Pos.Application.Orders.RemoveOrderLine;
using Pos.Application.Orders.RemoveSplit;
using Pos.Application.Orders.RemoveTip;
using Pos.Application.Orders.SaveSplit;
using Pos.Application.Orders.SetPaymentMethod;
using Pos.Application.Orders.SetTip;
using Pos.Application.Orders.SplitOrderEvenly;
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

    /// <summary>Splits the bill evenly between <paramref name="parts"/> people; the first part takes the leftover cents.</summary>
    [HttpGet("{id:guid}/split-even")]
    [ProducesResponseType<IReadOnlyList<SplitPartDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<SplitPartDto>> SplitEvenly(
        Guid id,
        [FromQuery, BindRequired] int parts,
        [FromServices] IQueryHandler<SplitOrderEvenlyQuery, IReadOnlyList<SplitPartDto>> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new SplitOrderEvenlyQuery(id, parts), cancellationToken);

    /// <summary>The saved split by item, priced with the current order; works in any status. No saved split → 404.</summary>
    [HttpGet("{id:guid}/split")]
    [ProducesResponseType<OrderSplitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OrderSplitDto> GetSplit(
        Guid id,
        [FromServices] IQueryHandler<GetSplitQuery, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetSplitQuery(id), cancellationToken);

    /// <summary>
    /// Saves the split by item (at least two parts, every line in exactly one part), replacing any saved one. Only
    /// the line assignment is stored; a real change to the order clears it.
    /// </summary>
    [HttpPut("{id:guid}/split")]
    [ProducesResponseType<OrderSplitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderSplitDto> SaveSplit(
        Guid id,
        SaveSplitRequest request,
        [FromServices] ICommandHandler<SaveSplitCommand, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new SaveSplitCommand(id, request.Parts?.Select(p => p?.LineIds).ToList()), cancellationToken);

    /// <summary>Prices a split by item like <see cref="SaveSplit"/> without saving anything.</summary>
    [HttpPost("{id:guid}/split/preview")]
    [ProducesResponseType<OrderSplitDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderSplitDto> PreviewSplit(
        Guid id,
        SaveSplitRequest request,
        [FromServices] IQueryHandler<PreviewSplitQuery, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new PreviewSplitQuery(id, request.Parts?.Select(p => p?.LineIds).ToList()), cancellationToken);

    /// <summary>Removes the saved split: missing order → 404, no saved split → 404, order not open → 422.</summary>
    [HttpDelete("{id:guid}/split")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RemoveSplit(
        Guid id,
        [FromServices] ICommandHandler<RemoveSplitCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new RemoveSplitCommand(id), cancellationToken);
        return NoContent();
    }

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

    /// <summary>Removes a line. A fixed discount larger than the new subtotal makes this a 422.</summary>
    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> RemoveLine(
        Guid id,
        Guid lineId,
        [FromServices] ICommandHandler<RemoveOrderLineCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new RemoveOrderLineCommand(id, lineId), cancellationToken);

    /// <summary>
    /// Sets a line's quantity (at least 1). A fixed discount larger than the new subtotal makes this a 422.
    /// </summary>
    [HttpPut("{id:guid}/lines/{lineId:guid}/quantity")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> ChangeLineQuantity(
        Guid id,
        Guid lineId,
        ChangeLineQuantityRequest request,
        [FromServices] ICommandHandler<ChangeOrderLineQuantityCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(
            new ChangeOrderLineQuantityCommand(id, lineId, request.Quantity!.Value),
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

    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> Pay(
        Guid id,
        [FromServices] ICommandHandler<PayOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new PayOrderCommand(id), cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrderDto> Cancel(
        Guid id,
        [FromServices] ICommandHandler<CancelOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new CancelOrderCommand(id), cancellationToken);
}

public sealed record AddOrderItemRequest(Guid MenuItemId, int Quantity, string? Notes);

public sealed record ChangeLineQuantityRequest([Required] int? Quantity);

/// <summary>Exactly one of <paramref name="Rate"/> (percent of subtotal) or <paramref name="Amount"/> (fixed).</summary>
public sealed record ApplyDiscountRequest(Percentage? Rate, Money? Amount);

public sealed record SetTipRequest([Required] Percentage? Rate);

public sealed record SetPaymentMethodRequest([Required] PaymentMethod? PaymentMethod);

/// <summary>A split by item: the line ids of each part, in part order. Used by PUT and preview.</summary>
public sealed record SaveSplitRequest(IReadOnlyList<SplitPartRequest?>? Parts);

public sealed record SplitPartRequest(IReadOnlyList<Guid>? LineIds);
