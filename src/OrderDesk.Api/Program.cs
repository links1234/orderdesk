using Microsoft.EntityFrameworkCore;
using OrderDesk.Api.Data;
using OrderDesk.Api.Endpoints;
using OrderDesk.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("OrderDesk");
builder.Services.AddDbContext<OrderDeskDbContext>(options =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlite("Data Source=orderdesk.db");
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});
builder.Services.AddSingleton<PricingService>();
builder.Services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<OrderDeskDbContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<OrderDeskDbContext>().SeedAsync();
}

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHealthChecks("/health");
app.MapOrderEndpoints();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
