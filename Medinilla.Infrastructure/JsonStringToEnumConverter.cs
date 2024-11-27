using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Medinilla.Infrastructure;

public class JsonStringToEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(
        Type type,
        JsonSerializerOptions options)
    {
        JsonConverter converter = (JsonConverter)Activator.CreateInstance(
            typeof(JsonStringToEnumConverterInner<>).MakeGenericType(
                [type]),
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            args: [options],
            culture: null)!;

        return converter;
    }

    private class JsonStringToEnumConverterInner<T> : JsonConverter<T> where T : struct, Enum
    {
        private readonly bool _isCaseSensitive;

        public JsonStringToEnumConverterInner()
        {
        }

        public JsonStringToEnumConverterInner(bool isCaseSensitive = false)
        {
            _isCaseSensitive = isCaseSensitive;
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            string value = reader.GetString();

            if (string.IsNullOrEmpty(value))
            {
                throw new JsonException($"Cannot convert null or empty string to enum {typeToConvert.Name}");
            }

            try
            {
                // Try to parse the enum value
                if (Enum.TryParse<T>(value, !_isCaseSensitive, out T result))
                {
                    return result;
                }

                throw new JsonException($"Value '{value}' is not valid for enum {typeToConvert.Name}");
            }
            catch (ArgumentException ex)
            {
                throw new JsonException($"Error converting value '{value}' to enum {typeToConvert.Name}: {ex.Message}");
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}