using Logistics.Backend.Model.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Logistics.Backend.Infrastructure;

public class LogisticsContext(
    DbContextOptions<LogisticsContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.Vin).IsUnique();
            entity.HasIndex(v => v.LicensePlate).IsUnique();
            entity.HasIndex(v => new { v.CreatedAt, v.Id });
            entity.HasIndex(v => new { v.UpdatedAt, v.Id });

            entity.Property(v => v.CreatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
            entity.Property(v => v.UpdatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
        });
    }
}
