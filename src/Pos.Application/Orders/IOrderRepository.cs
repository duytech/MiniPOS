using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public interface IOrderRepository
{
    /// <summary>Loads the order including its items.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lists order summaries matching the filter, newest first.</summary>
    Task<IReadOnlyList<OrderSummaryDto>> ListAsync(OrderListFilter filter, CancellationToken cancellationToken = default);

    void Add(Order order);
}
