using System.Text.Json.Serialization;

namespace Logistics.Backend.Model.Drivers;

[JsonConverter(typeof(JsonStringEnumConverter<LicenseClass>))]
public enum LicenseClass
{
    ClassA,
    ClassB,
    ClassC,
    Standard
}
