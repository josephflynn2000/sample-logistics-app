using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Drivers;

public sealed record class Driver
{
    public Guid Id { get; init; }

    [Required]
    [StringLength(32, MinimumLength = 2)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(80, MinimumLength = 1)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\+?[1-9]\d{1,14}$")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(32, MinimumLength = 4)]
    public string LicenseNumber { get; set; } = string.Empty;

    [EnumDataType(typeof(LicenseClass))]
    public LicenseClass LicenseClass { get; set; }

    [Required]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$")]
    public string LicenseExpiryDate { get; set; } = string.Empty;

    [EnumDataType(typeof(DriverStatus))]
    public DriverStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    [ConcurrencyCheck]
    public DateTimeOffset UpdatedAt { get; set; }
}
