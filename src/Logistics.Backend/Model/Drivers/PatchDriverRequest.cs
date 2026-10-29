using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Drivers;

public sealed record class PatchDriverRequest
{
    [StringLength(80, MinimumLength = 1)]
    public string? FirstName { get; init; }

    [StringLength(80, MinimumLength = 1)]
    public string? LastName { get; init; }

    [EmailAddress]
    [StringLength(254)]
    public string? Email { get; init; }

    [RegularExpression(@"^\+?[1-9]\d{1,14}$")]
    public string? Phone { get; init; }

    [StringLength(32, MinimumLength = 4)]
    public string? LicenseNumber { get; init; }

    [EnumDataType(typeof(LicenseClass))]
    public LicenseClass? LicenseClass { get; init; }

    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$")]
    public string? LicenseExpiryDate { get; init; }

    [EnumDataType(typeof(DriverStatus))]
    public DriverStatus? Status { get; init; }
}
