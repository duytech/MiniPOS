using System.Globalization;

namespace Pos.Application.Orders.ListOrders;

/// <summary>
/// The search box text: orders with an item whose name contains <see cref="Text"/> (case-insensitive), or the order
/// whose number is <see cref="Number"/>.
/// </summary>
public sealed record OrderSearch(string Text, int? Number)
{
    /// <summary>Null for a blank search. "12" and "#12" both also match order number 12.</summary>
    public static OrderSearch? Parse(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var text = search.Trim();
        var digits = text.StartsWith('#') ? text[1..] : text;
        int? number = int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        return new OrderSearch(text, number);
    }
}
