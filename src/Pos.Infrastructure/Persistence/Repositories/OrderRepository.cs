using Microsoft.EntityFrameworkCore;
using Pos.Application.Orders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Repositories;

public class OrderRepository(PosDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void Add(Order order) => db.Orders.Add(order);
}
