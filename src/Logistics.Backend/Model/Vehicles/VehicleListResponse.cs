namespace Logistics.Backend.Model.Vehicles;

public sealed record class VehicleListResponse
{
    public IReadOnlyList<Vehicle> Items { get; init; } = [];
    public string? NextPageToken { get; init; }
    public int TotalCount { get; init; }
}