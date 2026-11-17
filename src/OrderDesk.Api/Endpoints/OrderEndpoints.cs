using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderDesk.Api.Data;
using OrderDesk.Api.Domain;
using OrderDesk.Api.Services;

namespace OrderDesk.Api.Endpoints;

public static class OrderEndpoints
{
    public const string IdempotencyHeader = "Idempotency-Key";

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/services", async (OrderDeskDbContext db) =>
            await db.Services.Where(service => service.Active).OrderBy(service => service.Id)
                .Select(service => new ServiceResponse(service.Id, service.Name, service.Category, service.BasePrice))
                .ToListAsync());

        api.MapPost("/orders", CreateOrder);
        api.MapGet("/orders/{id:guid}", GetOrder);
        api.MapGet("/orders", ListOrders);
        api.MapPost("/orders/{id:guid}/pay", PayOrder);
        api.MapPost("/orders/{id:guid}/status/{status}", ChangeStatus);
    }

    private static async Task<IResult> CreateOrder(CreateOrderRequest request, OrderDeskDbContext db, PricingService pricing)
    {
        var errors = new Dictionary<string, string[]>();
        if (!MailAddress.TryCreate(request.CustomerEmail, out _))
        {
            errors["customerEmail"] = ["Enter a valid email address."];
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            errors["lines"] = ["Add at least one service."];
        }
        else if (request.Lines.Any(line => line.Quantity is < 1 or > 20))
        {
            errors["lines"] = ["Each quantity must be between 1 and 20."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var ids = request.Lines!.Select(line => line.ServiceId).Distinct().ToList();
        var services = await db.Services.Where(service => ids.Contains(service.Id) && service.Active)
            .ToDictionaryAsync(service => service.Id);
        if (services.Count != ids.Count)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["lines"] = ["One or more services are not available."],
            });
        }

        // Merge repeated services, then price on the server from the stored catalog.
        var priced = request.Lines!.GroupBy(line => line.ServiceId)
            .Select(group => new PricedLine(group.Key, services[group.Key].Name, group.Sum(line => line.Quantity), services[group.Key].BasePrice))
            .ToList();
        var breakdown = pricing.Price(priced, request.PromoCode);

        var order = new Order
        {
            CustomerEmail = request.CustomerEmail.Trim(),
            Subtotal = breakdown.Subtotal,
            Discount = breakdown.Discount,
            Tax = breakdown.Tax,
            Total = breakdown.Total,
            Lines = priced.Select(line => new OrderLine
            {
                ServiceOfferingId = line.ServiceOfferingId, Name = line.Name,
                Quantity = line.Quantity, UnitPrice = line.UnitPrice,
            }).ToList(),
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return Results.Created($"/api/orders/{order.Id}", OrderResponse.From(order));
    }

    private static async Task<IResult> GetOrder(Guid id, OrderDeskDbContext db)
    {
        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id);
        return order is null ? Results.NotFound() : Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> ListOrders(string? status, OrderDeskDbContext db)
    {
        var query = db.Orders.Include(order => order.Lines).AsQueryable();
        if (status is not null)
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown status."] });
            }

            query = query.Where(order => order.Status == parsed);
        }

        var orders = await query.OrderByDescending(order => order.CreatedAtUtc).Take(50).ToListAsync();
        return Results.Ok(orders.Select(OrderResponse.From));
    }

    private static async Task<IResult> PayOrder(
        Guid id,
        PayOrderRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        OrderDeskDbContext db,
        IPaymentGateway gateway,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Results.Problem($"The {IdempotencyHeader} header is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);
        if (order is null)
        {
            return Results.NotFound();
        }

        // A retry of the same request (same key) returns the first result instead of charging twice.
        if (order.Status == OrderStatus.Paid && order.PaymentIdempotencyKey == idempotencyKey)
        {
            return Results.Ok(OrderResponse.From(order));
        }

        if (!OrderStateMachine.CanMove(order.Status, OrderStatus.Paid))
        {
            return Results.Problem($"An order that is {order.Status} cannot be paid.", statusCode: StatusCodes.Status409Conflict);
        }

        var payment = await gateway.ChargeAsync(order.Id, order.Total, request.CardToken, ct);
        if (!payment.Approved)
        {
            return Results.Problem(payment.DeclineReason, statusCode: StatusCodes.Status402PaymentRequired);
        }

        OrderStateMachine.Move(order, OrderStatus.Paid);
        order.PaymentReference = payment.Reference;
        order.PaymentIdempotencyKey = idempotencyKey;
        await db.SaveChangesAsync(ct);
        return Results.Ok(OrderResponse.From(order));
    }

    private static async Task<IResult> ChangeStatus(Guid id, string status, OrderDeskDbContext db)
    {
        if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var target) || target == OrderStatus.Paid)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["status"] = ["Unknown or unsupported status."] });
        }

        var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id);
        if (order is null)
        {
            return Results.NotFound();
        }

        try
        {
            OrderStateMachine.Move(order, target);
        }
        catch (InvalidOrderTransitionException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status409Conflict);
        }

        await db.SaveChangesAsync();
        return Results.Ok(OrderResponse.From(order));
    }
}
