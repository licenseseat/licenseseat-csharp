using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat.Tests;

public class HeartbeatTimerTests
{
    private sealed class MockHttpClient : IHttpClientAdapter
    {
        private Func<string, string, HttpResponse>? _postHandler;
        private int _heartbeatCount;

        public int HeartbeatCount => _heartbeatCount;

        public void SetupPost(Func<string, string, HttpResponse> handler) => _postHandler = handler;

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
            => Task.FromResult(new HttpResponse(200, "{}"));

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (url.Contains("/heartbeat"))
            {
                Interlocked.Increment(ref _heartbeatCount);
            }
            var response = _postHandler?.Invoke(url, jsonBody) ?? new HttpResponse(200, "{}");
            return Task.FromResult(response);
        }
    }

    private static readonly string ActivationJson = """
        {
            "object": "activation",
            "id": "123",
            "device_id": "device-123",
            "license_key": "TEST-KEY",
            "activated_at": "2024-01-01T00:00:00Z",
            "license": {
                "key": "TEST-KEY",
                "status": "active",
                "plan_key": "pro",
                "active_seats": 1
            }
        }
    """;

    private static LicenseSeatClientOptions CreateOptions(TimeSpan heartbeatInterval) => new LicenseSeatClientOptions
    {
        ApiKey = "test-api-key",
        ProductSlug = "test-product",
        ApiBaseUrl = "https://api.test.com",
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero, // Disable auto-validation
        HeartbeatInterval = heartbeatInterval,
    };

    // ================================================================
    // HeartbeatInterval default
    // ================================================================

    [Fact]
    public void HeartbeatInterval_DefaultIsFiveMinutes()
    {
        var options = new LicenseSeatClientOptions();
        Assert.Equal(TimeSpan.FromMinutes(5), options.HeartbeatInterval);
    }

    [Fact]
    public void HeartbeatInterval_CanBeCustomized()
    {
        var options = new LicenseSeatClientOptions { HeartbeatInterval = TimeSpan.FromSeconds(30) };
        Assert.Equal(TimeSpan.FromSeconds(30), options.HeartbeatInterval);
    }

    [Fact]
    public void HeartbeatInterval_ClonedCorrectly()
    {
        var original = new LicenseSeatClientOptions { HeartbeatInterval = TimeSpan.FromMinutes(10) };
        var clone = original.Clone();
        Assert.Equal(TimeSpan.FromMinutes(10), clone.HeartbeatInterval);
    }

    // ================================================================
    // Heartbeat timer starts on activation
    // ================================================================

    [Fact]
    public async Task HeartbeatTimer_StartsOnActivation()
    {
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/heartbeat")) return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            return new HttpResponse(200, "{}");
        });

        var options = CreateOptions(TimeSpan.FromMilliseconds(200));

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        // Wait for at least 2 heartbeat timer firings
        await Task.Delay(700);

        Assert.True(mockHttp.HeartbeatCount >= 2,
            $"Expected >= 2 heartbeat calls from timer, got {mockHttp.HeartbeatCount}");
    }

    // ================================================================
    // Heartbeat timer does NOT start when interval <= 0
    // ================================================================

    [Fact]
    public async Task HeartbeatTimer_DoesNotStartWhenIntervalIsZero()
    {
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            return new HttpResponse(200, "{}");
        });

        var options = CreateOptions(TimeSpan.Zero);

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        await Task.Delay(500);

        Assert.Equal(0, mockHttp.HeartbeatCount);
    }

    [Fact]
    public async Task HeartbeatTimer_DoesNotStartWhenIntervalIsNegative()
    {
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            return new HttpResponse(200, "{}");
        });

        var options = CreateOptions(TimeSpan.FromSeconds(-1));

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        await Task.Delay(500);

        Assert.Equal(0, mockHttp.HeartbeatCount);
    }

    // ================================================================
    // Heartbeat timer stops on deactivation
    // ================================================================

    [Fact]
    public async Task HeartbeatTimer_StopsOnDeactivation()
    {
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/deactivate")) return new HttpResponse(200, """{"object":"deactivation","activation_id":"123","deactivated_at":"2024-01-01T00:00:00Z"}""");
            if (url.Contains("/heartbeat")) return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            return new HttpResponse(200, "{}");
        });

        var options = CreateOptions(TimeSpan.FromMilliseconds(200));

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        // Wait for some heartbeats
        await Task.Delay(500);
        var countBeforeDeactivation = mockHttp.HeartbeatCount;
        Assert.True(countBeforeDeactivation >= 1, "Should have at least 1 heartbeat before deactivation");

        // Deactivate -- timer should stop
        await client.DeactivateAsync();

        // Wait and check no more heartbeats fire
        await Task.Delay(500);
        var countAfterDeactivation = mockHttp.HeartbeatCount;

        Assert.Equal(countBeforeDeactivation, countAfterDeactivation);
    }

    // ================================================================
    // Heartbeat timer stops on Reset
    // ================================================================

    [Fact]
    public async Task HeartbeatTimer_StopsOnReset()
    {
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/heartbeat")) return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            return new HttpResponse(200, "{}");
        });

        var options = CreateOptions(TimeSpan.FromMilliseconds(200));

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        await Task.Delay(500);
        var countBeforeReset = mockHttp.HeartbeatCount;
        Assert.True(countBeforeReset >= 1);

        client.Reset();

        await Task.Delay(500);
        Assert.Equal(countBeforeReset, mockHttp.HeartbeatCount);
    }

    // ================================================================
    // Heartbeat timer stops on Dispose
    // ================================================================

    [Fact]
    public async Task HeartbeatTimer_StopsOnDispose()
    {
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/heartbeat")) return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            return new HttpResponse(200, "{}");
        });

        var options = CreateOptions(TimeSpan.FromMilliseconds(200));

        var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        await Task.Delay(500);
        var countBeforeDispose = mockHttp.HeartbeatCount;

        client.Dispose();

        await Task.Delay(500);
        Assert.Equal(countBeforeDispose, mockHttp.HeartbeatCount);
    }

    // ================================================================
    // Heartbeat timer stops on PurgeCachedLicense
    // ================================================================

    [Fact]
    public async Task HeartbeatTimer_StopsOnPurgeCachedLicense()
    {
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/heartbeat")) return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            return new HttpResponse(200, "{}");
        });

        var options = CreateOptions(TimeSpan.FromMilliseconds(200));

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        await Task.Delay(500);
        var countBeforePurge = mockHttp.HeartbeatCount;
        Assert.True(countBeforePurge >= 1);

        client.PurgeCachedLicense();

        await Task.Delay(500);
        Assert.Equal(countBeforePurge, mockHttp.HeartbeatCount);
    }

    // ================================================================
    // Heartbeat timer fires independently from auto-validation
    // ================================================================

    [Fact]
    public async Task HeartbeatTimer_FiresIndependentlyFromAutoValidation()
    {
        var validationCount = 0;
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/validate"))
            {
                Interlocked.Increment(ref validationCount);
                return new HttpResponse(200, """{"object":"validation_result","valid":true,"license":{"key":"TEST-KEY","status":"active","active_seats":1}}""");
            }
            if (url.Contains("/heartbeat")) return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            return new HttpResponse(200, "{}");
        });

        // Auto-validation at 5 second intervals (won't fire during test),
        // heartbeat at 200ms intervals (will fire multiple times)
        var options = new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ProductSlug = "test-product",
            ApiBaseUrl = "https://api.test.com",
            AutoInitialize = false,
            AutoValidateInterval = TimeSpan.FromSeconds(5),
            HeartbeatInterval = TimeSpan.FromMilliseconds(200),
        };

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");

        await Task.Delay(700);

        // Heartbeat should have fired multiple times
        Assert.True(mockHttp.HeartbeatCount >= 2,
            $"Expected >= 2 heartbeats, got {mockHttp.HeartbeatCount}");

        // Auto-validation should NOT have fired yet (5s interval)
        Assert.Equal(0, validationCount);
    }

    // ================================================================
    // Telemetry is injected into heartbeat requests
    // ================================================================

    [Fact]
    public async Task HeartbeatRequest_IncludesTelemetry()
    {
        string? capturedBody = null;
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/heartbeat"))
            {
                capturedBody = body;
                return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            }
            return new HttpResponse(200, "{}");
        });

        var options = new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ProductSlug = "test-product",
            ApiBaseUrl = "https://api.test.com",
            AutoInitialize = false,
            AutoValidateInterval = TimeSpan.Zero,
            HeartbeatInterval = TimeSpan.Zero, // Don't use timer
            TelemetryEnabled = true,
        };

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");
        await client.HeartbeatAsync();

        Assert.NotNull(capturedBody);

        var json = JsonDocument.Parse(capturedBody!);
        var root = json.RootElement;

        Assert.True(root.TryGetProperty("telemetry", out var telemetry), "Heartbeat request should include telemetry");
        Assert.True(telemetry.TryGetProperty("sdk_version", out _), "telemetry should have sdk_version");
        Assert.True(telemetry.TryGetProperty("platform", out var platform), "telemetry should have platform");
        Assert.Equal("native", platform.GetString());
        Assert.True(telemetry.TryGetProperty("architecture", out _), "telemetry should have architecture");
        Assert.True(telemetry.TryGetProperty("cpu_cores", out _), "telemetry should have cpu_cores");
        Assert.True(telemetry.TryGetProperty("device_type", out _), "telemetry should have device_type");
        Assert.True(telemetry.TryGetProperty("language", out _), "telemetry should have language");
        Assert.True(telemetry.TryGetProperty("runtime_version", out _), "telemetry should have runtime_version");
    }

    [Fact]
    public async Task HeartbeatRequest_ExcludesTelemetryWhenDisabled()
    {
        string? capturedBody = null;
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activate")) return new HttpResponse(200, ActivationJson);
            if (url.Contains("/heartbeat"))
            {
                capturedBody = body;
                return new HttpResponse(200, """{"object":"heartbeat","received_at":"2024-01-01T00:00:00Z"}""");
            }
            return new HttpResponse(200, "{}");
        });

        var options = new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ProductSlug = "test-product",
            ApiBaseUrl = "https://api.test.com",
            AutoInitialize = false,
            AutoValidateInterval = TimeSpan.Zero,
            HeartbeatInterval = TimeSpan.Zero,
            TelemetryEnabled = false,
        };

        using var client = new LicenseSeatClient(options, mockHttp);
        await client.ActivateAsync("TEST-KEY");
        await client.HeartbeatAsync();

        Assert.NotNull(capturedBody);

        var json = JsonDocument.Parse(capturedBody!);
        var root = json.RootElement;

        Assert.False(root.TryGetProperty("telemetry", out _), "Heartbeat should NOT include telemetry when disabled");
    }
}
