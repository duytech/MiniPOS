using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>The only place order amounts are calculated.</summary>
public static class OrderPricing
{
    /// <summary>Unit price × quantity. Both are exact (whole cents × integer), so no rounding is involved.</summary>
    public static Money LineTotal(OrderLine line) => line.UnitPrice * line.Quantity;

    public static Money Subtotal(Order order) =>
        order.Lines.Aggregate(Money.Zero(order.Currency), (sum, line) => sum + LineTotal(line));

    /// <summary>The subtotal the order would have with <paramref name="line"/> at <paramref name="quantity"/> (0 = removed).</summary>
    public static Money SubtotalWithQuantity(Order order, OrderLine line, int quantity) =>
        Subtotal(order) - LineTotal(line) + line.UnitPrice * quantity;

    /// <summary>The order's discount, built from its two stored columns; null when it has none.</summary>
    public static OrderDiscount? Discount(Order order) => (order.DiscountRate, order.DiscountAmount) switch
    {
        ({ } rate, null) => OrderDiscount.PercentOfSubtotal(rate),
        (null, { } amount) => OrderDiscount.FixedAmount(amount),
        _ => null,
    };

    /// <summary>
    /// Each step is rounded to cents (MidpointRounding.AwayFromZero) before the next step uses it.
    /// 1. line total = unit price × quantity (exact cents)
    /// 2. subtotal = sum of line totals
    /// 3. discount = percentage of subtotal, or a fixed amount
    /// 4. surcharge = card surcharge rate × (subtotal − discount) when paying by card, otherwise zero
    /// 5. tax = tax rate × (subtotal − discount + surcharge), i.e. the surcharge is taxed
    /// 6. tip = tip rate × (subtotal − discount), i.e. before surcharge and tax
    /// total = subtotal − discount + surcharge + tax + tip
    /// </summary>
    public static OrderTotals CalculateTotals(Order order)
    {
        var subtotal = Subtotal(order);

        var discount = Discount(order) switch
        {
            { Rate: { } rate } => PercentOf(subtotal, rate),
            { Amount: { } amount } => amount,
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
