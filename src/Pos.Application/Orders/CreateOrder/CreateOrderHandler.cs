using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    PricingOptions pricing)
    : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Create(timeProvider.GetUtcNow(), new Percentage(pricing.TaxRatePercent),
            new Percentage(pricing.CardSurchargePercent));
        orders.Add(order);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}
