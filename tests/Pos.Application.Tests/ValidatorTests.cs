using System.Globalization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.MenuItems.CreateMenuItem;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ListOrders;
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

    private IValidator<T> Validator<T>() => _services.GetRequiredService<IValidator<T>>();
}
