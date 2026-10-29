using System.ComponentModel.DataAnnotations;

namespace Logistics.Backend.Model.Routes;

public sealed record class Route
{
    public Guid Id { get; init; }

    [Required]
    [StringLength(32, MinimumLength = 2)]
    [RegularExpression("^[A-Z0-9-]+$")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [EnumDataType(typeof(RouteStatus))]
    public RouteStatus Status { get; set; }

    [Range(0.0, double.MaxValue)]
    public double? EstimatedDistanceKm { get; set; }

    [Range(0, int.MaxValue)]
    public int? EstimatedDurationMinutes { get; set; }

    [Required]
    [MinLength(2)]
    public List<RouteWaypoint> Waypoints { get; set; } = [];

    public DateTimeOffset CreatedAt { get; init; }

    [ConcurrencyCheck]
    public DateTimeOffset UpdatedAt { get; set; }
}
