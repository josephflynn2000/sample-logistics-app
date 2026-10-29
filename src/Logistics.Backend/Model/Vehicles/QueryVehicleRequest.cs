using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Vehicles;

public sealed record class QueryVehicleRequest
{
    [Range(1, 100)]
    public int? PageSize { get; init; }

    public string? PageToken { get; init; }

    public string? OrderBy { get; init; }

    public VehicleStatus? Status { get; init; }

    public VehicleType? Type { get; init; }

    [Range(0.0, double.MaxValue)]
    public double? MinCapacityKg { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }
}
