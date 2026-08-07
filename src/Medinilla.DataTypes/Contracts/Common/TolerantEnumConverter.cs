using System.Text.Json;
using System.Text.Json.Serialization;

namespace Medinilla.DataTypes.Contracts.Common;

public sealed class TolerantEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return default;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var i) && Enum.IsDefined(typeof(TEnum), i))
            {
                return (TEnum)(object)i;
            }
            return default;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString();
            if (!string.IsNullOrEmpty(s)
                && Enum.TryParse<TEnum>(s, ignoreCase: true, out var v)
                && Enum.IsDefined(typeof(TEnum), v))
            {
                return v;
            }
            return default;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
