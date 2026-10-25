using Microsoft.EntityFrameworkCore;
using Pos.Application.MenuItems;
using Pos.Domain.MenuItems;

namespace Pos.Infrastructure.Persistence.Repositories;

public class MenuItemRepository(PosDbContext db) : IMenuItemRepository
{
    public Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.MenuItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MenuItem>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.MenuItems.AsNoTracking().OrderBy(m => m.Name).ToListAsync(cancellationToken);

    public void Add(MenuItem menuItem) => db.MenuItems.Add(menuItem);
}
