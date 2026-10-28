using Microsoft.EntityFrameworkCore;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;
using Pos.Infrastructure.Persistence.Configurations;

namespace Pos.Infrastructure.Persistence;

public class PosDbContext(DbContextOptions<PosDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PosDbContext).Assembly);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        TouchOrdersWithChangedLines();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The order's xmin only changes when the orders row is updated. A change to a line alone would not touch it, so
    /// two concurrent add-item requests could both succeed and one quantity would be lost. Updating the order row
    /// whenever one of its lines changes makes every line change go through the order's concurrency check. The same
    /// goes for the saved split: its parts are only ever added or deleted (a new split replaces them all).
    /// </summary>
    private void TouchOrdersWithChangedLines()
    {
        ChangeTracker.DetectChanges();
        var changedOrderIds = ChangeTracker.Entries<OrderLine>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Property<Guid>(OrderLineConfiguration.OrderIdProperty).CurrentValue)
            .Concat(ChangeTracker.Entries<SavedSplitPart>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => e.Property<Guid>(SavedSplitPartConfiguration.OrderIdProperty).CurrentValue))
            .ToHashSet();

        foreach (var order in ChangeTracker.Entries<Order>())
        {
            if (order.State == EntityState.Unchanged && changedOrderIds.Contains(order.Entity.Id))
            {
                order.State = EntityState.Modified;
            }
        }
    }
}
