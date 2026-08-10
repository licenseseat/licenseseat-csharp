#nullable enable
using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LicenseSeat
{

    internal sealed class StringOrNumberJsonConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            string? value;
            if (reader.TokenType == JsonTokenType.String)
            {
                value = reader.GetString();
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                if (!reader.TryGetInt64(out var numericValue) || numericValue <= 0)
                {
                    throw new JsonException("Numeric identifiers must be positive 64-bit integers.");
                }

                value = numericValue.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                throw new JsonException("Expected a string or number identifier.");
            }

            if (!SecurityValidation.IsAsciiPrintable(value, 1, 128))
            {
                throw new JsonException("Identifier is invalid or too long.");
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value);
        }
    }
}
