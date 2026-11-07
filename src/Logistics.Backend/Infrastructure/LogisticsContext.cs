using Logistics.Backend.Model.Drivers;
using Logistics.Backend.Model.Routes;
using Logistics.Backend.Model.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using RouteEntity = Logistics.Backend.Model.Routes.Route;

namespace Logistics.Backend.Infrastructure;

public class LogisticsContext(
    DbContextOptions<LogisticsContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles { get; set; } = null!;
    public DbSet<Driver> Drivers { get; set; } = null!;
    public DbSet<RouteEntity> Routes { get; set; } = null!;

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
            entity.HasIndex(v => new { v.Status, v.CreatedAt, v.Id });
            entity.HasIndex(v => new { v.Type, v.CreatedAt, v.Id });
            entity.HasIndex(v => v.CapacityKg);

            entity.Property(v => v.CreatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
            entity.Property(v => v.UpdatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
        });

        modelBuilder.Entity<Driver>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.EmployeeNumber).IsUnique();
            entity.HasIndex(d => d.Email).IsUnique();
            entity.HasIndex(d => d.LicenseNumber).IsUnique();
            entity.HasIndex(d => new { d.CreatedAt, d.Id });
            entity.HasIndex(d => new { d.UpdatedAt, d.Id });
            entity.HasIndex(d => new { d.Status, d.CreatedAt, d.Id });
            entity.HasIndex(d => new { d.LicenseClass, d.CreatedAt, d.Id });

            entity.Property(d => d.CreatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
            entity.Property(d => d.UpdatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
        });

        modelBuilder.Entity<RouteEntity>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Code).IsUnique();
            entity.HasIndex(r => new { r.CreatedAt, r.Id });
            entity.HasIndex(r => new { r.UpdatedAt, r.Id });
            entity.HasIndex(r => new { r.Status, r.CreatedAt, r.Id });

            entity.Property(r => r.CreatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
            entity.Property(r => r.UpdatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());

            entity.OwnsMany(r => r.Waypoints, wp =>
            {
                wp.ToTable("RouteStops");
                wp.WithOwner().HasForeignKey("RouteId");
                wp.HasKey("RouteId", nameof(RouteWaypoint.SequenceNumber));
            });
        });
    }
}
