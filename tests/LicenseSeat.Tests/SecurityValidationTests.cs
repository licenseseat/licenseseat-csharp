using System.Text.Json;

namespace LicenseSeat.Tests;

public class SecurityValidationTests
{
    [Fact]
    public void Utf8Length_IsStrictAndCountsBytes()
    {
        Assert.Equal(1, SecurityValidation.GetUtf8ByteCount("a"));
        Assert.Equal(4, SecurityValidation.GetUtf8ByteCount("😀"));
        Assert.Throws<ArgumentNullException>(() => SecurityValidation.GetUtf8ByteCount(null!));
        Assert.Throws<FormatException>(() => SecurityValidation.GetUtf8ByteCount("\ud800"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\u0085")]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    [InlineData("\u202e")]
    [InlineData("\u200b")]
    [InlineData("\ufdd0")]
    [InlineData("\uffff")]
    public void SafeText_RejectsUnsafeOrEmptyText(string? value)
    {
        Assert.False(SecurityValidation.IsSafeText(value, 1, 128));
    }

    [Fact]
    public void SafeText_AcceptsBoundedUnicodeScalarText()
    {
        Assert.True(SecurityValidation.IsSafeText("café 😀", 1, 32));
        Assert.False(SecurityValidation.IsSafeText("café", 1, 4));
        Assert.False(SecurityValidation.IsSafeText("ok", 3, 32));
        Assert.False(SecurityValidation.IsSafeText(new string('\ud800', 1), 1, 32));
        Assert.False(SecurityValidation.IsSafeText(new string('\udc00', 1), 1, 32));
    }

    [Theory]
    [InlineData("alpha-1_test.value", false, true)]
    [InlineData("kid:2026", false, false)]
    [InlineData("kid:2026", true, true)]
    [InlineData(".hidden", false, false)]
    [InlineData("bad/slash", false, false)]
    [InlineData("bad space", false, false)]
    [InlineData("badé", false, false)]
    public void SafeIdentifier_EnforcesItsRestrictedAlphabet(
        string value,
        bool allowColon,
        bool expected)
    {
        Assert.Equal(expected, SecurityValidation.IsSafeIdentifier(value, 1, 128, allowColon));
    }

    [Theory]
    [InlineData("!~", true)]
    [InlineData("contains space", false)]
    [InlineData("\u007f", false)]
    [InlineData("é", false)]
    public void AsciiPrintable_RejectsWhitespaceAndNonAscii(string value, bool expected)
    {
        Assert.Equal(expected, SecurityValidation.IsAsciiPrintable(value, 1, 128));
    }

    [Theory]
    [InlineData("plain", true)]
    [InlineData("percent%20encoded", true)]
    [InlineData("percent%2Fencoded", true)]
    [InlineData("trailing%", false)]
    [InlineData("short%2", false)]
    [InlineData("bad%xy", false)]
    public void PercentEncoding_RequiresCompleteHexOctets(string value, bool expected)
    {
        Assert.Equal(expected, SecurityValidation.HasValidPercentEncoding(value));
    }

    [Fact]
    public void StrictJson_EnforcesInputAndStructuralBounds()
    {
        Assert.Throws<ArgumentNullException>(() => SecurityValidation.ParseStrictJson(null!, 1024));
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson(string.Empty, 1024));
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson("{}", 1));
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson("{\"a\":1,}", 1024));
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson("{/*x*/\"a\":1}", 1024));
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson("{\"a\":1,\"a\":2}", 1024));

        var tooDeep = new string('[', SecurityValidation.MaxJsonDepth + 1) +
                      "0" +
                      new string(']', SecurityValidation.MaxJsonDepth + 1);
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson(tooDeep, 4096));

        var tooManyNodes = "[" + string.Join(",", Enumerable.Repeat("0", SecurityValidation.MaxJsonNodes)) + "]";
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson(tooManyNodes, 64 * 1024));

        var oversizedKey = "{\"" + new string('k', SecurityValidation.MaxJsonKeyBytes + 1) + "\":1}";
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson(oversizedKey, 4096));

        var oversizedText = JsonSerializer.Serialize(new string('x', SecurityValidation.MaxJsonStringBytes + 1));
        Assert.ThrowsAny<JsonException>(() => SecurityValidation.ParseStrictJson(oversizedText, 128 * 1024));
    }

    [Theory]
    [InlineData("{\"a\":1,\"b\":[true,null,\"x\"]}", "{\"b\":[true,null,\"x\"],\"a\":1}", true)]
    [InlineData("[1,2]", "[1,2]", true)]
    [InlineData("[1,2]", "[1,2,3]", false)]
    [InlineData("[1,2]", "[1,3]", false)]
    [InlineData("1", "1.0", false)]
    [InlineData("true", "false", false)]
    [InlineData("\"a\"", "\"b\"", false)]
    [InlineData("null", "null", true)]
    [InlineData("{}", "[]", false)]
    public void JsonElementEquality_IsStructuralAndExact(string leftJson, string rightJson, bool expected)
    {
        using var left = JsonDocument.Parse(leftJson);
        using var right = JsonDocument.Parse(rightJson);
        Assert.Equal(expected, SecurityValidation.JsonElementsEqual(left.RootElement, right.RootElement));
    }

    [Fact]
    public void JsonElementEquality_RejectsDuplicatePropertiesAndUndefinedValues()
    {
        using var duplicateRight = JsonDocument.Parse("{\"a\":1,\"a\":1}");
        using var ordinaryLeft = JsonDocument.Parse("{\"a\":1}");
        Assert.False(SecurityValidation.JsonElementsEqual(ordinaryLeft.RootElement, duplicateRight.RootElement));
        Assert.False(SecurityValidation.JsonElementsEqual(default, default));
    }

    [Theory]
    [InlineData("2026-08-09T20:30:00Z", "2026-08-09T20:30:00+00:00")]
    [InlineData("2026-08-09T20:30:00.1234567+01:30", "2026-08-09T19:00:00.1234567+00:00")]
    public void Rfc3339Parser_AcceptsExplicitOffsetsAndNormalizesUtc(string value, string expected)
    {
        Assert.True(SecurityValidation.TryParseRfc3339(value, out var timestamp));
        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), timestamp);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-08-09T20:30:00")]
    [InlineData("2026-08-09 20:30:00Z")]
    [InlineData("2026-08-09T20:30:00+0130")]
    [InlineData("2026-13-09T20:30:00Z")]
    public void Rfc3339Parser_RejectsAmbiguousOrInvalidTimestamps(string? value)
    {
        Assert.False(SecurityValidation.TryParseRfc3339(value, out _));
    }
}
