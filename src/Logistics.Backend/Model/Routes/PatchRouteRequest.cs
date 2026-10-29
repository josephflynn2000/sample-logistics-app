using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Routes;

public sealed record class PatchRouteRequest
{
    [StringLength(32, MinimumLength = 2)]
    [RegularExpression("^[A-Z0-9-]+$")]
    public string? Code { get; init; }

    [StringLength(120, MinimumLength = 1)]
    public string? Name { get; init; }

    [StringLength(500)]
    public string? Description { get; init; }

    [EnumDataType(typeof(RouteStatus))]
    public RouteStatus? Status { get; init; }

    [Range(0.0, double.MaxValue)]
    public double? EstimatedDistanceKm { get; init; }

    [Range(0, int.MaxValue)]
    public int? EstimatedDurationMinutes { get; init; }

    [MinLength(2)]
    public List<RouteWaypoint>? Waypoints { get; init; }
}
