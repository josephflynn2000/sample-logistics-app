using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Backend.Model.Vehicles;

public sealed record class QueryVehicleRequest
{
    [FromQuery(Name = "pageSize")]
    [Range(1, 100)]
    public int? PageSize { get; init; }

    [FromQuery(Name = "pageToken")]
    public string? PageToken { get; init; }

    [FromQuery(Name = "orderBy")]
    public string? OrderBy { get; init; }

    [FromQuery(Name = "status")]
    public VehicleStatus? Status { get; init; }

    [FromQuery(Name = "type")]
    public VehicleType? Type { get; init; }

    [FromQuery(Name = "minCapacityKg")]
    [Range(0.0, double.MaxValue)]
    public double? MinCapacityKg { get; init; }

    [FromQuery(Name = "search")]
    [StringLength(100)]
    public string? Search { get; init; }
}
