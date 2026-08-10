#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace LicenseSeat
{

    internal static class SecurityValidation
    {
        internal const int MaxJsonDepth = 20;
        internal const int MaxJsonNodes = 10_000;
        internal const int MaxJsonKeyBytes = 256;
        internal const int MaxJsonStringBytes = 64 * 1024;
        internal const int MaxCanonicalJsonBytes = 1024 * 1024;

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static int GetUtf8ByteCount(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            try
            {
                return StrictUtf8.GetByteCount(value);
            }
            catch (EncoderFallbackException ex)
            {
                throw new FormatException("Text contains invalid Unicode.", ex);
            }
        }

        internal static bool IsSafeText(string? value, int minBytes, int maxBytes)
        {
            if (value == null)
            {
                return false;
            }

            int byteCount;
            try
            {
                byteCount = GetUtf8ByteCount(value);
            }
            catch (FormatException)
            {
                return false;
            }

            if (byteCount < minBytes || byteCount > maxBytes)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var category = CharUnicodeInfo.GetUnicodeCategory(value, index);
                int codePoint;
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    {
                        return false;
                    }

                    codePoint = char.ConvertToUtf32(character, value[index + 1]);
                    index++;
                }
                else if (char.IsLowSurrogate(character))
                {
                    return false;
                }
                else
                {
                    codePoint = character;
                }

                // Keep server-controlled text safe for logs and UI. In
                // particular, reject bidi/zero-width format controls and line
                // separators that can visually rewrite or split log records.
                if (category == UnicodeCategory.Control ||
                    category == UnicodeCategory.Format ||
                    category == UnicodeCategory.LineSeparator ||
                    category == UnicodeCategory.ParagraphSeparator ||
                    category == UnicodeCategory.Surrogate ||
                    IsUnicodeNoncharacter(codePoint))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsSafeIdentifier(string? value, int minBytes, int maxBytes, bool allowColon = false)
        {
            if (!IsSafeText(value, minBytes, maxBytes))
            {
                return false;
            }

            for (var index = 0; index < value!.Length; index++)
            {
                var character = value[index];
                var allowed = (character >= 'A' && character <= 'Z') ||
                              (character >= 'a' && character <= 'z') ||
                              (character >= '0' && character <= '9') ||
                              character == '-' || character == '_' || character == '.' ||
                              (allowColon && character == ':');
                if (!allowed)
                {
                    return false;
                }
            }

            var first = value[0];
            return (first >= 'A' && first <= 'Z') ||
                   (first >= 'a' && first <= 'z') ||
                   (first >= '0' && first <= '9');
        }

        internal static bool IsAsciiPrintable(string? value, int minBytes, int maxBytes)
        {
            if (!IsSafeText(value, minBytes, maxBytes))
            {
                return false;
            }

            foreach (var character in value!)
            {
                if (character < '\u0021' || character > '\u007e')
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool HasValidPercentEncoding(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                if (value[index] != '%')
                {
                    continue;
                }

                if (index + 2 >= value.Length ||
                    !IsHex(value[index + 1]) ||
                    !IsHex(value[index + 2]))
                {
                    return false;
                }

                index += 2;
            }

            return true;
        }

        internal static JsonDocument ParseStrictJson(string json, int maxBytes)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var byteCount = GetUtf8ByteCount(json);
            if (byteCount == 0 || byteCount > maxBytes)
            {
                throw new JsonException($"JSON body must be between 1 and {maxBytes} bytes.");
            }

            var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaxJsonDepth
            });

            try
            {
                var nodes = 0;
                ValidateJsonElement(document.RootElement, 0, ref nodes);
                return document;
            }
            catch
            {
                document.Dispose();
                throw;
            }
        }

        internal static bool JsonElementsEqual(JsonElement left, JsonElement right)
        {
            if (left.ValueKind != right.ValueKind)
            {
                return false;
            }

            switch (left.ValueKind)
            {
                case JsonValueKind.Object:
                    var rightProperties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                    foreach (var property in right.EnumerateObject())
                    {
                        if (rightProperties.ContainsKey(property.Name))
                        {
                            return false;
                        }

                        rightProperties.Add(property.Name, property.Value);
                    }

                    var leftCount = 0;
                    foreach (var property in left.EnumerateObject())
                    {
                        leftCount++;
                        if (!rightProperties.TryGetValue(property.Name, out var rightValue) ||
                            !JsonElementsEqual(property.Value, rightValue))
                        {
                            return false;
                        }
                    }

                    return leftCount == rightProperties.Count;

                case JsonValueKind.Array:
                    var leftEnumerator = left.EnumerateArray();
                    var rightEnumerator = right.EnumerateArray();
                    while (true)
                    {
                        var hasLeft = leftEnumerator.MoveNext();
                        var hasRight = rightEnumerator.MoveNext();
                        if (hasLeft != hasRight)
                        {
                            return false;
                        }

                        if (!hasLeft)
                        {
                            return true;
                        }

                        if (!JsonElementsEqual(leftEnumerator.Current, rightEnumerator.Current))
                        {
                            return false;
                        }
                    }

                case JsonValueKind.String:
                    return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);

                case JsonValueKind.Number:
                    return string.Equals(left.GetRawText(), right.GetRawText(), StringComparison.Ordinal);

                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    return true;

                default:
                    return false;
            }
        }

        internal static bool TryParseRfc3339(string? value, out DateTimeOffset timestamp)
        {
            timestamp = default;
            if (!IsSafeText(value, 20, 40))
            {
                return false;
            }

            string normalized;
            if (value!.EndsWith("Z", StringComparison.Ordinal))
            {
                normalized = value.Substring(0, value.Length - 1) + "+00:00";
            }
            else
            {
                if (value.Length < 25 ||
                    (value[value.Length - 6] != '+' && value[value.Length - 6] != '-') ||
                    value[value.Length - 3] != ':')
                {
                    return false;
                }

                normalized = value;
            }

            var formats = new[]
            {
            "yyyy-MM-dd'T'HH:mm:sszzz",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
        };

            if (!DateTimeOffset.TryParseExact(
                    normalized,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out timestamp))
            {
                return false;
            }

            timestamp = timestamp.ToUniversalTime();
            return true;
        }

        private static void ValidateJsonElement(JsonElement element, int depth, ref int nodes)
        {
            if (depth > MaxJsonDepth || ++nodes > MaxJsonNodes)
            {
                throw new JsonException("JSON exceeds the supported structural limits.");
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var propertyNames = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var property in element.EnumerateObject())
                    {
                        if (!IsBoundedUnicode(property.Name, 0, MaxJsonKeyBytes))
                        {
                            throw new JsonException("JSON contains an invalid or oversized property name.");
                        }

                        if (!propertyNames.Add(property.Name))
                        {
                            throw new JsonException($"JSON contains duplicate property '{property.Name}'.");
                        }

                        ValidateJsonElement(property.Value, depth + 1, ref nodes);
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        ValidateJsonElement(item, depth + 1, ref nodes);
                    }

                    break;

                case JsonValueKind.String:
                    if (!IsBoundedUnicode(element.GetString(), 0, MaxJsonStringBytes))
                    {
                        throw new JsonException("JSON contains invalid or oversized text.");
                    }

                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    break;

                default:
                    throw new JsonException("JSON contains an unsupported value.");
            }
        }

        private static bool IsBoundedUnicode(string? value, int minBytes, int maxBytes)
        {
            if (value == null)
            {
                return false;
            }

            try
            {
                var bytes = GetUtf8ByteCount(value);
                return bytes >= minBytes && bytes <= maxBytes;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool IsHex(char value)
        {
            return (value >= '0' && value <= '9') ||
                   (value >= 'a' && value <= 'f') ||
                   (value >= 'A' && value <= 'F');
        }

        private static bool IsUnicodeNoncharacter(int codePoint)
        {
            return (codePoint >= 0xfdd0 && codePoint <= 0xfdef) ||
                   (codePoint & 0xffff) == 0xfffe ||
                   (codePoint & 0xffff) == 0xffff;
        }
    }
}
