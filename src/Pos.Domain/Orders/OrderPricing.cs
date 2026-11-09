using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>The only place order amounts are calculated.</summary>
public static class OrderPricing
{
    /// <summary>Unit price × quantity. Both are exact (whole cents × integer), so no rounding is involved.</summary>
    public static Money ItemTotal(OrderMenuItem item) => item.UnitPrice * item.Quantity;

    public static Money Subtotal(Order order) =>
        order.Items.Aggregate(Money.Zero(order.Currency), (sum, item) => sum + ItemTotal(item));

    /// <summary>The subtotal the order would have with <paramref name="item"/> at <paramref name="quantity"/> (0 = removed).</summary>
    public static Money SubtotalWithQuantity(Order order, OrderMenuItem item, int quantity) =>
        Subtotal(order) - ItemTotal(item) + item.UnitPrice * quantity;

    /// <summary>
    /// Each step is rounded to cents (MidpointRounding.AwayFromZero) before the next step uses it.
    /// 1. item total = unit price × quantity (exact cents)
    /// 2. subtotal = sum of item totals
    /// 3. discount = percentage of subtotal, or a fixed amount
    /// 4. surcharge = card surcharge rate × (subtotal − discount) when paying by card, otherwise zero
    /// 5. tax = tax rate × (subtotal − discount + surcharge), i.e. the surcharge is taxed
    /// 6. tip = tip rate × (subtotal − discount), i.e. before surcharge and tax
    /// total = subtotal − discount + surcharge + tax + tip
    /// </summary>
    public static OrderTotals CalculateTotals(Order order)
    {
        var subtotal = Subtotal(order);

        // At most one of the two is set: applying a discount checks it (ApplyDiscountValidator).
        var discount = (order.DiscountRate, order.DiscountAmount) switch
        {
            ({ } rate, _) => PercentOf(subtotal, rate),
            (_, { } amount) => amount,
            _ => Money.Zero(order.Currency),
        };
        var discountedSubtotal = subtotal - discount;

        var surchargeBase = discountedSubtotal;
        var surcharge = order.PaymentMethod == PaymentMethod.Card
            ? PercentOf(surchargeBase, order.CardSurchargeRate)
            : Money.Zero(order.Currency);

        var taxBase = discountedSubtotal + surcharge;
        var tax = PercentOf(taxBase, order.TaxRate);

        var tipBase = discountedSubtotal;
        var tip = order.TipRate is { } tipRate ? PercentOf(tipBase, tipRate) : Money.Zero(order.Currency);

        return new OrderTotals(subtotal, discount, surcharge, tax, tip,
            Total: discountedSubtotal + surcharge + tax + tip);
    }

    private static Money PercentOf(Money amount, Percentage rate) =>
        new(decimal.Round(amount.Amount * rate.Value / 100m, 2, MidpointRounding.AwayFromZero), amount.Currency);
}
