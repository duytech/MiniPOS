using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler(IOrderRepository orders, TimeProvider timeProvider, PricingOptions pricing,
    ILogger<CreateOrderHandler> logger)
    : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = OrderStatus.Open,
            CreatedAt = timeProvider.GetUtcNow().ToUniversalTime(),
            Currency = Currency.USD,
            TaxRate = new Percentage(pricing.TaxRatePercent),
            CardSurchargeRate = new Percentage(pricing.CardSurchargePercent),
            PaymentMethod = PaymentMethod.Cash,
        };
        try
        {
            await orders.CreateAsync(order, cancellationToken);
        }
        catch (SaveFailedException ex)
        {
            logger.OrderSaveFailed(ex, order.Id, ex.Code);
            return Result.Fail<OrderDto>(ex.ToError());
        }

        return Result.Ok(OrderDto.From(order));
    }
}
