using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pos.Application.MenuItems.CreateMenuItem;
using Pos.Application.Orders.AddOrderItem;
using Pos.Domain.Common;

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
            .Validate(new CreateMenuItemCommand("Tea", Money.Usd(decimal.Parse(price))));

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

    private IValidator<T> Validator<T>() => _services.GetRequiredService<IValidator<T>>();
}
