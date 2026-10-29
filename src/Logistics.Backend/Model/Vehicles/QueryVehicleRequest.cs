using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Vehicles;

public sealed record class QueryVehicleRequest
{
    public int? PageSize { get; init; }
    public string? PageToken { get; init; }

    [AllowedValues("createdAt", "updatedAt")]
    public string? OrderBy { get; init; }
    public VehicleStatus? Status { get; init; }
    public VehicleType? Type { get; init; }
    public double? MinCapacityKg { get; init; }
    public string? Search { get; init; }
}