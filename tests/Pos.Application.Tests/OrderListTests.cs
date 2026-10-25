using Pos.Application.Common;
using Pos.Application.Orders;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class OrderListTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);

    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryMenuItemRepository _menuItems = new();
    private readonly OrderService _sut;

    public OrderListTests()
    {
        _sut = new OrderService(_orders, _menuItems, new FakeUnitOfWork(), new FakeClock(Now));
    }

    [Fact]
    public async Task ListAsync_without_filters_uses_defaults()
    {
        await _sut.ListAsync(new ListOrdersRequest());

        Assert.Equal(new OrderListFilter(null, null, OrderService.DefaultListLimit), _orders.LastListFilter);
    }

    [Theory]
    [InlineData("Open", OrderStatus.Open)]
    [InlineData("paid", OrderStatus.Paid)]
    [InlineData("  CANCELLED ", OrderStatus.Cancelled)]
    public async Task ListAsync_parses_status_case_insensitively(string raw, OrderStatus expected)
    {
        await _sut.ListAsync(new ListOrdersRequest(Status: raw));

        Assert.Equal(expected, _orders.LastListFilter!.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ListAsync_blank_status_and_search_mean_no_filter(string? blank)
    {
        await _sut.ListAsync(new ListOrdersRequest(Status: blank, Search: blank));

        Assert.Null(_orders.LastListFilter!.Status);
        Assert.Null(_orders.LastListFilter.Search);
    }

    [Theory]
    [InlineData("Shipped")]
    [InlineData("1")]   // numeric enum values are not accepted
    [InlineData("99")]
    public async Task ListAsync_unknown_status_throws_validation_exception(string raw)
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.ListAsync(new ListOrdersRequest(Status: raw)));

        Assert.Contains("Open, Paid, Cancelled", ex.Message);
        Assert.Null(_orders.LastListFilter);
    }

    [Fact]
    public async Task ListAsync_trims_search_text()
    {
        await _sut.ListAsync(new ListOrdersRequest(Search: "  latte "));

        Assert.Equal("latte", _orders.LastListFilter!.Search);
    }

    [Fact]
    public async Task ListAsync_too_long_search_throws_validation_exception() =>
        await Assert.ThrowsAsync<ValidationException>(
            () => _sut.ListAsync(new ListOrdersRequest(Search: new string('x', OrderService.MaxSearchLength + 1))));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(OrderService.MaxListLimit + 1)]
    public async Task ListAsync_out_of_range_limit_throws_validation_exception(int limit) =>
        await Assert.ThrowsAsync<ValidationException>(() => _sut.ListAsync(new ListOrdersRequest(Limit: limit)));

    [Fact]
    public async Task ListAsync_returns_matching_summaries_from_repository()
    {
        var latte = new MenuItem("Latte", 3.00m);
        var withLatte = new Order(Now);
        withLatte.AddItem(latte, 2);
        var empty = new Order(Now.AddMinutes(-5));
        _orders.Add(withLatte);
        _orders.Add(empty);

        var result = await _sut.ListAsync(new ListOrdersRequest(Status: "open", Search: "LAT"));

        var row = Assert.Single(result);
        Assert.Equal(withLatte.Id, row.Id);
        Assert.Equal("Open", row.Status);
        Assert.Equal(2, row.ItemCount);
        Assert.Equal(6.00m, row.Total);
    }
}
