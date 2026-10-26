using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

public class OrderTests
{
    private static readonly MenuItem Burger = new("Burger", Money.Usd(12.99m));
    private static readonly MenuItem Fries = new("Fries", Money.Usd(3.35m));

    [Fact]
    public void New_order_is_open_and_keeps_its_creation_time_in_utc()
    {
        var createdAt = new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.FromHours(7));

        var order = Order.Create(createdAt, Percentage.Zero, Percentage.Zero);

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(createdAt, order.CreatedAt);
        Assert.Equal(TimeSpan.Zero, order.CreatedAt.Offset);
    }

    [Fact]
    public void New_order_has_zero_totals()
    {
        var totals = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero).CalculateTotals();

        Assert.Equal(Money.Usd(0.00m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.00m), totals.Total);
    }

    [Fact]
    public void Line_total_is_unit_price_times_quantity()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);

        var line = order.AddItem(Fries, 3, notes: null);

        Assert.Equal(Money.Usd(10.05m), line.LineTotal);
    }

    [Fact]
    public void Subtotal_is_the_sum_of_line_totals_and_total_equals_subtotal()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        order.AddItem(Burger, 2, notes: null); // 25.98
        order.AddItem(Fries, 3, notes: null); // 10.05

        var totals = order.CalculateTotals();

        Assert.Equal(Money.Usd(36.03m), totals.Subtotal);
        Assert.Equal(Money.Usd(36.03m), totals.Total);
    }

    [Fact]
    public void Sums_without_floating_point_drift()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var dime = new MenuItem("Mint", Money.Usd(0.10m));
        var twenty = new MenuItem("Gum", Money.Usd(0.20m));
        order.AddItem(dime, 1, notes: null);
        order.AddItem(twenty, 1, notes: null);

        Assert.Equal(Money.Usd(0.30m), order.CalculateTotals().Total);
    }

    [Fact]
    public void Identical_line_increases_quantity()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);

        order.AddItem(Burger, 1, "no onions");
        order.AddItem(Burger, 2, "no onions");

        var line = Assert.Single(order.Lines);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(Money.Usd(38.97m), order.CalculateTotals().Total);
    }

    [Fact]
    public void Notes_are_trimmed_and_blank_notes_count_as_no_notes()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);

        order.AddItem(Burger, 1, null);
        order.AddItem(Burger, 1, "   ");
        order.AddItem(Burger, 1, "");

        Assert.Equal(3, Assert.Single(order.Lines).Quantity);
    }

    [Fact]
    public void Different_notes_create_a_new_line()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);

        order.AddItem(Burger, 1, "no onions");
        order.AddItem(Burger, 1, null);

        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void Different_menu_item_creates_a_new_line()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);

        order.AddItem(Burger, 1, null);
        order.AddItem(Fries, 1, null);

        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public void Line_quantity_overflow_is_a_domain_error()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        order.AddItem(Burger, int.MaxValue, null);

        Assert.Throws<DomainException>(() => order.AddItem(Burger, 1, null));
        Assert.Equal(int.MaxValue, Assert.Single(order.Lines).Quantity);
    }

    public static TheoryData<OrderStatus, Action<Order>> ClosedOrderChanges => new()
    {
        { OrderStatus.Paid, o => o.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(10m))) },
        { OrderStatus.Paid, o => o.RemoveDiscount() },
        { OrderStatus.Paid, o => o.SetTip(new Percentage(15m)) },
        { OrderStatus.Paid, o => o.RemoveTip() },
        { OrderStatus.Paid, o => o.SetPaymentMethod(PaymentMethod.Card) },
        { OrderStatus.Cancelled, o => o.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(1.00m))) },
        { OrderStatus.Cancelled, o => o.RemoveDiscount() },
        { OrderStatus.Cancelled, o => o.SetTip(new Percentage(15m)) },
        { OrderStatus.Cancelled, o => o.RemoveTip() },
        { OrderStatus.Cancelled, o => o.SetPaymentMethod(PaymentMethod.Card) },
    };

    [Theory]
    [MemberData(nameof(ClosedOrderChanges))]
    public void Discount_tip_and_payment_method_cannot_change_once_the_order_is_closed(
        OrderStatus status, Action<Order> change)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        order.AddItem(Burger, 1, null);
        var totalsBefore = order.CalculateTotals();
        SetStatus(order, status);

        Assert.Throws<DomainException>(() => change(order));
        Assert.Equal(totalsBefore, order.CalculateTotals());
        Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
    }

    /// <summary>No domain method closes an order yet, so tests set the status directly.</summary>
    private static void SetStatus(Order order, OrderStatus status) =>
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, status);
}
