using Pos.Application.MenuItems;
using Pos.Domain.MenuItems;

namespace Pos.Infrastructure.Persistence.Repositories;

internal sealed class MenuItemRepository(PosDbContext db) : BaseRepository<MenuItem>(db), IMenuItemRepository
{
    public async Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await Entities.FindAsync([id], cancellationToken);

    public Task CreateAsync(MenuItem menuItem, CancellationToken cancellationToken)
    {
        Entities.Add(menuItem);
        return SaveChangesAsync(cancellationToken);
    }
}
