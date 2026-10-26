using Microsoft.EntityFrameworkCore;
using Pos.Application.Orders;
using Pos.Application.Orders.ListOrders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(PosDbContext db) : IOrderRepository
{
    private const string LikeEscape = "\\";

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Order> Orders, int TotalCount)> ListAsync(
        OrderStatus? status, OrderSearch? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Orders.AsNoTracking();
        if (status is not null)
        {
            query = query.Where(o => o.Status == status);
        }

        if (search is not null)
        {
            var pattern = $"%{EscapeLike(search.Text)}%";
            query = search.Number is { } number
                ? query.Where(o => o.Number == number
                    || o.Lines.Any(l => EF.Functions.ILike(l.Name, pattern, LikeEscape)))
                : query.Where(o => o.Lines.Any(l => EF.Functions.ILike(l.Name, pattern, LikeEscape)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Number)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(o => o.Lines)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return (orders, totalCount);
    }

    public void Add(Order order) => db.Orders.Add(order);

    /// <summary>Makes '%', '_' and the escape character in user input match themselves in an ILIKE pattern.</summary>
    private static string EscapeLike(string text) =>
        text.Replace(LikeEscape, LikeEscape + LikeEscape).Replace("%", LikeEscape + "%").Replace("_", LikeEscape + "_");
}
