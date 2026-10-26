using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Application.MenuItems.CreateMenuItem;
using Pos.Application.MenuItems.GetMenuItem;
using Pos.Application.Orders;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.CreateOrder;
using Pos.Application.Orders.GetOrder;
using Pos.Application.Orders.ListOrders;

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
        services.AddScoped<IQueryHandler<GetOrderQuery, OrderDto>, GetOrderHandler>();
        services.AddQueryHandler<ListOrdersQuery, PagedResult<OrderSummaryDto>, ListOrdersHandler>();

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
