namespace Pos.Application.Orders;

/// <summary>The <c>Pricing</c> configuration section.</summary>
public sealed class PricingOptions
{
    public const string SectionName = "Pricing";

    /// <summary>Sales tax rate in percent (8.875 means 8.875%), snapshotted onto each new order.</summary>
    public decimal TaxRatePercent { get; init; }
}
