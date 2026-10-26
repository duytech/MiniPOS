using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Application.Orders;
using Pos.Application.Orders.ListOrders;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

internal sealed class FakeOrderRepository : IOrderRepository
{
    public Dictionary<Guid, Order> Orders { get; } = [];

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Orders.GetValueOrDefault(id));

    public void Add(Order order) => Orders.Add(order.Id, order);

    /// <summary>The arguments of the last <see cref="ListAsync"/> call.</summary>
    public (OrderStatus? Status, OrderSearch? Search, int Page, int PageSize)? LastList { get; private set; }

    /// <summary>Records the arguments and returns every order; filtering and paging are the real repository's job.</summary>
    public Task<(IReadOnlyList<Order> Orders, int TotalCount)> ListAsync(
        OrderStatus? status, OrderSearch? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (status, search, page, pageSize);
        return Task.FromResult<(IReadOnlyList<Order>, int)>((Orders.Values.ToList(), Orders.Count));
    }
}

internal sealed class FakeMenuItemRepository : IMenuItemRepository
{
    public Dictionary<Guid, MenuItem> MenuItems { get; } = [];

    public Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(MenuItems.GetValueOrDefault(id));

    public void Add(MenuItem menuItem) => MenuItems.Add(menuItem.Id, menuItem);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
