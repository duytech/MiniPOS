using Pos.Domain.Common;

namespace Pos.Application.Common;

internal static class NotFoundErrors
{
    public static Error Order(Guid id) => new(ErrorCode.OrderNotFound, Message("Order", id));

    public static Error MenuItem(Guid id) => new(ErrorCode.MenuItemNotFound, Message("Menu item", id));

    public static Error OrderLine(Guid id) => new(ErrorCode.OrderLineNotFound, Message("Order line", id));

    public static Error Split(Guid id) => new(ErrorCode.SplitNotFound, Message("Split", id));

    private static string Message(string resource, Guid id) => $"{resource} '{id}' was not found.";
}
