using System.Text.Json.Serialization;

namespace Logistics.Backend.Model.Routes;

[JsonConverter(typeof(JsonStringEnumConverter<RouteStatus>))]
public enum RouteStatus
{
    Draft,
    Active,
    Archived
}
