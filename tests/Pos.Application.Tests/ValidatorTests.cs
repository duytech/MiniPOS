using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.MenuItems.CreateMenuItem;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ApplyDiscount;
using Pos.Application.Orders.ChangeOrderMenuItemQuantity;
using Pos.Application.Orders.ListOrders;
using Pos.Application.Orders.PreviewSplit;
using Pos.Application.Orders.SaveSplit;
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
    public void Add_item_requires_the_menu_item_id()
    {
        var result = Validator<AddOrderItemCommand>()
            .Validate(new AddOrderItemCommand(Guid.NewGuid(), Guid.Empty, 1, null));

        Assert.Equal(["MenuItemId"], result.Errors.Select(e => e.PropertyName));
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

    /// <summary>
    /// Shapes for the split by item body. Part counts, empty parts and which items are in them are the order's rules
    /// (422), so one part or an empty part is a valid shape here.
    /// </summary>
    public static TheoryData<string, bool> SplitShapes => new()
    {
        { "two-parts", true },
        { "one-part", true },
        { "no-parts", true },
        { "empty-part", true },
        { "parts-null", false },
        { "part-null", false },
        { "item-id-empty", false },
    };

    [Theory]
    [MemberData(nameof(SplitShapes))]
    public void Save_and_preview_split_check_the_shape_only(string shape, bool valid)
    {
        var (orderId, parts) = SplitShape(shape);

        Assert.Equal(valid, Validator<SaveSplitCommand>().Validate(new SaveSplitCommand(orderId, parts)).IsValid);
        Assert.Equal(valid, Validator<PreviewSplitQuery>().Validate(new PreviewSplitQuery(orderId, parts)).IsValid);
    }

    private static (Guid OrderId, IReadOnlyList<IReadOnlyList<Guid>?>? Parts) SplitShape(string shape)
    {
        Guid[] a = [Guid.NewGuid()];
        Guid[] b = [Guid.NewGuid(), Guid.NewGuid()];
        return shape switch
        {
            "two-parts" => (Guid.NewGuid(), [a, b]),
            "one-part" => (Guid.NewGuid(), [a]),
            "no-parts" => (Guid.NewGuid(), []),
            "empty-part" => (Guid.NewGuid(), [a, []]),
            "parts-null" => (Guid.NewGuid(), null),
            "part-null" => (Guid.NewGuid(), [a, null]),
            _ => (Guid.NewGuid(), [a, [Guid.NewGuid(), Guid.Empty]]),
        };
    }

    [Theory]
    [InlineData(int.MinValue, false)]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(int.MaxValue, true)]
    public void Changed_item_quantity_must_be_positive(int quantity, bool valid)
    {
        var result = Validator<ChangeOrderMenuItemQuantityCommand>()
            .Validate(new ChangeOrderMenuItemQuantityCommand(Guid.NewGuid(), Guid.NewGuid(), quantity));

        Assert.Equal(valid, result.IsValid);
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
    }

    private IValidator<T> Validator<T>() => _services.GetRequiredService<IValidator<T>>();
}
