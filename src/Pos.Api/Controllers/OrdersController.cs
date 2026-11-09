using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pos.Application.Common;
using Pos.Application.Orders;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ApplyDiscount;
using Pos.Application.Orders.CancelOrder;
using Pos.Application.Orders.ChangeOrderMenuItemQuantity;
using Pos.Application.Orders.CreateOrder;
using Pos.Application.Orders.GetOrder;
using Pos.Application.Orders.GetSplit;
using Pos.Application.Orders.ListOrders;
using Pos.Application.Orders.PayOrder;
using Pos.Application.Orders.PreviewSplit;
using Pos.Application.Orders.RemoveDiscount;
using Pos.Application.Orders.RemoveOrderMenuItem;
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
    public async Task<Result<OrderDto>> Create(
        [FromServices] ICommandHandler<CreateOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new CreateOrderCommand(), cancellationToken);

    [HttpGet]
    public async Task<Result<PagedResult<OrderSummaryDto>>> List(
        [FromServices] IQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>> handler,
        CancellationToken cancellationToken,
        [FromQuery] string? search = null,
        [FromQuery] OrderStatus? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20) =>
        await handler.HandleAsync(new ListOrdersQuery(search, status, page, pageSize), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<Result<OrderDto>> Get(
        Guid id,
        [FromServices] IQueryHandler<GetOrderQuery, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new GetOrderQuery(id), cancellationToken);

    /// <summary>Splits the bill evenly between <paramref name="parts"/> people; the first part takes the leftover cents.</summary>
    [HttpGet("{id:guid}/split-even")]
    public async Task<Result<IReadOnlyList<SplitPartDto>>> SplitEvenly(
        Guid id,
        [FromQuery, BindRequired] int parts,
        [FromServices] IQueryHandler<SplitOrderEvenlyQuery, IReadOnlyList<SplitPartDto>> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new SplitOrderEvenlyQuery(id, parts), cancellationToken);

    /// <summary>The saved split by item, priced with the current order; works in any status.</summary>
    [HttpGet("{id:guid}/split")]
    public async Task<Result<OrderSplitDto>> GetSplit(
        Guid id,
        [FromServices] IQueryHandler<GetSplitQuery, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new GetSplitQuery(id), cancellationToken);

    /// <summary>
    /// Saves the split by item (at least two parts, every item in exactly one part), replacing any saved one. Only
    /// the item assignment is stored; a real change to the order clears it.
    /// </summary>
    [HttpPut("{id:guid}/split")]
    public async Task<Result<OrderSplitDto>> SaveSplit(
        Guid id,
        SaveSplitRequest request,
        [FromServices] ICommandHandler<SaveSplitCommand, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new SaveSplitCommand(id, request.Parts?.Select(p => p?.ItemIds).ToList()), cancellationToken);

    /// <summary>Prices a split by item like <see cref="SaveSplit"/> without saving anything.</summary>
    [HttpPost("{id:guid}/split/preview")]
    public async Task<Result<OrderSplitDto>> PreviewSplit(
        Guid id,
        SaveSplitRequest request,
        [FromServices] IQueryHandler<PreviewSplitQuery, OrderSplitDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new PreviewSplitQuery(id, request.Parts?.Select(p => p?.ItemIds).ToList()), cancellationToken);

    /// <summary>Removes the saved split: fails on a missing order, no saved split, or an order that is not open.</summary>
    [HttpDelete("{id:guid}/split")]
    public async Task<Result<bool?>> RemoveSplit(
        Guid id,
        [FromServices] ICommandHandler<RemoveSplitCommand, bool?> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new RemoveSplitCommand(id), cancellationToken);

    [HttpPost("{id:guid}/items")]
    public async Task<Result<OrderDto>> AddItem(
        Guid id,
        AddOrderItemRequest request,
        [FromServices] ICommandHandler<AddOrderItemCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(
            new AddOrderItemCommand(id, request.MenuItemId, request.Quantity, request.Notes),
            cancellationToken);

    /// <summary>Removes an item. Fails when a fixed discount is larger than the new subtotal.</summary>
    [HttpDelete("{id:guid}/items/{itemId:guid}")]
    public async Task<Result<OrderDto>> RemoveItem(
        Guid id,
        Guid itemId,
        [FromServices] ICommandHandler<RemoveOrderMenuItemCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new RemoveOrderMenuItemCommand(id, itemId), cancellationToken);

    /// <summary>
    /// Sets an item's quantity (at least 1). Fails when a fixed discount is larger than the new subtotal.
    /// </summary>
    [HttpPut("{id:guid}/items/{itemId:guid}/quantity")]
    public async Task<Result<OrderDto>> ChangeItemQuantity(
        Guid id,
        Guid itemId,
        ChangeItemQuantityRequest request,
        [FromServices] ICommandHandler<ChangeOrderMenuItemQuantityCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(
            new ChangeOrderMenuItemQuantityCommand(id, itemId, request.Quantity!.Value),
            cancellationToken);

    [HttpPut("{id:guid}/discount")]
    public async Task<Result<OrderDto>> ApplyDiscount(
        Guid id,
        ApplyDiscountRequest request,
        [FromServices] ICommandHandler<ApplyDiscountCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new ApplyDiscountCommand(id, request.Rate, request.Amount), cancellationToken);

    [HttpDelete("{id:guid}/discount")]
    public async Task<Result<OrderDto>> RemoveDiscount(
        Guid id,
        [FromServices] ICommandHandler<RemoveDiscountCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new RemoveDiscountCommand(id), cancellationToken);

    [HttpPut("{id:guid}/tip")]
    public async Task<Result<OrderDto>> SetTip(
        Guid id,
        SetTipRequest request,
        [FromServices] ICommandHandler<SetTipCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new SetTipCommand(id, request.Rate!.Value), cancellationToken);

    [HttpDelete("{id:guid}/tip")]
    public async Task<Result<OrderDto>> RemoveTip(
        Guid id,
        [FromServices] ICommandHandler<RemoveTipCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new RemoveTipCommand(id), cancellationToken);

    [HttpPut("{id:guid}/payment-method")]
    public async Task<Result<OrderDto>> SetPaymentMethod(
        Guid id,
        SetPaymentMethodRequest request,
        [FromServices] ICommandHandler<SetPaymentMethodCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new SetPaymentMethodCommand(id, request.PaymentMethod!.Value), cancellationToken);

    [HttpPost("{id:guid}/pay")]
    public async Task<Result<OrderDto>> Pay(
        Guid id,
        [FromServices] ICommandHandler<PayOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new PayOrderCommand(id), cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    public async Task<Result<OrderDto>> Cancel(
        Guid id,
        [FromServices] ICommandHandler<CancelOrderCommand, OrderDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new CancelOrderCommand(id), cancellationToken);
}

public sealed record AddOrderItemRequest(Guid MenuItemId, int Quantity, string? Notes);

public sealed record ChangeItemQuantityRequest([Required] int? Quantity);

/// <summary>Exactly one of <paramref name="Rate"/> (percent of subtotal) or <paramref name="Amount"/> (fixed).</summary>
public sealed record ApplyDiscountRequest(Percentage? Rate, Money? Amount);

public sealed record SetTipRequest([Required] Percentage? Rate);

public sealed record SetPaymentMethodRequest([Required] PaymentMethod? PaymentMethod);

/// <summary>A split by item: the item ids of each part, in part order. Used by PUT and preview.</summary>
public sealed record SaveSplitRequest(IReadOnlyList<SplitPartRequest?>? Parts);

public sealed record SplitPartRequest(IReadOnlyList<Guid>? ItemIds);
