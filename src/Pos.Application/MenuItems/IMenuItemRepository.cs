using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems;

public interface IMenuItemRepository
{
    Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MenuItem>> ListAsync(CancellationToken cancellationToken = default);
    void Add(MenuItem menuItem);
}
