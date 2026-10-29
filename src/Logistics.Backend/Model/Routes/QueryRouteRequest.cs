using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Backend.Model.Routes;

public sealed record class QueryRouteRequest
{
    [FromQuery(Name = "pageSize")]
    [Range(1, 100)]
    public int? PageSize { get; init; }

    [FromQuery(Name = "pageToken")]
    public string? PageToken { get; init; }

    [FromQuery(Name = "orderBy")]
    public string? OrderBy { get; init; }

    [FromQuery(Name = "status")]
    public RouteStatus? Status { get; init; }

    [FromQuery(Name = "search")]
    [StringLength(100)]
    public string? Search { get; init; }
}
