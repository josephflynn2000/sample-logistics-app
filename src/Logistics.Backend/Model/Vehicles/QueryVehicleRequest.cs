using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Backend.Model.Vehicles;

public sealed record class QueryVehicleRequest
{
    [FromQuery(Name = "pageSize")]
    [Range(1, 100)]
    [Description("Maximum number of items to return per page (default 25, max 100).")]
    public int? PageSize { get; init; }

    [FromQuery(Name = "pageToken")]
    [Description("Opaque continuation token returned from a previous list call (`nextPageToken`) to fetch the next page of results.")]
    public string? PageToken { get; init; }

    [FromQuery(Name = "orderBy")]
    [Description("Sort expression (e.g., `createdAt desc`, `name asc`).")]
    public string? OrderBy { get; init; }

    [FromQuery(Name = "status")]
    [Description("Filter by operational status.")]
    public VehicleStatus? Status { get; init; }

    [FromQuery(Name = "type")]
    [Description("Filter by vehicle type classification.")]
    public VehicleType? Type { get; init; }

    [FromQuery(Name = "minCapacityKg")]
    [Range(0.0, double.MaxValue)]
    [Description("Filter vehicles with payload capacity greater than or equal to this value in kilograms.")]
    public double? MinCapacityKg { get; init; }

    [FromQuery(Name = "search")]
    [StringLength(100)]
    [Description("Case-insensitive free-text filter applied across key identifying fields.")]
    public string? Search { get; init; }
}