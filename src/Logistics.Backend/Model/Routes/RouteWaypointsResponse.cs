namespace Logistics.Backend.Model.Routes;

public sealed record class RouteWaypointsResponse
{
    public Guid RouteId { get; init; }
    public IReadOnlyList<RouteWaypoint> Waypoints { get; init; } = [];
}
