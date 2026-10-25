using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Application.Orders;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}

internal sealed class InMemoryMenuItemRepository : IMenuItemRepository
{
    public Dictionary<Guid, MenuItem> Items { get; } = [];

    public Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.GetValueOrDefault(id));

    public Task<IReadOnlyList<MenuItem>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MenuItem>>(Items.Values.OrderBy(m => m.Name).ToList());

    public void Add(MenuItem menuItem) => Items.Add(menuItem.Id, menuItem);
}

internal sealed class InMemoryOrderRepository : IOrderRepository
{
    public Dictionary<Guid, Order> Orders { get; } = [];

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Orders.GetValueOrDefault(id));

    public void Add(Order order) => Orders.Add(order.Id, order);
}
