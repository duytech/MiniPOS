using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Application.Orders;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Pos";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it with user-secrets or the " +
                $"ConnectionStrings__{ConnectionStringName} environment variable.");

        services.AddDbContext<PosDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IMenuItemRepository, MenuItemRepository>();
        return services;
    }

    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.MigrateAsync();
    }
}
