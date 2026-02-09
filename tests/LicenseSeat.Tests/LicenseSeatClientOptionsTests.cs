using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat.Tests;

public class LicenseSeatClientOptionsTests
{
    [Fact]
    public void Constructor_Default_SetsDefaultValues()
    {
        var options = new LicenseSeatClientOptions();

        Assert.Null(options.ApiKey);
        Assert.Null(options.ProductSlug);
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
        Assert.Null(options.DeviceId);
        Assert.Equal(TimeSpan.FromSeconds(30), options.HttpTimeout);
        Assert.Null(options.HttpClientAdapter);
        Assert.Equal(TimeSpan.FromMinutes(5), options.HeartbeatInterval);
        Assert.Null(options.AppVersion);
        Assert.Null(options.AppBuild);
    }

    [Fact]
    public void Constructor_WithApiKeyAndProductSlug_SetsBoth()
    {
        var options = new LicenseSeatClientOptions("test-api-key", "test-product");

        Assert.Equal("test-api-key", options.ApiKey);
        Assert.Equal("test-product", options.ProductSlug);
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LicenseSeatClientOptions(null!, "test-product"));
    }

    [Fact]
    public void Constructor_WithNullProductSlug_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LicenseSeatClientOptions("test-key", null!));
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        var mockAdapter = new MockHttpClientAdapter();
        var original = new LicenseSeatClientOptions
        {
            ApiKey = "test-key",
            ProductSlug = "test-product",
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
            DeviceId = "device-123",
            HttpTimeout = TimeSpan.FromSeconds(60),
            HttpClientAdapter = mockAdapter,
            HeartbeatInterval = TimeSpan.FromMinutes(10),
            AppVersion = "2.0.0",
            AppBuild = "100"
        };

        var clone = original.Clone();

        Assert.NotSame(original, clone);
        Assert.Equal(original.ApiKey, clone.ApiKey);
        Assert.Equal(original.ProductSlug, clone.ProductSlug);
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
        Assert.Equal(original.DeviceId, clone.DeviceId);
        Assert.Equal(original.HttpTimeout, clone.HttpTimeout);
        Assert.Same(original.HttpClientAdapter, clone.HttpClientAdapter);
        Assert.Equal(original.HeartbeatInterval, clone.HeartbeatInterval);
        Assert.Equal(original.AppVersion, clone.AppVersion);
        Assert.Equal(original.AppBuild, clone.AppBuild);
    }

    /// <summary>
    /// Mock HTTP client adapter for testing Clone functionality.
    /// </summary>
    private sealed class MockHttpClientAdapter : IHttpClientAdapter
    {
        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(new HttpResponse(200, "{}"));

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
            => Task.FromResult(new HttpResponse(200, "{}"));
    }

    [Fact]
    public void Validate_WithValidOptions_DoesNotThrow()
    {
        var options = new LicenseSeatClientOptions("test-key", "test-product");

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithEmptyApiBaseUrl_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", ApiBaseUrl = "" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ApiBaseUrl", exception.Message);
    }

    [Fact]
    public void Validate_WithInvalidApiBaseUrl_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", ApiBaseUrl = "not-a-valid-url" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ApiBaseUrl", exception.Message);
    }

    [Fact]
    public void Validate_WithFtpApiBaseUrl_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", ApiBaseUrl = "ftp://example.com" };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("ApiBaseUrl", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeMaxRetries_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", MaxRetries = -1 };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("MaxRetries", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeRetryDelay_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", RetryDelay = TimeSpan.FromSeconds(-1) };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("RetryDelay", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeAutoValidateInterval_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", AutoValidateInterval = TimeSpan.FromSeconds(-1) };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("AutoValidateInterval", exception.Message);
    }

    [Fact]
    public void Validate_WithNegativeMaxOfflineDays_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", MaxOfflineDays = -1 };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("MaxOfflineDays", exception.Message);
    }

    [Fact]
    public void Validate_WithZeroHttpTimeout_ThrowsInvalidOperationException()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", HttpTimeout = TimeSpan.Zero };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.Contains("HttpTimeout", exception.Message);
    }

    [Fact]
    public void Validate_WithHttpsUrl_DoesNotThrow()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", ApiBaseUrl = "https://api.example.com" };

        var exception = Record.Exception(() => options.Validate());

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_WithHttpUrl_DoesNotThrow()
    {
        var options = new LicenseSeatClientOptions { ProductSlug = "test-product", ApiBaseUrl = "http://localhost:3000" };

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
