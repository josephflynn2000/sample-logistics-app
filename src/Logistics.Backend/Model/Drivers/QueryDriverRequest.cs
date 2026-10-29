using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Drivers;

public sealed record class QueryDriverRequest
{
    [Range(1, 100)]
    public int? PageSize { get; init; }

    public string? PageToken { get; init; }

    public string? OrderBy { get; init; }

    public DriverStatus? Status { get; init; }

    public LicenseClass? LicenseClass { get; init; }

    [StringLength(100)]
    public string? Search { get; init; }
}
