using Pos.Domain.Common;

namespace Pos.Domain.Tests;

public class PercentageTests
{
    [Fact]
    public void Rejects_negative_values()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Percentage(-0.0001m));
    }

    [Fact]
    public void Rejects_more_decimal_places_than_the_database_stores()
    {
        Assert.Throws<ArgumentException>(() => new Percentage(8.87501m));
        Assert.Equal(8.8750m, new Percentage(8.8750m).Value);
    }
}
