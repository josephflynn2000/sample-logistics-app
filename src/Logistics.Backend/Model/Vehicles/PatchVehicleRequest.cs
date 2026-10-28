using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Vehicles;

public sealed record class PatchVehicleRequest
{
    [StringLength(20, MinimumLength = 2)]
    public string? LicensePlate { get; init; }

    [StringLength(60, MinimumLength = 1)]
    public string? Make { get; init; }

    [StringLength(60, MinimumLength = 1)]
    public string? Model { get; init; }

    [Range(1990, 2100)]
    public int? Year { get; init; }

    [EnumDataType(typeof(VehicleType))]
    public VehicleType? Type { get; init; }

    [Range(1.0, double.MaxValue)]
    public double? CapacityKg { get; init; }

    [Range(0.1, double.MaxValue)]
    public double? CapacityVolumeM3 { get; init; }

    [EnumDataType(typeof(VehicleStatus))]
    public VehicleStatus? Status { get; init; }
}