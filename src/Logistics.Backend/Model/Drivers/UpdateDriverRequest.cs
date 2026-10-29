using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Drivers;

public sealed record class UpdateDriverRequest
{
    [Required]
    [StringLength(32, MinimumLength = 2)]
    public string EmployeeNumber { get; init; } = string.Empty;

    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^\+?[1-9]\d{1,14}$")]
    public string Phone { get; init; } = string.Empty;

    [Required]
    [StringLength(32, MinimumLength = 4)]
    public string LicenseNumber { get; init; } = string.Empty;

    [EnumDataType(typeof(LicenseClass))]
    public LicenseClass LicenseClass { get; init; }

    [Required]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$")]
    public string LicenseExpiryDate { get; init; } = string.Empty;

    [EnumDataType(typeof(DriverStatus))]
    public DriverStatus Status { get; init; }
}
