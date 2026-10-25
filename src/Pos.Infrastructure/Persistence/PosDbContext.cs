using Microsoft.EntityFrameworkCore;
using Pos.Application.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence;

public class PosDbContext(DbContextOptions<PosDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);
    }

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) =>
        await SaveChangesAsync(cancellationToken);
}
