using Pos.Application.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.ApplyDiscount;

public sealed class ApplyDiscountHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyDiscountCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(ApplyDiscountCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.ApplyDiscount(command.Rate is { } rate
            ? OrderDiscount.PercentOfSubtotal(rate)
            : OrderDiscount.FixedAmount(command.Amount!.Value));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}
