using OrderDesk.Api.Domain;
using OrderDesk.Api.Services;

namespace OrderDesk.Tests;

public class PricingAndStateTests
{
    private readonly PricingService _pricing = new();

    [Fact]
    public void Prices_a_plain_order_with_tax()
    {
        var result = _pricing.Price([new PricedLine(1, "Coaching", 2, 25.00m)], promoCode: null);
        Assert.Equal(new PriceBreakdown(50.00m, 0m, 6.50m, 56.50m), result);
    }

    [Theory]
    [InlineData(5, null)]
    [InlineData(1, "welcome10")]
    [InlineData(6, "WELCOME10")]
    public void Applies_one_ten_percent_discount_for_bulk_or_promo(int quantity, string? code)
    {
        var result = _pricing.Price([new PricedLine(3, "Duo queue game", quantity, 8.50m)], code);
        var subtotal = 8.50m * quantity;
        Assert.Equal(Math.Round(subtotal * 0.10m, 2, MidpointRounding.AwayFromZero), result.Discount);
        Assert.Equal(result.Subtotal - result.Discount + result.Tax, result.Total);
    }

    [Fact]
    public void Ignores_unknown_promo_codes()
    {
        Assert.Equal(0m, _pricing.Price([new PricedLine(1, "Coaching", 1, 25.00m)], "FREE").Discount);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Completed, false)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Completed, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid, false)]
    public void Allows_only_legal_status_changes(OrderStatus from, OrderStatus to, bool allowed)
    {
        Assert.Equal(allowed, OrderStateMachine.CanMove(from, to));
        var order = new Order { CustomerEmail = "a@example.com", Status = from };
        if (allowed)
        {
            OrderStateMachine.Move(order, to);
            Assert.Equal(to, order.Status);
        }
        else
        {
            Assert.Throws<InvalidOrderTransitionException>(() => OrderStateMachine.Move(order, to));
        }
    }
}
