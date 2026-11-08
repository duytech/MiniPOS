using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pos.Api.Contracts;
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
    public async Task<ApiResponse<OrderDto>> Create(
        [FromServices] ICommandHandler<CreateOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new CreateOrderCommand(), cancellationToken)).ToResponse();

    [HttpGet]
    public async Task<ApiResponse<PagedResult<OrderSummaryDto>>> List(
        [FromServices] IQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>> handler,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        (await handler.HandleAsync(new ListOrdersQuery(search, status, page, pageSize), cancellationToken)).ToResponse();

    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<OrderDto>> Get(
        Guid id,
        [FromServices] IQueryHandler<GetOrderQuery, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new GetOrderQuery(id), cancellationToken)).ToResponse();

    /// <summary>Splits the bill evenly between <paramref name="parts"/> people; the first part takes the leftover cents.</summary>
    [HttpGet("{id:guid}/split-even")]
    public async Task<ApiResponse<IReadOnlyList<SplitPartDto>>> SplitEvenly(
        Guid id,
        [FromQuery, BindRequired] int parts,
        [FromServices] IQueryHandler<SplitOrderEvenlyQuery, IReadOnlyList<SplitPartDto>> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new SplitOrderEvenlyQuery(id, parts), cancellationToken)).ToResponse();

    /// <summary>The saved split by item, priced with the current order; works in any status.</summary>
    [HttpGet("{id:guid}/split")]
    public async Task<ApiResponse<OrderSplitDto>> GetSplit(
        Guid id,
        [FromServices] IQueryHandler<GetSplitQuery, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new GetSplitQuery(id), cancellationToken)).ToResponse();

    /// <summary>
    /// Saves the split by item (at least two parts, every line in exactly one part), replacing any saved one. Only
    /// the line assignment is stored; a real change to the order clears it.
    /// </summary>
    [HttpPut("{id:guid}/split")]
    public async Task<ApiResponse<OrderSplitDto>> SaveSplit(
        Guid id,
        SaveSplitRequest request,
        [FromServices] ICommandHandler<SaveSplitCommand, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new SaveSplitCommand(id, request.Parts?.Select(p => p?.LineIds).ToList()), cancellationToken)).ToResponse();

    /// <summary>Prices a split by item like <see cref="SaveSplit"/> without saving anything.</summary>
    [HttpPost("{id:guid}/split/preview")]
    public async Task<ApiResponse<OrderSplitDto>> PreviewSplit(
        Guid id,
        SaveSplitRequest request,
        [FromServices] IQueryHandler<PreviewSplitQuery, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new PreviewSplitQuery(id, request.Parts?.Select(p => p?.LineIds).ToList()), cancellationToken)).ToResponse();

    /// <summary>Removes the saved split: fails on a missing order, no saved split, or an order that is not open.</summary>
    [HttpDelete("{id:guid}/split")]
    public async Task<ApiResponse<bool?>> RemoveSplit(
        Guid id,
        [FromServices] ICommandHandler<RemoveSplitCommand, bool?> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new RemoveSplitCommand(id), cancellationToken)).ToResponse();

    [HttpPost("{id:guid}/items")]
    public async Task<ApiResponse<OrderDto>> AddItem(
        Guid id,
        AddOrderItemRequest request,
        [FromServices] ICommandHandler<AddOrderItemCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(
            new AddOrderItemCommand(id, request.MenuItemId, request.Quantity, request.Notes),
            cancellationToken)).ToResponse();

    /// <summary>Removes a line. Fails when a fixed discount is larger than the new subtotal.</summary>
    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    public async Task<ApiResponse<OrderDto>> RemoveLine(
        Guid id,
        Guid lineId,
        [FromServices] ICommandHandler<RemoveOrderLineCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new RemoveOrderLineCommand(id, lineId), cancellationToken)).ToResponse();

    /// <summary>
    /// Sets a line's quantity (at least 1). Fails when a fixed discount is larger than the new subtotal.
    /// </summary>
    [HttpPut("{id:guid}/lines/{lineId:guid}/quantity")]
    public async Task<ApiResponse<OrderDto>> ChangeLineQuantity(
        Guid id,
        Guid lineId,
        ChangeLineQuantityRequest request,
        [FromServices] ICommandHandler<ChangeOrderLineQuantityCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(
            new ChangeOrderLineQuantityCommand(id, lineId, request.Quantity!.Value),
            cancellationToken)).ToResponse();

    [HttpPut("{id:guid}/discount")]
    public async Task<ApiResponse<OrderDto>> ApplyDiscount(
        Guid id,
        ApplyDiscountRequest request,
        [FromServices] ICommandHandler<ApplyDiscountCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new ApplyDiscountCommand(id, request.Rate, request.Amount), cancellationToken)).ToResponse();

    [HttpDelete("{id:guid}/discount")]
    public async Task<ApiResponse<OrderDto>> RemoveDiscount(
        Guid id,
        [FromServices] ICommandHandler<RemoveDiscountCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new RemoveDiscountCommand(id), cancellationToken)).ToResponse();

    [HttpPut("{id:guid}/tip")]
    public async Task<ApiResponse<OrderDto>> SetTip(
        Guid id,
        SetTipRequest request,
        [FromServices] ICommandHandler<SetTipCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new SetTipCommand(id, request.Rate!.Value), cancellationToken)).ToResponse();

    [HttpDelete("{id:guid}/tip")]
    public async Task<ApiResponse<OrderDto>> RemoveTip(
        Guid id,
        [FromServices] ICommandHandler<RemoveTipCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new RemoveTipCommand(id), cancellationToken)).ToResponse();

    [HttpPut("{id:guid}/payment-method")]
    public async Task<ApiResponse<OrderDto>> SetPaymentMethod(
        Guid id,
        SetPaymentMethodRequest request,
        [FromServices] ICommandHandler<SetPaymentMethodCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new SetPaymentMethodCommand(id, request.PaymentMethod!.Value), cancellationToken)).ToResponse();

    [HttpPost("{id:guid}/pay")]
    public async Task<ApiResponse<OrderDto>> Pay(
        Guid id,
        [FromServices] ICommandHandler<PayOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new PayOrderCommand(id), cancellationToken)).ToResponse();

    [HttpPost("{id:guid}/cancel")]
    public async Task<ApiResponse<OrderDto>> Cancel(
        Guid id,
        [FromServices] ICommandHandler<CancelOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        (await handler.HandleAsync(new CancelOrderCommand(id), cancellationToken)).ToResponse();
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
