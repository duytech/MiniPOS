using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems;

public interface IMenuItemRepository
{
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds the menu item and saves it. Fails with a constraint violation; that is expected, anything else throws.</summary>
    Task<Result> CreateAsync(MenuItem menuItem, CancellationToken cancellationToken);
}
