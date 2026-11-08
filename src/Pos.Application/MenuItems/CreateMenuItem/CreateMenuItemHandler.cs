using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems.CreateMenuItem;

public sealed class CreateMenuItemHandler(IMenuItemRepository menuItems, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateMenuItemCommand, MenuItemDto>
{
    public async Task<Result<MenuItemDto>> HandleAsync(CreateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var menuItem = new MenuItem { Id = Guid.NewGuid(), Name = command.Name.Trim(), Price = command.Price };
        menuItems.Add(menuItem);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(MenuItemDto.From(menuItem)) : Result.Fail<MenuItemDto>(saved.Error);
    }
}
