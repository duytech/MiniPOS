using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

public class OrderTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_order_is_open_empty_and_has_zero_total()
    {
        var order = new Order(Now);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(Now, order.CreatedAtUtc);
        Assert.Empty(order.Items);
        Assert.Equal(0m, order.Total);
    }

    [Fact]
    public void AddItem_snapshots_menu_item_name_and_price()
    {
        var order = new Order(Now);
        var espresso = new MenuItem("Espresso", 2.50m);

        var line = order.AddItem(espresso, 2);

        Assert.Equal(order.Id, line.OrderId);
        Assert.Equal(espresso.Id, line.MenuItemId);
        Assert.Equal("Espresso", line.Name);
        Assert.Equal(2.50m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(5.00m, line.LineTotal);
    }

    [Fact]
    public void Total_is_sum_of_unit_price_times_quantity_across_lines()
    {
        var order = new Order(Now);
        order.AddItem(new MenuItem("Espresso", 2.50m), 2);   // 5.00
        order.AddItem(new MenuItem("Croissant", 2.20m), 3);  // 6.60
        order.AddItem(new MenuItem("Water", 0m), 1);         // 0.00

        Assert.Equal(3, order.Items.Count);
        Assert.Equal(11.60m, order.Total);
    }

    [Fact]
    public void Adding_same_menu_item_twice_merges_into_one_line()
    {
        var order = new Order(Now);
        var espresso = new MenuItem("Espresso", 2.50m);

        order.AddItem(espresso, 1);
        order.AddItem(espresso, 2);

        var line = Assert.Single(order.Items);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(7.50m, order.Total);
    }

    [Fact]
    public void Later_menu_price_change_does_not_affect_existing_order_lines()
    {
        var order = new Order(Now);
        var espresso = new MenuItem("Espresso", 2.50m);
        order.AddItem(espresso, 2);

        espresso.ChangePrice(9.99m);

        Assert.Equal(5.00m, order.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_rejects_non_positive_quantity(int quantity)
    {
        var order = new Order(Now);

        var ex = Assert.Throws<DomainException>(() => order.AddItem(new MenuItem("Espresso", 2.50m), quantity));
        Assert.Contains("greater than zero", ex.Message);
        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_rejects_quantity_above_limit_including_merged_quantity()
    {
        var order = new Order(Now);
        var espresso = new MenuItem("Espresso", 2.50m);
        order.AddItem(espresso, Order.MaxQuantityPerLine);

        Assert.Throws<DomainException>(() => order.AddItem(espresso, 1));
        Assert.Equal(Order.MaxQuantityPerLine, Assert.Single(order.Items).Quantity);
    }

    [Fact]
    public void AddItem_rejects_unavailable_menu_item()
    {
        var order = new Order(Now);
        var soldOut = new MenuItem("Seasonal Pie", 4.00m, isAvailable: false);

        Assert.Throws<DomainException>(() => order.AddItem(soldOut, 1));
        Assert.Empty(order.Items);
    }

    [Fact]
    public void AddItem_throws_on_null_menu_item()
    {
        var order = new Order(Now);

        Assert.Throws<ArgumentNullException>(() => order.AddItem(null!, 1));
    }
}
