using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Application.MenuItems.CreateMenuItem;
using Pos.Application.MenuItems.GetMenuItem;
using Pos.Domain.Common;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/menu-items")]
public sealed class MenuItemsController : ControllerBase
{
    [HttpPost]
    public async Task<Result<MenuItemDto>> Create(
        CreateMenuItemRequest request,
        [FromServices] ICommandHandler<CreateMenuItemCommand, MenuItemDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new CreateMenuItemCommand(request.Name, request.Price!.Value), cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<Result<MenuItemDto>> Get(
        Guid id,
        [FromServices] IQueryHandler<GetMenuItemQuery, MenuItemDto> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new GetMenuItemQuery(id), cancellationToken);
}

public sealed record CreateMenuItemRequest(string Name, [Required] Money? Price);
