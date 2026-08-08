using System.Text.Json;
using System.Text.Json.Serialization;

namespace Medinilla.Infrastructure;

public static class OcppPayloadSerializer
{
    public static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string SerializePayload<T>(T payload) => JsonSerializer.Serialize(payload, PayloadJsonOptions);
}
