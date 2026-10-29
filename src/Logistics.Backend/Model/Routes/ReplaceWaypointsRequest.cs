using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Routes;

public sealed record class ReplaceWaypointsRequest
{
    [Required]
    [MinLength(2)]
    public List<RouteWaypoint> Waypoints { get; init; } = [];
}
