using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderDesk.Api.Data;
using OrderDesk.Api.Endpoints;
using OrderDesk.Api.Services;

namespace OrderDesk.Tests;

/// <summary>Runs the real application in memory against a throwaway SQLite database.</summary>
public class OrderDeskFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDeskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<OrderDeskDbContext>>();
            services.AddDbContext<OrderDeskDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}

public class OrderApiTests(OrderDeskFactory factory) : IClassFixture<OrderDeskFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<OrderResponse> CreateOrderAsync(int quantity = 2, string? promo = null)
    {
        var response = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest("buyer@example.com", [new OrderLineRequest(1, quantity)], promo));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
    }

    private async Task<HttpResponseMessage> PayAsync(Guid id, string key, string token = "tok_visa")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{id}/pay")
        {
            Content = JsonContent.Create(new PayOrderRequest(token)),
        };
        request.Headers.Add(OrderEndpoints.IdempotencyHeader, key);
        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Catalog_lists_only_active_services()
    {
        var services = await _client.GetFromJsonAsync<List<ServiceResponse>>("/api/services");
        Assert.Equal(4, services!.Count);
        Assert.DoesNotContain(services, service => service.Name == "Retired offering");
    }

    [Fact]
    public async Task Creating_an_order_prices_it_on_the_server()
    {
        var order = await CreateOrderAsync(quantity: 2);
        Assert.Equal("Pending", order.Status);
        Assert.Equal(50.00m, order.Subtotal);
        Assert.Equal(6.50m, order.Tax);
        Assert.Equal(56.50m, order.Total);

        var fetched = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}");
        Assert.Equal(order.Total, fetched!.Total);
        Assert.Single(fetched.Lines);
    }

    [Theory]
    [InlineData("not-an-email", 1, 1)]
    [InlineData("buyer@example.com", 1, 0)]
    [InlineData("buyer@example.com", 1, 21)]
    [InlineData("buyer@example.com", 5, 1)]
    [InlineData("buyer@example.com", 999, 1)]
    public async Task Rejects_invalid_orders(string email, int serviceId, int quantity)
    {
        var response = await _client.PostAsJsonAsync("/api/orders",
            new CreateOrderRequest(email, [new OrderLineRequest(serviceId, quantity)], null));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Paying_twice_with_the_same_key_charges_once()
    {
        var order = await CreateOrderAsync();
        var first = await (await PayAsync(order.Id, "key-1")).Content.ReadFromJsonAsync<OrderResponse>();
        var retry = await PayAsync(order.Id, "key-1");
        var second = await retry.Content.ReadFromJsonAsync<OrderResponse>();

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal("Paid", first!.Status);
        Assert.Equal(first.PaymentReference, second!.PaymentReference);

        var different = await PayAsync(order.Id, "key-2");
        Assert.Equal(HttpStatusCode.Conflict, different.StatusCode);
    }

    [Fact]
    public async Task Declined_card_leaves_the_order_unpaid()
    {
        var order = await CreateOrderAsync();
        var declined = await PayAsync(order.Id, "key-1", SimulatedPaymentGateway.DeclinedToken);
        Assert.Equal(HttpStatusCode.PaymentRequired, declined.StatusCode);

        var fetched = await _client.GetFromJsonAsync<OrderResponse>($"/api/orders/{order.Id}");
        Assert.Equal("Pending", fetched!.Status);
        Assert.Null(fetched.PaymentReference);
    }

    [Fact]
    public async Task Payment_requires_an_idempotency_key()
    {
        var order = await CreateOrderAsync();
        var response = await _client.PostAsJsonAsync($"/api/orders/{order.Id}/pay", new PayOrderRequest("tok_visa"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Order_moves_through_its_lifecycle_and_refuses_illegal_steps()
    {
        var order = await CreateOrderAsync();
        var skipAhead = await _client.PostAsync($"/api/orders/{order.Id}/status/Completed", null);
        Assert.Equal(HttpStatusCode.Conflict, skipAhead.StatusCode);

        await PayAsync(order.Id, "key-1");
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/orders/{order.Id}/status/InProgress", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/api/orders/{order.Id}/status/Completed", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsync($"/api/orders/{order.Id}/status/Cancelled", null)).StatusCode);

        var completed = await _client.GetFromJsonAsync<List<OrderResponse>>("/api/orders?status=completed");
        Assert.Contains(completed!, item => item.Id == order.Id);
    }

    [Fact]
    public async Task Unknown_order_returns_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/orders/{Guid.NewGuid()}")).StatusCode);
    }
}
