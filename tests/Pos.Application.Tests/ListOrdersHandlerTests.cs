using Pos.Application.Orders.ListOrders;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class ListOrdersHandlerTests
{
    private readonly FakeOrderRepository _orders = new();
    private readonly ListOrdersHandler _handler;

    public ListOrdersHandlerTests() => _handler = new ListOrdersHandler(_orders);

    [Fact]
    public async Task Summary_total_is_the_domain_total_in_exact_cents()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero);
        order.AddItem(new MenuItem("Pho", Money.Usd(7.70m)), 2, null); // 15.40
        order.AddItem(new MenuItem("Iced tea", Money.Usd(1.15m)), 3, null); // 3.45
        order.AddItem(new MenuItem("Pho", Money.Usd(7.70m)), 1, "no onions"); // 7.70
        _orders.Add(order);

        var result = await _handler.HandleAsync(new ListOrdersQuery(null, null, 1, 20), CancellationToken.None);

        var summary = Assert.Single(result.Items);
        Assert.Equal(Money.Usd(26.55m), summary.Total);
        Assert.Equal(order.CalculateTotals().Total, summary.Total);
        Assert.Equal(["Pho", "Iced tea"], summary.ItemNames);
        Assert.Equal(OrderStatus.Open, summary.Status);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Passes_status_parsed_search_and_paging_to_the_repository()
    {
        var result = await _handler.HandleAsync(
            new ListOrdersQuery("  #12 ", OrderStatus.Paid, 3, 10), CancellationToken.None);

        Assert.Equal((OrderStatus.Paid, new OrderSearch("#12", 12), 3, 10), _orders.LastList);
        Assert.Equal(3, result.Page);
        Assert.Equal(10, result.PageSize);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_search_is_no_search(string? search) => Assert.Null(OrderSearch.Parse(search));

    [Theory]
    [InlineData("12", 12)]
    [InlineData("#12", 12)]
    [InlineData(" #007 ", 7)]
    [InlineData("pho", null)]
    [InlineData("#", null)]
    [InlineData("12.5", null)]
    [InlineData("-12", null)]
    [InlineData("+12", null)]
    [InlineData("1 2", null)]
    [InlineData("99999999999", null)]
    public void Search_is_also_an_order_number_only_when_it_is_a_whole_number(string search, int? number)
    {
        var parsed = OrderSearch.Parse(search);

        Assert.NotNull(parsed);
        Assert.Equal(search.Trim(), parsed.Text);
        Assert.Equal(number, parsed.Number);
    }
}
