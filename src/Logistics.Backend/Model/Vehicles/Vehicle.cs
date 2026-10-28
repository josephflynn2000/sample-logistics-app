using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Vehicles;

public sealed record class Vehicle
{
    public Guid Id { get; init; }

    [Required]
    [StringLength(17, MinimumLength = 17)]
    [RegularExpression("^[A-HJ-NPR-Z0-9]{17}$")]
    public string Vin { get; init; } = string.Empty;

    [Required]
    [StringLength(20, MinimumLength = 2)]
    public string LicensePlate { get; init; } = string.Empty;

    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string Make { get; init; } = string.Empty;

    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string Model { get; init; } = string.Empty;

    [Range(1990, 2100)]
    public int Year { get; init; }

    [EnumDataType(typeof(VehicleType))]
    public VehicleType Type { get; init; }

    [Range(1.0, double.MaxValue)]
    public double CapacityKg { get; init; }

    [Range(0.1, double.MaxValue)]
    public double CapacityVolumeM3 { get; init; }

    [EnumDataType(typeof(VehicleStatus))]
    public VehicleStatus Status { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}