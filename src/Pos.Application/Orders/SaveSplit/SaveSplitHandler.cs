using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Application.Orders.SaveSplit;

public sealed class SaveSplitHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<SaveSplitCommand, OrderSplitDto>
{
    public async Task<Result<OrderSplitDto>> HandleAsync(SaveSplitCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<OrderSplitDto>(NotFoundErrors.Order(command.OrderId));
        }

        var parts = order.SaveSplit(command.Parts!.Select(p => (IReadOnlyCollection<Guid>)p!).ToList());
        if (!parts.IsSuccess)
        {
            return Result.Fail<OrderSplitDto>(parts.Error);
        }

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderSplitDto.From(parts.Data)) : Result.Fail<OrderSplitDto>(saved.Error);
    }
}
