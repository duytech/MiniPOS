namespace Pos.Domain.Common;

/// <summary>
/// An amount of money in whole cents. Arithmetic that can produce fractions of a cent is not offered here:
/// rounding belongs to the order's pricing method.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    public Money(decimal amount, Currency currency)
    {
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException($"Money must be in whole cents, got {amount}.", nameof(amount));
        }

        // Normalize the scale (1.5m and 1.500m both become 1.50m) so equal amounts compare and print the same.
        Amount = decimal.Round(amount, 2) + 0.00m;
        Currency = currency;
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    public static Money Usd(decimal amount) => new(amount, Currency.USD);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int quantity) => new(money.Amount * quantity, money.Currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException($"Cannot combine {left.Currency} and {right.Currency}.");
        }
    }
}
