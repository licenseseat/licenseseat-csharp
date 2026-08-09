using System.Text.Json;

namespace LicenseSeat.Tests;

public class SerializationBoundaryTests
{
    private static readonly JsonSerializerOptions IdentifierOptions = new JsonSerializerOptions
    {
        Converters = { new StringOrNumberJsonConverter() }
    };

    [Fact]
    public void DeviceIdentifier_IsDeterministicBoundedAndNonReversible()
    {
        var first = DeviceIdentifier.FromInput("stable-device-input");
        var second = DeviceIdentifier.FromInput("stable-device-input");
        var different = DeviceIdentifier.FromInput("different-device-input");

        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
        Assert.Matches("^[0-9a-f]{32}$", first);

        var generated = DeviceIdentifier.Generate();
        Assert.Matches("^[0-9a-f]{32}$", generated);
    }

    [Fact]
    public void DeviceIdentifier_RejectsInvalidOrUnboundedInput()
    {
        Assert.Throws<ArgumentException>(() => DeviceIdentifier.FromInput(null!));
        Assert.Throws<ArgumentException>(() => DeviceIdentifier.FromInput(string.Empty));
        Assert.Throws<ArgumentException>(() => DeviceIdentifier.FromInput("line\nbreak"));
        Assert.Throws<ArgumentException>(() => DeviceIdentifier.FromInput(new string('x', 4097)));
    }

    [Theory]
    [InlineData("\"abc-123\"", "abc-123")]
    [InlineData("123", "123")]
    [InlineData("9223372036854775807", "9223372036854775807")]
    [InlineData("null", null)]
    public void StringOrNumberIdentifierConverter_AcceptsOnlyCanonicalSupportedKinds(
        string json,
        string? expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<string?>(json, IdentifierOptions));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    [InlineData("true")]
    [InlineData("\"\"")]
    [InlineData("\"bad\\nvalue\"")]
    public void StringOrNumberIdentifierConverter_RejectsAmbiguousOrUnsafeValues(string json)
    {
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<string?>(json, IdentifierOptions));
    }

    [Fact]
    public void StringOrNumberIdentifierConverter_EnforcesByteLimitAndWritesStrings()
    {
        var oversized = JsonSerializer.Serialize(new string('x', 129));
        Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize<string?>(oversized, IdentifierOptions));

        Assert.Equal("\"123\"", JsonSerializer.Serialize<string?>("123", IdentifierOptions));
        Assert.Equal("null", JsonSerializer.Serialize<string?>(null, IdentifierOptions));
    }
}
