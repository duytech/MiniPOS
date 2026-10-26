namespace Pos.Domain.Common;

/// <summary>
/// A rate in percent (8.875 means 8.875%). Applying it to money produces fractions of a cent, so that is done only
/// by the order's pricing method, which rounds.
/// </summary>
public readonly record struct Percentage
{
    /// <summary>Decimal places the database stores (numeric(7,4)); more would be lost silently on save.</summary>
    public const int MaxDecimalPlaces = 4;

    public decimal Value { get; }

    public Percentage(decimal value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A percentage cannot be negative.");
        }

        if (decimal.Round(value, MaxDecimalPlaces) != value)
        {
            throw new ArgumentException(
                $"A percentage can have at most {MaxDecimalPlaces} decimal places, got {value}.", nameof(value));
        }

        Value = value;
    }

    public static Percentage Zero => new(0m);

    public override string ToString() => $"{Value}%";
}
