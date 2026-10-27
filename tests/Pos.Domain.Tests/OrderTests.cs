using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

public class OrderTests
{
    private static readonly MenuItem Burger = new("Burger", Money.Usd(12.99m));
    private static readonly MenuItem Fries = new("Fries", Money.Usd(3.35m));
    private static readonly DateTimeOffset ClosedAt = new(2026, 10, 27, 9, 15, 0, TimeSpan.Zero);

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

    [Fact]
    public void Removing_a_line_keeps_the_other_lines()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var burger = order.AddItem(Burger, 2, null);
        var fries = order.AddItem(Fries, 1, null);

        order.RemoveLine(burger);

        Assert.Same(fries, Assert.Single(order.Lines));
        Assert.Equal(Money.Usd(3.35m), order.CalculateTotals().Total);
    }

    [Fact]
    public void Removing_the_last_line_leaves_the_order_open_and_empty()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var burger = order.AddItem(Burger, 1, null);

        order.RemoveLine(burger);

        Assert.Empty(order.Lines);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(Money.Usd(0.00m), order.CalculateTotals().Total);
    }

    [Fact]
    public void Removing_a_line_of_another_order_is_a_domain_error()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        order.AddItem(Burger, 1, null);
        var other = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var otherLine = other.AddItem(Burger, 1, null);

        Assert.Throws<DomainException>(() => order.RemoveLine(otherLine));
        Assert.Single(order.Lines);
    }

[Fact]
    public void Changing_a_line_quantity_replaces_it_and_keeps_the_other_lines()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var burger = order.AddItem(Burger, 2, null);
        var fries = order.AddItem(Fries, 1, null);

        order.ChangeLineQuantity(burger, 5);

        Assert.Equal(5, burger.Quantity);
        Assert.Equal(1, fries.Quantity);
        Assert.Equal(2, order.Lines.Count);
        Assert.Equal(Money.Usd(68.30m), order.CalculateTotals().Total); // 12.99 × 5 + 3.35

        order.ChangeLineQuantity(burger, 1);

        Assert.Equal(1, burger.Quantity);
        Assert.Equal(Money.Usd(16.34m), order.CalculateTotals().Total);
    }

    [Fact]
    public void Changing_a_line_to_its_current_quantity_leaves_the_order_unchanged()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var burger = order.AddItem(Burger, 2, null);

        order.ChangeLineQuantity(burger, 2);

        Assert.Equal(2, Assert.Single(order.Lines).Quantity);
        Assert.Equal(Money.Usd(25.98m), order.CalculateTotals().Total);
    }

    [Fact]
    public void Changing_the_quantity_of_a_line_of_another_order_is_a_domain_error()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        order.AddItem(Burger, 1, null);
        var other = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var otherLine = other.AddItem(Burger, 1, null);

        Assert.Throws<DomainException>(() => order.ChangeLineQuantity(otherLine, 3));
        Assert.Equal(1, Assert.Single(order.Lines).Quantity);
        Assert.Equal(1, otherLine.Quantity);
    }

        public static TheoryData<OrderStatus, Action<Order>> ClosedOrderChanges => new()
    {
        { OrderStatus.Paid, o => o.RemoveLine(o.Lines[0]) },
        { OrderStatus.Cancelled, o => o.RemoveLine(o.Lines[0]) },
        { OrderStatus.Paid, o => o.ChangeLineQuantity(o.Lines[0], 3) },
        { OrderStatus.Cancelled, o => o.ChangeLineQuantity(o.Lines[0], 3) },
        { OrderStatus.Paid, o => o.AddItem(Burger, 1, null) },
        { OrderStatus.Paid, o => o.AddItem(Fries, 1, null) },
        { OrderStatus.Cancelled, o => o.AddItem(Burger, 1, null) },
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
    public void Items_discount_tip_and_payment_method_cannot_change_once_the_order_is_closed(
        OrderStatus status, Action<Order> change)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        order.AddItem(Burger, 1, null);
        var totalsBefore = order.CalculateTotals();
        Close(order, status);

        Assert.Throws<DomainException>(() => change(order));
        Assert.Equal(1, Assert.Single(order.Lines).Quantity);
        Assert.Equal(totalsBefore, order.CalculateTotals());
        Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public void Closing_an_open_order_sets_the_status_and_its_time_and_keeps_the_totals(OrderStatus status)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, new Percentage(8.875m), new Percentage(3m));
        order.AddItem(new MenuItem("Che", Money.Usd(0.70m)), 1, null);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(15m)));
        order.SetTip(new Percentage(15m));
        order.SetPaymentMethod(PaymentMethod.Card);
        var totalsBefore = order.CalculateTotals();

        Close(order, status);

        Assert.Equal(status, order.Status);
        Assert.Equal(status == OrderStatus.Paid ? ClosedAt : null, order.PaidAt);
        Assert.Equal(status == OrderStatus.Cancelled ? ClosedAt : null, order.CancelledAt);
        var totals = order.CalculateTotals();
        Assert.Equal(totalsBefore, totals);
        Assert.Equal(Money.Usd(0.70m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.11m), totals.Discount); // 15% of 0.70 = 0.105, midpoint away from zero
        Assert.Equal(Money.Usd(0.02m), totals.Surcharge); // 3% of 0.59 = 0.0177
        Assert.Equal(Money.Usd(0.05m), totals.Tax); // 8.875% of 0.61 = 0.0541375
        Assert.Equal(Money.Usd(0.09m), totals.Tip); // 15% of 0.59 = 0.0885
        Assert.Equal(Money.Usd(0.75m), totals.Total);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public void An_empty_order_can_be_closed(OrderStatus status)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, new Percentage(8.875m), Percentage.Zero);

        Close(order, status);

        Assert.Equal(status, order.Status);
        Assert.Empty(order.Lines);
        Assert.Equal(Money.Usd(0.00m), order.CalculateTotals().Total);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, OrderStatus.Paid)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public void A_closed_order_cannot_be_paid_or_cancelled_again(OrderStatus first, OrderStatus second)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        order.AddItem(Burger, 1, null);
        Close(order, first);
        var (paidAt, cancelledAt) = (order.PaidAt, order.CancelledAt);

        Assert.Throws<DomainException>(() => Close(order, second, ClosedAt.AddMinutes(5)));
        Assert.Equal(first, order.Status);
        Assert.Equal(paidAt, order.PaidAt);
        Assert.Equal(cancelledAt, order.CancelledAt);
        Assert.Equal(Money.Usd(12.99m), order.CalculateTotals().Total);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public void The_closing_time_is_kept_in_utc(OrderStatus status)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero, Percentage.Zero);
        var closedAt = new DateTimeOffset(2026, 10, 27, 16, 15, 0, TimeSpan.FromHours(7));

        Close(order, status, closedAt);

        var stored = status == OrderStatus.Paid ? order.PaidAt : order.CancelledAt;
        Assert.Equal(closedAt, stored);
        Assert.Equal(TimeSpan.Zero, stored!.Value.Offset);
    }

    private static void Close(Order order, OrderStatus status, DateTimeOffset? at = null)
    {
        switch (status)
        {
            case OrderStatus.Paid:
                order.Pay(at ?? ClosedAt);
                break;
            case OrderStatus.Cancelled:
                order.Cancel(at ?? ClosedAt);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Paid or Cancelled");
        }
    }
}
