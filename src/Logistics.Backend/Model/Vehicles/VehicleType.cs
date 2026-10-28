using System.Text.Json.Serialization;

namespace Logistics.Backend.Model.Vehicles;

[JsonConverter(typeof(JsonStringEnumConverter<VehicleType>))]
public enum VehicleType
{
    Van,
    BoxTruck,
    SemiTractor,
    RefrigeratedTruck,
    Flatbed
}