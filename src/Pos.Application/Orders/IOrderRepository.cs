using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public interface IOrderRepository
{
    /// <summary>Loads the order with its lines, tracked for changes.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(Order order);
}
