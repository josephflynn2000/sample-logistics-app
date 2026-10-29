using System.Text.Json.Serialization;

namespace Logistics.Backend.Model.Routes;

[JsonConverter(typeof(JsonStringEnumConverter<StopType>))]
public enum StopType
{
    Origin,
    Pickup,
    Delivery,
    Waypoint,
    Destination
}
