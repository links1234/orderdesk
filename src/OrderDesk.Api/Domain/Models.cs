namespace OrderDesk.Api.Domain;

public enum OrderStatus
{
    Pending,
    Paid,
    InProgress,
    Completed,
    Cancelled,
}

public class ServiceOffering
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
    public decimal BasePrice { get; set; }
    public bool Active { get; set; } = true;
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CustomerEmail { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? PaymentReference { get; set; }
    public string? PaymentIdempotencyKey { get; set; }
}

public class OrderLine
{
    public int Id { get; set; }
    public Guid OrderId { get; set; }
    public int ServiceOfferingId { get; set; }
    public required string Name { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
