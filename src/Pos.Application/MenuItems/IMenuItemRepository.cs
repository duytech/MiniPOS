using Pos.Application.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems;

public interface IMenuItemRepository
{
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Adds the menu item and saves it. Throws a <see cref="SaveFailedException"/> for an expected
    /// failure.</summary>
    Task CreateAsync(MenuItem menuItem, CancellationToken cancellationToken);
}
