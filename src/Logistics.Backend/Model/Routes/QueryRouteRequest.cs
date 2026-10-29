using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Routes;

public sealed record class QueryRouteRequest
{
    [Range(1, 100)]
    public int? PageSize { get; init; }

    public string? PageToken { get; init; }

    public string? OrderBy { get; init; }

    public RouteStatus? Status { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }
}
