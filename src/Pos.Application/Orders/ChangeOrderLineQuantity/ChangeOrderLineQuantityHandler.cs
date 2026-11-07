using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Application.Orders.ChangeOrderLineQuantity;

public sealed class ChangeOrderLineQuantityHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<ChangeOrderLineQuantityCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(ChangeOrderLineQuantityCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.Order(command.OrderId));
        }

        var line = order.Lines.FirstOrDefault(l => l.Id == command.LineId);
        if (line is null)
        {
            return Result.Fail<OrderDto>(NotFoundErrors.OrderLine(command.LineId));
        }

        var changed = order.ChangeLineQuantity(line, command.Quantity);
        if (!changed.IsSuccess)
        {
            return Result.Fail<OrderDto>(changed.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}
