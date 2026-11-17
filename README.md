# OrderDesk

A small online ordering system: a customer picks services, places an order and pays with a test card.

- **API**: C# on ASP.NET Core 9 with Entity Framework Core and PostgreSQL.
  Prices are calculated on the server from the stored catalog, order status changes go through one state machine, and a payment retried with the same `Idempotency-Key` is charged once.
- **Frontend**: Angular 19 and TypeScript, served by the API.
- **Tests**: 23 xUnit tests (pricing, status rules, and the HTTP API run in memory) and 5 Playwright browser tests (checkout, promo code, declined card, validation).
- **Payments** are simulated; no real card processor is called.

## Run

```bash
docker compose up -d --build        # http://127.0.0.1:8090
docker run --rm -v "$PWD":/work -w /work mcr.microsoft.com/dotnet/sdk:9.0 dotnet test
cd e2e && npm install && npx playwright test
```

Not deployed to a cloud host yet.
