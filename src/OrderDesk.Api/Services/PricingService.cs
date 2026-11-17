namespace OrderDesk.Api.Services;

public record PricedLine(int ServiceOfferingId, string Name, int Quantity, decimal UnitPrice);

public record PriceBreakdown(decimal Subtotal, decimal Discount, decimal Tax, decimal Total);

/// <summary>Server-side pricing. The client never sends prices, only what it wants to buy.</summary>
public class PricingService
{
    public const decimal TaxRate = 0.13m;
    public const decimal BulkDiscountRate = 0.10m;
    public const int BulkDiscountMinimumUnits = 5;
    public const string WelcomeCode = "WELCOME10";

    public PriceBreakdown Price(IReadOnlyCollection<PricedLine> lines, string? promoCode)
    {
        var subtotal = lines.Sum(line => line.UnitPrice * line.Quantity);
        var units = lines.Sum(line => line.Quantity);
        var hasCode = string.Equals(promoCode?.Trim(), WelcomeCode, StringComparison.OrdinalIgnoreCase);

        // Discounts do not stack: a bulk order with a promo code still gets ten percent.
        var discount = units >= BulkDiscountMinimumUnits || hasCode ? Round(subtotal * BulkDiscountRate) : 0m;
        var tax = Round((subtotal - discount) * TaxRate);
        return new PriceBreakdown(Round(subtotal), discount, tax, Round(subtotal - discount + tax));
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
