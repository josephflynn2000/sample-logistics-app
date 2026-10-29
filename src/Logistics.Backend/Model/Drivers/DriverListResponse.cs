namespace Logistics.Backend.Model.Drivers;

public sealed record class DriverListResponse
{
    public IReadOnlyList<Driver> Items { get; init; } = [];
    public string? NextPageToken { get; init; }
    public int TotalCount { get; init; }
}
