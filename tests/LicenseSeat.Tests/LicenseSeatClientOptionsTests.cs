using System;

namespace LicenseSeat.Tests;

public class LicenseSeatClientOptionsTests
{
    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        var options = new LicenseSeatClientOptions();

        Assert.Null(options.ApiKey);
        Assert.Equal(LicenseSeatClientOptions.DefaultApiBaseUrl, options.ApiBaseUrl);
        Assert.Equal("licenseseat_", options.StoragePrefix);
        Assert.Equal(TimeSpan.FromHours(1), options.AutoValidateInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), options.NetworkRecheckInterval);
        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(1), options.RetryDelay);
        Assert.False(options.Debug);
        Assert.Equal(OfflineFallbackMode.Disabled, options.OfflineFallbackMode);
        Assert.Equal(TimeSpan.FromHours(72), options.OfflineLicenseRefreshInterval);
        Assert.Equal(0, options.MaxOfflineDays);
        Assert.Equal(TimeSpan.FromMinutes(5), options.MaxClockSkew);
        Assert.True(options.AutoInitialize);
        Assert.Null(options.DeviceIdentifier);
        Assert.Equal(TimeSpan.FromSeconds(30), options.HttpTimeout);
    }

    [Fact]
    public void Constructor_WithApiKey_SetsApiKey()
    {
        var options = new LicenseSeatClientOptions("test-api-key");

        Assert.Equal("test-api-key", options.ApiKey);
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LicenseSeatClientOptions(null!));
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        var original = new LicenseSeatClientOptions
        {
            ApiKey = "test-key",
            ApiBaseUrl = "https://custom.api.com",
            StoragePrefix = "custom_",
            AutoValidateInterval = TimeSpan.FromMinutes(30),
            NetworkRecheckInterval = TimeSpan.FromSeconds(15),
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromSeconds(2),
            Debug = true,
            OfflineFallbackMode = OfflineFallbackMode.Always,
            OfflineLicenseRefreshInterval = TimeSpan.FromHours(24),
            MaxOfflineDays = 7,
            MaxClockSkew = TimeSpan.FromMinutes(10),
            AutoInitialize = false,
            DeviceIdentifier = "device-123",
            HttpTimeout = TimeSpan.FromSeconds(60)
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.ApiKey, clone.ApiKey);
        Assert.Equal(original.ApiBaseUrl, clone.ApiBaseUrl);
        Assert.Equal(original.StoragePrefix, clone.StoragePrefix);
        Assert.Equal(original.AutoValidateInterval, clone.AutoValidateInterval);
        Assert.Equal(original.NetworkRecheckInterval, clone.NetworkRecheckInterval);
        Assert.Equal(original.MaxRetries, clone.MaxRetries);
        Assert.Equal(original.RetryDelay, clone.RetryDelay);
        Assert.Equal(original.Debug, clone.Debug);
        Assert.Equal(original.OfflineFallbackMode, clone.OfflineFallbackMode);
        Assert.Equal(original.OfflineLicenseRefreshInterval, clone.OfflineLicenseRefreshInterval);
        Assert.Equal(original.MaxOfflineDays, clone.MaxOfflineDays);
        Assert.Equal(original.MaxClockSkew, clone.MaxClockSkew);
        Assert.Equal(original.AutoInitialize, clone.AutoInitialize);
        Assert.Equal(original.DeviceIdentifier, clone.DeviceIdentifier);
        Assert.Equal(original.HttpTimeout, clone.HttpTimeout);
    }

    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        var options = new LicenseSeatClientOptions("test-key");

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithEmptyApiBaseUrl_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ApiBaseUrl = "" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ApiBaseUrl", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidApiBaseUrl_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ApiBaseUrl = "not-a-valid-url" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ApiBaseUrl", exception.Message);
    }

    [Fact]
    public void Validate_WithFtpApiBaseUrl_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ApiBaseUrl = "ftp://example.com" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ApiBaseUrl", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeMaxRetries_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { MaxRetries = -1 };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("MaxRetries", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeRetryDelay_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { RetryDelay = TimeSpan.FromSeconds(-1) };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("RetryDelay", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeAutoValidateInterval_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { AutoValidateInterval = TimeSpan.FromSeconds(-1) };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("AutoValidateInterval", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeMaxOfflineDays_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { MaxOfflineDays = -1 };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("MaxOfflineDays", exception.Message);
    }

    [Fact]
    public void Validate_WithZeroHttpTimeout_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { HttpTimeout = TimeSpan.Zero };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("HttpTimeout", exception.Message);
    }

    [Fact]
    public void Validate_WithHttpsUrl_DoesNotThrow()
    {
        var options = new LicenseSeatClientOptions { ApiBaseUrl = "https://api.example.com" };

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithHttpUrl_DoesNotThrow()
    {
        var options = new LicenseSeatClientOptions { ApiBaseUrl = "http://localhost:3000" };

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(OfflineFallbackMode.Disabled)]
    [InlineData(OfflineFallbackMode.NetworkOnly)]
    [InlineData(OfflineFallbackMode.Always)]
    public void OfflineFallbackMode_CanBeSet(OfflineFallbackMode mode)
    {
        var options = new LicenseSeatClientOptions { OfflineFallbackMode = mode };

        Assert.Equal(mode, options.OfflineFallbackMode);
    }
}
