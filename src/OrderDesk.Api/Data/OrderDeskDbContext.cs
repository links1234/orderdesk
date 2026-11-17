using Microsoft.EntityFrameworkCore;
using OrderDesk.Api.Domain;

namespace OrderDesk.Api.Data;

public class OrderDeskDbContext(DbContextOptions<OrderDeskDbContext> options) : DbContext(options)
{
    public DbSet<ServiceOffering> Services => Set<ServiceOffering>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ServiceOffering>(entity =>
        {
            entity.Property(service => service.Name).HasMaxLength(120);
            entity.Property(service => service.Category).HasMaxLength(60);
            entity.Property(service => service.BasePrice).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(order => order.CustomerEmail).HasMaxLength(254);
            entity.Property(order => order.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(order => order.Subtotal).HasPrecision(10, 2);
            entity.Property(order => order.Discount).HasPrecision(10, 2);
            entity.Property(order => order.Tax).HasPrecision(10, 2);
            entity.Property(order => order.Total).HasPrecision(10, 2);
            entity.HasIndex(order => order.Status);
            entity.HasMany(order => order.Lines).WithOne().HasForeignKey(line => line.OrderId);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.Property(line => line.Name).HasMaxLength(120);
            entity.Property(line => line.UnitPrice).HasPrecision(10, 2);
        });
    }

    public static readonly ServiceOffering[] SeedServices =
    [
        new() { Id = 1, Name = "Rank coaching session", Category = "Coaching", BasePrice = 25.00m },
        new() { Id = 2, Name = "Replay review", Category = "Coaching", BasePrice = 15.00m },
        new() { Id = 3, Name = "Duo queue game", Category = "Play", BasePrice = 8.50m },
        new() { Id = 4, Name = "Placement match", Category = "Play", BasePrice = 12.00m },
        new() { Id = 5, Name = "Retired offering", Category = "Play", BasePrice = 5.00m, Active = false },
    ];

    public async Task SeedAsync()
    {
        await Database.EnsureCreatedAsync();
        if (!await Services.AnyAsync())
        {
            Services.AddRange(SeedServices.Select(seed => new ServiceOffering
            {
                Id = seed.Id, Name = seed.Name, Category = seed.Category,
                BasePrice = seed.BasePrice, Active = seed.Active,
            }));
            await SaveChangesAsync();
        }
    }
}
