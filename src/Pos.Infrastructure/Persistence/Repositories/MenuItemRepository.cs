using Pos.Application.MenuItems;
using Pos.Domain.MenuItems;

namespace Pos.Infrastructure.Persistence.Repositories;

internal sealed class MenuItemRepository(PosDbContext db) : BaseRepository(db), IMenuItemRepository
{
    public async Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await Db.MenuItems.FindAsync([id], cancellationToken);

    public Task CreateAsync(MenuItem menuItem, CancellationToken cancellationToken)
    {
        Db.MenuItems.Add(menuItem);
        return SaveChangesAsync(cancellationToken);
    }
}
