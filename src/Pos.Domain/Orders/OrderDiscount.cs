using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>A discount on the whole order: either a percentage of the subtotal or a fixed amount. Exactly one is set.</summary>
public sealed record OrderDiscount
{
    public Percentage? Rate { get; }
    public Money? Amount { get; }

    private OrderDiscount(Percentage? rate, Money? amount)
    {
        Rate = rate;
        Amount = amount;
    }

    public static OrderDiscount PercentOfSubtotal(Percentage rate) => new(rate, null);

    public static OrderDiscount FixedAmount(Money amount)
    {
        if (amount.Amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "A discount cannot be negative.");
        }

        return new OrderDiscount(null, amount);
    }
}
