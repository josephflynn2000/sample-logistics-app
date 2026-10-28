using System.Text.Json.Serialization;

namespace Logistics.Backend.Model.Vehicles;

[JsonConverter(typeof(JsonStringEnumConverter<VehicleStatus>))]
public enum VehicleStatus
{
    Available,
    InMaintenance,
    OutOfService,
    Retired
}