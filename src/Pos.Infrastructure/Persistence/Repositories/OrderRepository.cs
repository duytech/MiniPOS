using Microsoft.EntityFrameworkCore;
using Pos.Application.Orders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Repositories;

public class OrderRepository(PosDbContext db) : IOrderRepository
{
    private const string LikeEscape = "\\";

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OrderSummaryDto>> ListAsync(OrderListFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = db.Orders.AsNoTracking();

        if (filter.Status is { } status)
            query = query.Where(o => o.Status == status);

        if (filter.Search is { } search)
        {
            // Case-insensitive "contains" on the order id (as text) or any item name.
            // LIKE wildcards in user input are escaped so they match literally.
            var pattern = $"%{EscapeLike(search)}%";
            query = query.Where(o =>
                EF.Functions.ILike(o.Id.ToString(), pattern, LikeEscape) ||
                o.Items.Any(i => EF.Functions.ILike(i.Name, pattern, LikeEscape)));
        }

        var rows = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenBy(o => o.Id)
            .Take(filter.Limit)
            .Select(o => new
            {
                o.Id,
                o.CreatedAtUtc,
                o.Status,
                ItemCount = o.Items.Sum(i => (int?)i.Quantity) ?? 0,
                Total = o.Items.Sum(i => (decimal?)(i.UnitPrice * i.Quantity)) ?? 0m,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new OrderSummaryDto(
                r.Id, DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc), r.Status.ToString(), r.ItemCount, r.Total))
            .ToList();
    }

    public void Add(Order order) => db.Orders.Add(order);

    private static string EscapeLike(string value) =>
        value.Replace(LikeEscape, LikeEscape + LikeEscape)
             .Replace("%", LikeEscape + "%")
             .Replace("_", LikeEscape + "_");
}
