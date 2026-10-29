using System.Text.Json;

using Logistics.Backend.Model.Drivers;
using Logistics.Backend.Model.Routes;
using Logistics.Backend.Model.Vehicles;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

            entity.Property(d => d.CreatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
            entity.Property(d => d.UpdatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
        });

        ValueConverter<List<RouteWaypoint>, string> waypointsConverter = new(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<RouteWaypoint>>(v, (JsonSerializerOptions?)null) ?? new List<RouteWaypoint>());

        ValueComparer<List<RouteWaypoint>> waypointsComparer = new(
            (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        modelBuilder.Entity<RouteEntity>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasIndex(r => r.Code).IsUnique();
            entity.HasIndex(r => new { r.CreatedAt, r.Id });
            entity.HasIndex(r => new { r.UpdatedAt, r.Id });

            entity.Property(r => r.Waypoints)
                .HasConversion(waypointsConverter, waypointsComparer);

            entity.Property(r => r.CreatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
            entity.Property(r => r.UpdatedAt)
                .HasConversion(new DateTimeOffsetToBinaryConverter());
        });
    }
}
