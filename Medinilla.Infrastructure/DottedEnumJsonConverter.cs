using System.Text.Json;
using System.Text.Json.Serialization;

namespace Medinilla.Infrastructure;

/// <summary>
/// A custom JsonConverter attribute for converting between dotted string representations and enum values.
/// For example, converts "Transaction.Begin" to Enum.TransactionBegin.
/// </summary>
[AttributeUsage(AttributeTargets.Enum)]
public class DottedEnumJsonConverterAttribute : JsonConverterAttribute
{
    public DottedEnumJsonConverterAttribute() : base(typeof(DottedEnumJsonConverter))
    {
    }
}

/// <summary>
/// The converter implementation that handles the actual conversion logic.
/// </summary>
public class DottedEnumJsonConverter : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type converterType = typeof(DottedEnumJsonConverterInner<>).MakeGenericType(typeToConvert);
        return (JsonConverter)Activator.CreateInstance(converterType);
    }

    private class DottedEnumJsonConverterInner<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Expected string value for converting to {typeToConvert.Name}.");
            }

            string dottedValue = reader.GetString();

            if (string.IsNullOrEmpty(dottedValue))
            {
                throw new JsonException("Enum string value cannot be null or empty.");
            }

            // Convert "Transaction.Begin" to "TransactionBegin"
            string enumValueName = dottedValue.Replace(".", string.Empty)
                                              .Replace("-", string.Empty);

            if (Enum.TryParse<TEnum>(enumValueName, out var result))
            {
                return result;
            }

            throw new JsonException($"Cannot convert value '{dottedValue}' to enum type {typeToConvert.Name}.");
        }

        public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
        {
            string enumString = value.ToString();

            // Convert "TransactionBegin" to "Transaction.Begin"
            // Look for capital letters after the first character and insert dots before them
            char[] chars = enumString.ToCharArray();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            sb.Append(chars[0]);
            for (int i = 1; i < chars.Length; i++)
            {
                if (char.IsUpper(chars[i]))
                {
                    sb.Append('.');
                }
                sb.Append(chars[i]);
            }

            writer.WriteStringValue(sb.ToString());
        }
    }
}