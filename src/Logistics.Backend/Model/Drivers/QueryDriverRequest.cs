using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Logistics.Backend.Model.Drivers;

public sealed record class QueryDriverRequest
{
    [FromQuery(Name = "pageSize")]
    [Range(1, 100)]
    public int? PageSize { get; init; }

    [FromQuery(Name = "pageToken")]
    public string? PageToken { get; init; }

    [FromQuery(Name = "orderBy")]
    public string? OrderBy { get; init; }

    [FromQuery(Name = "status")]
    public DriverStatus? Status { get; init; }

    [FromQuery(Name = "licenseClass")]
    public LicenseClass? LicenseClass { get; init; }

    [FromQuery(Name = "search")]
    [StringLength(100)]
    public string? Search { get; init; }
}
