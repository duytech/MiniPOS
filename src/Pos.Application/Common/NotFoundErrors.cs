using Pos.Domain.Common;

namespace Pos.Application.Common;

internal static class NotFoundErrors
{
    public static Error Order(Guid id) => new(ErrorCode.OrderNotFound, FormatMessage("Order", id));

    public static Error MenuItem(Guid id) => new(ErrorCode.MenuItemNotFound, FormatMessage("Menu item", id));

    public static Error OrderMenuItem(Guid id) => new(ErrorCode.OrderItemNotFound, FormatMessage("Order item", id));

    public static Error Split(Guid id) => new(ErrorCode.SplitNotFound, FormatMessage("Split", id));

    private static string FormatMessage(string resource, Guid id) => $"{resource} '{id}' was not found.";
}
