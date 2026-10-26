using System.Globalization;
using Pos.Domain.Common;

namespace Pos.Domain.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("10.005")]
    [InlineData("0.125")]
    [InlineData("1.001")]
    public void Rejects_amounts_that_are_not_whole_cents(string amount)
    {
        Assert.Throws<ArgumentException>(() => Money.Usd(decimal.Parse(amount, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void Normalizes_scale_to_two_decimals()
    {
        Assert.Equal("15.40", Money.Usd(15.4m).Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("15.40", Money.Usd(15.400m).Amount.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(Money.Usd(15.4m), Money.Usd(15.400m));
    }

    [Fact]
    public void Adds_exact_cents()
    {
        Assert.Equal(Money.Usd(0.30m), Money.Usd(0.10m) + Money.Usd(0.20m));
    }

    [Fact]
    public void Subtracts_exact_cents()
    {
        Assert.Equal(Money.Usd(0.10m), Money.Usd(0.30m) - Money.Usd(0.20m));
    }

    [Fact]
    public void Multiplies_by_quantity_exactly()
    {
        Assert.Equal(Money.Usd(3.45m), Money.Usd(1.15m) * 3);
        Assert.Equal(Money.Usd(2.01m), Money.Usd(0.67m) * 3);
    }
}
