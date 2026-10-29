namespace Logistics.Backend.Model.Routes;

public sealed record class RouteListResponse
{
    public IReadOnlyList<Route> Items { get; init; } = [];
    public string? NextPageToken { get; init; }
    public int TotalCount { get; init; }
}
