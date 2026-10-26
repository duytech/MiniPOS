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
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MenuItemDto>> Create(
        CreateMenuItemRequest request,
        [FromServices] ICommandHandler<CreateMenuItemCommand, MenuItemDto> handler,
        CancellationToken cancellationToken)
    {
        var menuItem = await handler.HandleAsync(new CreateMenuItemCommand(request.Name, request.Price!.Value), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = menuItem.Id }, menuItem);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<MenuItemDto> Get(
        Guid id,
        [FromServices] IQueryHandler<GetMenuItemQuery, MenuItemDto> handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(new GetMenuItemQuery(id), cancellationToken);
}

public sealed record CreateMenuItemRequest(string Name, [Required] Money? Price);
