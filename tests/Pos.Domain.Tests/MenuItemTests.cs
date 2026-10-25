using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Domain.Tests;

public class MenuItemTests
{
    [Fact]
    public void Constructor_trims_name_and_sets_fields()
    {
        var item = new MenuItem("  Latte ", 3.20m);

        Assert.Equal("Latte", item.Name);
        Assert.Equal(3.20m, item.Price);
        Assert.True(item.IsAvailable);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_name(string name) =>
        Assert.Throws<DomainException>(() => new MenuItem(name, 1m));

    [Fact]
    public void Constructor_rejects_too_long_name() =>
        Assert.Throws<DomainException>(() => new MenuItem(new string('x', MenuItem.NameMaxLength + 1), 1m));

    [Theory]
    [InlineData("-0.01")]
    [InlineData("1.005")]
    public void Constructor_rejects_negative_price_or_more_than_two_decimals(string price) =>
        Assert.Throws<DomainException>(() => new MenuItem("Latte", decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture)));
}
