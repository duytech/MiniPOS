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

    [Theory]
    [InlineData("1.009", "1.00")]
    [InlineData("1.005", "1.00")] // midpoint: rounds down, not away from zero
    [InlineData("3.3333333333", "3.33")]
    [InlineData("2.5", "2.50")]
    [InlineData("0", "0.00")]
    public void RoundDownToCent_rounds_down_to_whole_cents(string amount, string expected)
    {
        var rounded = Money.RoundDownToCent(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.USD);

        Assert.Equal(expected, rounded.Amount.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(Currency.USD, rounded.Currency);
    }

    [Theory]
    [InlineData("0.505", "0.51")] // midpoint: away from zero, not to even (0.50)
    [InlineData("0.515", "0.52")]
    [InlineData("0.8875", "0.89")]
    [InlineData("1.004", "1.00")]
    [InlineData("2.5", "2.50")]
    public void RoundToNearestCent_rounds_half_a_cent_away_from_zero(string amount, string expected)
    {
        var rounded = Money.RoundToNearestCent(decimal.Parse(amount, CultureInfo.InvariantCulture), Currency.USD);

        Assert.Equal(expected, rounded.Amount.ToString(CultureInfo.InvariantCulture));
        Assert.Equal(Currency.USD, rounded.Currency);
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
