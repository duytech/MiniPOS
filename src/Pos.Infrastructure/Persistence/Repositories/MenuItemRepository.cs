using Pos.Application.MenuItems;
using Pos.Domain.MenuItems;

namespace Pos.Infrastructure.Persistence.Repositories;

internal sealed class MenuItemRepository(PosDbContext db) : IMenuItemRepository
{
    public async Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await db.MenuItems.FindAsync([id], cancellationToken);

    public void Add(MenuItem menuItem) => db.MenuItems.Add(menuItem);
}
