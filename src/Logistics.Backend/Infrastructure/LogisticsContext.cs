using Logistics.Backend.Model.Vehicles;

using Microsoft.EntityFrameworkCore;

namespace Logistics.Backend.Infrastructure;

public class LogisticsContext(
    DbContextOptions<LogisticsContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles { get; set; } = null!;
}