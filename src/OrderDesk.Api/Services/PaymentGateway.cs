namespace OrderDesk.Api.Services;

public record PaymentResult(bool Approved, string? Reference, string? DeclineReason);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(Guid orderId, decimal amount, string cardToken, CancellationToken ct);
}

/// <summary>
/// Stand-in for a card processor. It approves every charge except the documented test token,
/// which lets the decline path be exercised end to end without a real payment account.
/// </summary>
public class SimulatedPaymentGateway : IPaymentGateway
{
    public const string DeclinedToken = "tok_declined";

    public Task<PaymentResult> ChargeAsync(Guid orderId, decimal amount, string cardToken, CancellationToken ct)
    {
        var result = cardToken == DeclinedToken
            ? new PaymentResult(false, null, "The card was declined.")
            : new PaymentResult(true, $"pay_{orderId:N}"[..20], null);
        return Task.FromResult(result);
    }
}
