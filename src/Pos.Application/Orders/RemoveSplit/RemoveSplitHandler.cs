using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.RemoveSplit;

/// <summary>
/// Checks in this order: missing order, no saved split, order not open. Returns true once the split is removed.
/// </summary>
public sealed class RemoveSplitHandler(IOrderRepository orders, ILogger<RemoveSplitHandler> logger)
    : ICommandHandler<RemoveSplitCommand, bool?>
{
    public async Task<Result<bool?>> HandleAsync(RemoveSplitCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<bool?>(NotFoundErrors.Order(command.OrderId));
        }

        if (order.SavedSplitParts.Count == 0)
        {
            return Result.Fail<bool?>(NotFoundErrors.Split(command.OrderId));
        }

        if (!OrderRules.IsOpen(order))
        {
            return Result.Fail<bool?>(OrderErrors.NotOpen(order.Status));
        }

        order.SavedSplitParts.Clear();

        try
        {
            await orders.SaveAsync(cancellationToken);
        }
        catch (SaveFailedException ex)
        {
            logger.OrderSaveFailed(ex, order.Id, ex.Code);
            return Result.Fail<bool?>(ex.ToError());
        }

        return Result.Ok<bool?>(true);
    }
}
