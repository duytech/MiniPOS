using Microsoft.Extensions.DependencyInjection;
using Pos.Application.MenuItems;
using Pos.Application.Orders;

namespace Pos.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<MenuItemService>();
        services.AddScoped<OrderService>();
        return services;
    }
}
