using Pos.Application.Orders.ListOrders;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public interface IOrderRepository
{
    /// <summary>Loads the order with its lines, tracked for changes.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads one page (1-based) of matching orders with their lines, not tracked, newest first, and the number of all
    /// matching orders.
    /// </summary>
    Task<(IReadOnlyList<Order> Orders, int TotalCount)> ListAsync(
        OrderStatus? status, OrderSearch? search, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Adds the order and saves it. Fails with a constraint violation; that is expected, anything else throws.</summary>
    Task<Result> CreateAsync(Order order, CancellationToken cancellationToken);

    /// <summary>
    /// Saves the changes made to orders loaded by <see cref="GetByIdAsync"/>. Fails with a concurrent change or a
    /// constraint violation; those are expected, anything else throws.
    /// </summary>
    Task<Result> SaveAsync(CancellationToken cancellationToken);
}
