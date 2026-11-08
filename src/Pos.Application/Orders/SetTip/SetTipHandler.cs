using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.SetTip;

public sealed class SetTipHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<SetTipCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(SetTipCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        if (OrderRules.OpenError(order) is { } notOpen)
        {
            return Result.Fail<OrderDto>(notOpen);
        }

        if (order.TipRate != command.Rate)
        {
            order.TipRate = command.Rate;
            order.SavedSplitParts.Clear();
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}
