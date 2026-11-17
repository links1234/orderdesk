using OrderDesk.Api.Domain;

namespace OrderDesk.Api.Endpoints;

public record ServiceResponse(int Id, string Name, string Category, decimal Price);

public record OrderLineRequest(int ServiceId, int Quantity);

public record CreateOrderRequest(string CustomerEmail, List<OrderLineRequest> Lines, string? PromoCode);

public record PayOrderRequest(string CardToken);

public record OrderLineResponse(int ServiceId, string Name, int Quantity, decimal UnitPrice);

public record OrderResponse(
    Guid Id,
    string CustomerEmail,
    string Status,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal Total,
    DateTime CreatedAtUtc,
    string? PaymentReference,
    List<OrderLineResponse> Lines)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.CustomerEmail,
        order.Status.ToString(),
        order.Subtotal,
        order.Discount,
        order.Tax,
        order.Total,
        order.CreatedAtUtc,
        order.PaymentReference,
        order.Lines.Select(line => new OrderLineResponse(line.ServiceOfferingId, line.Name, line.Quantity, line.UnitPrice)).ToList());
}
