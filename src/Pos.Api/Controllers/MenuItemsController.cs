using Microsoft.AspNetCore.Mvc;
using Pos.Application.MenuItems;

namespace Pos.Api.Controllers;

[ApiController]
[Route("api/menu-items")]
[Produces("application/json")]
public class MenuItemsController(MenuItemService menuItems) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MenuItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MenuItemDto>>> List(CancellationToken cancellationToken) =>
        Ok(await menuItems.ListAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> Get(Guid id, CancellationToken cancellationToken) =>
        await menuItems.GetAsync(id, cancellationToken);

    [HttpPost]
    [ProducesResponseType<MenuItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MenuItemDto>> Create(CreateMenuItemRequest request, CancellationToken cancellationToken)
    {
        var menuItem = await menuItems.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = menuItem.Id }, menuItem);
    }
}
