using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public interface IOrderRepository
{
    /// <summary>Loads the order including its items.</summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(Order order);
}
