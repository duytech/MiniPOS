using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Domain.Common;

namespace Pos.Application.Tests;

public class MenuItemServiceTests
{
    private readonly InMemoryMenuItemRepository _repo = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly MenuItemService _sut;

    public MenuItemServiceTests() => _sut = new MenuItemService(_repo, _unitOfWork);

    [Fact]
    public async Task CreateAsync_persists_menu_item()
    {
        var dto = await _sut.CreateAsync(new CreateMenuItemRequest("Latte", 3.40m));

        Assert.Equal("Latte", dto.Name);
        Assert.Equal(3.40m, dto.Price);
        Assert.True(dto.IsAvailable);
        Assert.True(_repo.Items.ContainsKey(dto.Id));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task CreateAsync_invalid_price_throws_and_does_not_save()
    {
        await Assert.ThrowsAsync<DomainException>(() => _sut.CreateAsync(new CreateMenuItemRequest("Latte", -1m)));
        Assert.Empty(_repo.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetAsync_unknown_id_throws_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAsync(Guid.NewGuid()));

    [Fact]
    public async Task ListAsync_returns_items_sorted_by_name()
    {
        await _sut.CreateAsync(new CreateMenuItemRequest("Tea", 2m));
        await _sut.CreateAsync(new CreateMenuItemRequest("Americano", 2.8m));

        var list = await _sut.ListAsync();

        Assert.Equal(["Americano", "Tea"], list.Select(m => m.Name));
    }
}
