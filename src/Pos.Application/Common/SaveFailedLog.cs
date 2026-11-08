using Microsoft.Extensions.Logging;
using Pos.Domain.Common;

namespace Pos.Application.Common;

/// <summary>Log messages for a <see cref="SaveFailedException"/> caught by a handler.</summary>
internal static partial class SaveFailedLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Saving order {OrderId} failed with {Code}")]
    public static partial void OrderSaveFailed(this ILogger logger, Exception exception, Guid orderId, ErrorCode code);

    [LoggerMessage(Level = LogLevel.Error, Message = "Saving menu item {MenuItemId} failed with {Code}")]
    public static partial void MenuItemSaveFailed(
        this ILogger logger, Exception exception, Guid menuItemId, ErrorCode code);
}
