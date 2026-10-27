using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.MenuItems.CreateMenuItem;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ApplyDiscount;
using Pos.Application.Orders.ChangeOrderLineQuantity;
using Pos.Application.Orders.ListOrders;
using Pos.Application.Orders.RemoveDiscount;
using Pos.Application.Orders.RemoveOrderLine;
using Pos.Application.Orders.RemoveTip;
using Pos.Application.Orders.SetPaymentMethod;
using Pos.Application.Orders.SetTip;
using Pos.Application.Orders.SplitOrderEvenly;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class ValidatorTests
{
    private readonly IServiceProvider _services = new ServiceCollection().AddApplication().BuildServiceProvider();

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public void Add_item_quantity_must_be_positive(int quantity, bool valid)
    {
        var result = Validator<AddOrderItemCommand>()
            .Validate(new AddOrderItemCommand(Guid.NewGuid(), Guid.NewGuid(), quantity, null));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Add_item_requires_ids()
    {
        var result = Validator<AddOrderItemCommand>()
            .Validate(new AddOrderItemCommand(Guid.Empty, Guid.Empty, 1, null));

        Assert.Equal(["OrderId", "MenuItemId"], result.Errors.Select(e => e.PropertyName));
    }

    [Theory]
    [InlineData("0.00", true)]
    [InlineData("9999999999.99", true)]
    [InlineData("-0.01", false)]
    [InlineData("10000000000.00", false)]
    public void Menu_item_price_must_fit_the_money_column_and_not_be_negative(string price, bool valid)
    {
        var result = Validator<CreateMenuItemCommand>()
            .Validate(new CreateMenuItemCommand("Tea", Money.Usd(decimal.Parse(price, CultureInfo.InvariantCulture))));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Menu_item_name_is_required(string name)
    {
        var result = Validator<CreateMenuItemCommand>().Validate(new CreateMenuItemCommand(name, Money.Usd(1m)));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 20, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    [InlineData(int.MaxValue, 100, false)]
    public void List_orders_paging_must_be_in_range(int page, int pageSize, bool valid)
    {
        var result = Validator<ListOrdersQuery>().Validate(new ListOrdersQuery(null, null, page, pageSize));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void List_orders_status_must_be_a_known_status()
    {
        var result = Validator<ListOrdersQuery>().Validate(new ListOrdersQuery(null, (OrderStatus)42, 1, 20));

        Assert.Equal(["Status"], result.Errors.Select(e => e.PropertyName));
    }

    [Theory]
    [InlineData("15", null, true)]
    [InlineData(null, "2.50", true)]
    [InlineData(null, "0.00", true)]
    [InlineData(null, "9999999999.99", true)]
    [InlineData(null, null, false)]
    [InlineData("15", "2.50", false)]
    [InlineData(null, "-0.01", false)]
    [InlineData(null, "10000000000.00", false)]
    public void Discount_has_exactly_one_of_rate_or_amount_and_the_amount_fits(string? rate, string? amount, bool valid)
    {
        var result = Validator<ApplyDiscountCommand>().Validate(new ApplyDiscountCommand(
            Guid.NewGuid(),
            rate is null ? null : new Percentage(decimal.Parse(rate, CultureInfo.InvariantCulture)),
            amount is null ? null : Money.Usd(decimal.Parse(amount, CultureInfo.InvariantCulture))));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("999.9999", true)]
    [InlineData("1000", false)]
    public void Tip_rate_must_fit_the_percentage_column(string rate, bool valid)
    {
        var result = Validator<SetTipCommand>()
            .Validate(new SetTipCommand(Guid.NewGuid(), new Percentage(decimal.Parse(rate, CultureInfo.InvariantCulture))));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Payment_method_must_be_a_known_method()
    {
        var result = Validator<SetPaymentMethodCommand>()
            .Validate(new SetPaymentMethodCommand(Guid.NewGuid(), (PaymentMethod)42));

        Assert.Equal(["PaymentMethod"], result.Errors.Select(e => e.PropertyName));
    }

    [Fact]
    public void Order_changes_require_the_order_id()
    {
        Assert.False(Validator<ApplyDiscountCommand>().Validate(new ApplyDiscountCommand(Guid.Empty, Percentage.Zero, null)).IsValid);
        Assert.False(Validator<RemoveDiscountCommand>().Validate(new RemoveDiscountCommand(Guid.Empty)).IsValid);
        Assert.False(Validator<RemoveOrderLineCommand>().Validate(new RemoveOrderLineCommand(Guid.Empty, Guid.NewGuid())).IsValid);
        Assert.False(Validator<SetTipCommand>().Validate(new SetTipCommand(Guid.Empty, Percentage.Zero)).IsValid);
        Assert.False(Validator<RemoveTipCommand>().Validate(new RemoveTipCommand(Guid.Empty)).IsValid);
        Assert.False(Validator<SetPaymentMethodCommand>()
            .Validate(new SetPaymentMethodCommand(Guid.Empty, PaymentMethod.Card)).IsValid);
    }

    [Theory]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    public void Changed_line_quantity_must_be_positive(int quantity, bool valid)
    {
        var result = Validator<ChangeOrderLineQuantityCommand>()
            .Validate(new ChangeOrderLineQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), quantity));

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Change_line_quantity_requires_both_ids()
    {
        var result = Validator<ChangeOrderLineQuantityCommand>()
            .Validate(new ChangeOrderLineQuantityCommand(Guid.Empty, Guid.Empty, 1));

        Assert.Equal(["OrderId", "LineId"], result.Errors.Select(e => e.PropertyName));
    }

    [Fact]
    public void Remove_line_requires_both_ids()
    {
        var result = Validator<RemoveOrderLineCommand>().Validate(new RemoveOrderLineCommand(Guid.Empty, Guid.Empty));

        Assert.Equal(["OrderId", "LineId"], result.Errors.Select(e => e.PropertyName));
        Assert.True(Validator<RemoveOrderLineCommand>()
            .Validate(new RemoveOrderLineCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    [InlineData(int.MaxValue, false)]
    public void Even_split_is_between_1_and_20_parts(int parts, bool valid)
    {
        Assert.Equal(valid, Validator<SplitOrderEvenlyQuery>().Validate(new SplitOrderEvenlyQuery(Guid.NewGuid(), parts)).IsValid);
        Assert.False(Validator<SplitOrderEvenlyQuery>().Validate(new SplitOrderEvenlyQuery(Guid.Empty, 2)).IsValid);
    }

    private IValidator<T> Validator<T>() => _services.GetRequiredService<IValidator<T>>();
}
