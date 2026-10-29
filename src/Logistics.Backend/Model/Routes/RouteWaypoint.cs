using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Routes;

public sealed record class RouteWaypoint
{
    [Range(1, int.MaxValue)]
    public int SequenceNumber { get; set; }

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Label { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Address { get; set; }

    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }

    [EnumDataType(typeof(StopType))]
    public StopType StopType { get; set; }

    [Range(0, int.MaxValue)]
    public int DwellTimeMinutes { get; set; }
}
