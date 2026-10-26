using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems;

public interface IMenuItemRepository
{
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(MenuItem menuItem);
}
