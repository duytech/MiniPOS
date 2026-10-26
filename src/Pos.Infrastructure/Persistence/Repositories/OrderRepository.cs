using Microsoft.EntityFrameworkCore;
using Pos.Application.Orders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(PosDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void Add(Order order) => db.Orders.Add(order);
}
