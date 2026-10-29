using System.Text.Json.Serialization;

namespace Logistics.Backend.Model.Drivers;

[JsonConverter(typeof(JsonStringEnumConverter<DriverStatus>))]
public enum DriverStatus
{
    Active,
    Inactive,
    OnLeave,
    Suspended
}
