using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Application.MenuItems.CreateMenuItem;
using Pos.Application.MenuItems.GetMenuItem;
using Pos.Application.Orders;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ApplyDiscount;
using Pos.Application.Orders.ChangeOrderLineQuantity;
using Pos.Application.Orders.CreateOrder;
using Pos.Application.Orders.GetOrder;
using Pos.Application.Orders.ListOrders;
using Pos.Application.Orders.RemoveDiscount;
using Pos.Application.Orders.RemoveOrderLine;
using Pos.Application.Orders.RemoveTip;
using Pos.Application.Orders.SetPaymentMethod;
using Pos.Application.Orders.SetTip;
using Pos.Application.Orders.SplitOrderEvenly;

namespace Pos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);

        services.AddCommandHandler<CreateMenuItemCommand, MenuItemDto, CreateMenuItemHandler>();
        services.AddScoped<IQueryHandler<GetMenuItemQuery, MenuItemDto>, GetMenuItemHandler>();

        services.AddCommandHandler<CreateOrderCommand, OrderDto, CreateOrderHandler>();
        services.AddCommandHandler<AddOrderItemCommand, OrderDto, AddOrderItemHandler>();
        services.AddCommandHandler<RemoveOrderLineCommand, OrderDto, RemoveOrderLineHandler>();
        services.AddCommandHandler<ChangeOrderLineQuantityCommand, OrderDto, ChangeOrderLineQuantityHandler>();
        services.AddCommandHandler<ApplyDiscountCommand, OrderDto, ApplyDiscountHandler>();
        services.AddCommandHandler<RemoveDiscountCommand, OrderDto, RemoveDiscountHandler>();
        services.AddCommandHandler<SetTipCommand, OrderDto, SetTipHandler>();
        services.AddCommandHandler<RemoveTipCommand, OrderDto, RemoveTipHandler>();
        services.AddCommandHandler<SetPaymentMethodCommand, OrderDto, SetPaymentMethodHandler>();
        services.AddScoped<IQueryHandler<GetOrderQuery, OrderDto>, GetOrderHandler>();
        services.AddQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>, ListOrdersHandler>();
        services.AddQueryHandler<SplitOrderEvenlyQuery, IReadOnlyList<SplitPartDto>, SplitOrderEvenlyHandler>();

        return services;
    }

    private static void AddQueryHandler<TQuery, TResult, THandler>(this IServiceCollection services)
        where THandler : class, IQueryHandler<TQuery, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<IQueryHandler<TQuery, TResult>>(sp => new ValidatingQueryHandler<TQuery, TResult>(
            sp.GetRequiredService<THandler>(),
            sp.GetServices<IValidator<TQuery>>()));
    }

    private static void AddCommandHandler<TCommand, TResult, THandler>(this IServiceCollection services)
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(sp => new ValidatingCommandHandler<TCommand, TResult>(
            sp.GetRequiredService<THandler>(),
            sp.GetServices<IValidator<TCommand>>()));
    }
}
