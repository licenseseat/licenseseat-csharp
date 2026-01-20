using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat.Tests;

public class LicenseSeatClientTests
{
    private sealed class MockHttpClient : IHttpClientAdapter
    {
        private Func<string, HttpResponse>? _getHandler;
        private Func<string, string, HttpResponse>? _postHandler;

        public void SetupGet(Func<string, HttpResponse> handler) => _getHandler = handler;
        public void SetupPost(Func<string, string, HttpResponse> handler) => _postHandler = handler;

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _getHandler?.Invoke(url) ?? new HttpResponse(200, "{}");
            return Task.FromResult(response);
        }

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _postHandler?.Invoke(url, jsonBody) ?? new HttpResponse(200, "{}");
            return Task.FromResult(response);
        }
    }

    private static LicenseSeatClientOptions CreateOptions() => new LicenseSeatClientOptions
    {
        ApiKey = "test-api-key",
        ApiBaseUrl = "https://api.test.com",
        AutoInitialize = false, // Disable for tests
        AutoValidateInterval = TimeSpan.Zero // Disable auto-validation for tests
    };

    [Fact]
    public void Constructor_WithOptions_CreatesClient()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        Assert.NotNull(client);
        Assert.NotNull(client.Events);
        Assert.True(client.IsOnline);
    }

    [Fact]
    public void Constructor_WithApiKey_CreatesClient()
    {
        // This test creates a real HTTP client, but we're just testing construction
        var options = new LicenseSeatClientOptions("test-key")
        {
            AutoInitialize = false
        };

        using var client = new LicenseSeatClient(options);

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new LicenseSeatClient((LicenseSeatClientOptions)null!));
    }

    [Fact]
    public async Task Constructor_WithHttpClientAdapterInOptions_UsesProvidedAdapter()
    {
        // Arrange: Create a mock that tracks whether it was called
        var mockHttp = new MockHttpClient();
        var wasCalled = false;
        mockHttp.SetupPost((url, body) =>
        {
            wasCalled = true;
            if (url.Contains("/activations/activate"))
            {
                return new HttpResponse(200, """
                    {
                        "success": true,
                        "license": {
                            "license_key": "TEST-KEY",
                            "status": "active",
                            "plan_key": "pro",
                            "seat_limit": 5
                        }
                    }
                """);
            }
            return new HttpResponse(200, "{}");
        });

        // Set the adapter via options (this is the key part of this test)
        var options = new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ApiBaseUrl = "https://api.test.com",
            AutoInitialize = false,
            AutoValidateInterval = TimeSpan.Zero,
            HttpClientAdapter = mockHttp  // <-- Inject via options, not constructor
        };

        // Act: Use the PUBLIC constructor (not the internal one)
        using var client = new LicenseSeatClient(options);
        await client.ActivateAsync("TEST-KEY");

        // Assert: The mock adapter should have been used
        Assert.True(wasCalled, "HttpClientAdapter from options should be used for API calls");
    }

    [Fact]
    public async Task ActivateAsync_Success_ReturnsLicense()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activations/activate"))
            {
                return new HttpResponse(200, """
                    {
                        "success": true,
                        "license": {
                            "license_key": "TEST-KEY",
                            "status": "active",
                            "plan_key": "pro",
                            "seat_limit": 5
                        }
                    }
                """);
            }
            return new HttpResponse(200, "{}");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        var license = await client.ActivateAsync("TEST-KEY");

        Assert.NotNull(license);
        Assert.Equal("TEST-KEY", license.LicenseKey);
        Assert.Equal("pro", license.PlanKey);
    }

    [Fact]
    public async Task ActivateAsync_WithEmptyLicenseKey_ThrowsArgumentException()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        await Assert.ThrowsAsync<ArgumentException>(() => client.ActivateAsync(""));
    }

    [Fact]
    public async Task ActivateAsync_EmitsEvents()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((_, _) => new HttpResponse(200, """{"success":true,"license":{"license_key":"TEST"}}"""));

        using var client = new LicenseSeatClient(options, mockHttp);
        var startEventReceived = false;
        var successEventReceived = false;

        client.Events.On(LicenseSeatEvents.ActivationStart, _ => startEventReceived = true);
        client.Events.On(LicenseSeatEvents.ActivationSuccess, _ => successEventReceived = true);

        await client.ActivateAsync("TEST-KEY");

        Assert.True(startEventReceived);
        Assert.True(successEventReceived);
    }

    [Fact]
    public async Task ActivateAsync_Error_EmitsErrorEvent()
    {
        var options = CreateOptions();
        options.MaxRetries = 0;
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((_, _) => new HttpResponse(400, """{"error":"Invalid license key"}"""));

        using var client = new LicenseSeatClient(options, mockHttp);
        var errorEventReceived = false;

        client.Events.On(LicenseSeatEvents.ActivationError, _ => errorEventReceived = true);

        await Assert.ThrowsAsync<ApiException>(() => client.ActivateAsync("INVALID-KEY"));

        Assert.True(errorEventReceived);
    }

    [Fact]
    public async Task ValidateAsync_Success_ReturnsValidResult()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, _) =>
        {
            if (url.Contains("/licenses/validate"))
            {
                return new HttpResponse(200, """{"valid":true,"license":{"license_key":"TEST-KEY","status":"active"}}""");
            }
            return new HttpResponse(200, "{}");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        var result = await client.ValidateAsync("TEST-KEY");

        Assert.True(result.Valid);
    }

    [Fact]
    public async Task ValidateAsync_Invalid_ReturnsInvalidResult()
    {
        var options = CreateOptions();
        options.MaxRetries = 0;
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, _) =>
        {
            if (url.Contains("/licenses/validate"))
            {
                return new HttpResponse(200, """{"valid":false,"error":"License expired","reason_code":"expired"}""");
            }
            return new HttpResponse(200, "{}");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        var result = await client.ValidateAsync("EXPIRED-KEY");

        Assert.False(result.Valid);
        Assert.Equal("expired", result.ReasonCode);
    }

    [Fact]
    public async Task DeactivateAsync_WithNoLicense_ThrowsLicenseException()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        var ex = await Assert.ThrowsAsync<LicenseException>(() => client.DeactivateAsync());
        Assert.Equal(LicenseException.NoLicenseCode, ex.ErrorCode);
    }

    [Fact]
    public async Task DeactivateAsync_Success_ClearsCache()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, _) =>
        {
            if (url.Contains("/activations/activate"))
            {
                return new HttpResponse(200, """{"success":true,"license":{"license_key":"TEST"}}""");
            }
            if (url.Contains("/activations/deactivate"))
            {
                return new HttpResponse(200, """{"success":true}""");
            }
            return new HttpResponse(200, "{}");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        // Activate first
        await client.ActivateAsync("TEST-KEY");
        Assert.NotNull(client.GetCurrentLicense());

        // Then deactivate
        await client.DeactivateAsync();
        Assert.Null(client.GetCurrentLicense());
    }

    [Fact]
    public void GetStatus_WithNoLicense_ReturnsInactive()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        var status = client.GetStatus();

        Assert.Equal(LicenseStatusType.Inactive, status.StatusType);
        Assert.False(status.IsValid);
    }

    [Fact]
    public async Task GetStatus_AfterActivation_ReturnsPending()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((_, _) => new HttpResponse(200, """{"success":true,"license":{"license_key":"TEST"}}"""));

        using var client = new LicenseSeatClient(options, mockHttp);

        await client.ActivateAsync("TEST-KEY");
        var status = client.GetStatus();

        // After activation with optimistic validation, status should be pending or valid
        // The optimistic validation sets Valid=true, so it should be Active
        Assert.True(status.IsValid || status.IsPending);
    }

    [Fact]
    public void CheckEntitlement_WithNoLicense_ReturnsNoLicense()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        var status = client.CheckEntitlement("pro-features");

        Assert.False(status.Active);
        Assert.Equal(EntitlementInactiveReason.NoLicense, status.Reason);
    }

    [Fact]
    public void HasEntitlement_WithNoLicense_ReturnsFalse()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        Assert.False(client.HasEntitlement("pro-features"));
    }

    [Fact]
    public async Task Reset_ClearsAllData()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((_, _) => new HttpResponse(200, """{"success":true,"license":{"license_key":"TEST"}}"""));

        using var client = new LicenseSeatClient(options, mockHttp);
        var resetEventReceived = false;

        client.Events.On(LicenseSeatEvents.SdkReset, _ => resetEventReceived = true);

        // Setup some state first
        await client.ActivateAsync("TEST-KEY");

        // Reset
        client.Reset();

        Assert.True(resetEventReceived);
        Assert.Null(client.GetCurrentLicense());
    }

    [Fact]
    public async Task TestAuthAsync_WithNoApiKey_ThrowsConfigurationException()
    {
        var options = CreateOptions();
        options.ApiKey = null;
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        var ex = await Assert.ThrowsAsync<ConfigurationException>(() => client.TestAuthAsync());
        Assert.Equal(ConfigurationException.MissingApiKeyCode, ex.ErrorCode);
    }

    [Fact]
    public async Task TestAuthAsync_Success_ReturnsTrue()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupGet(_ => new HttpResponse(200, """{"success":true}"""));

        using var client = new LicenseSeatClient(options, mockHttp);

        var result = await client.TestAuthAsync();

        Assert.True(result);
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        var client = new LicenseSeatClient(options, mockHttp);
        var destroyedEventReceived = false;

        client.Events.On(LicenseSeatEvents.SdkDestroyed, _ => destroyedEventReceived = true);

        client.Dispose();

        Assert.True(destroyedEventReceived);
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        var client = new LicenseSeatClient(options, mockHttp);

        client.Dispose();
        var exception = Record.Exception(() => client.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public async Task OfflineFallback_WhenEnabled_FallsBackOnNetworkError()
    {
        var options = CreateOptions();
        options.OfflineFallbackMode = OfflineFallbackMode.Always;
        options.MaxRetries = 0;

        var mockHttp = new MockHttpClient();
        // First activation succeeds
        mockHttp.SetupPost((url, _) =>
        {
            if (url.Contains("/activations/activate"))
            {
                return new HttpResponse(200, """{"success":true,"license":{"license_key":"TEST"}}""");
            }
            // Validation fails with network error
            return new HttpResponse(0, "Network error");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        // Activate first
        await client.ActivateAsync("TEST-KEY");

        // Validation should fall back to offline (and fail since no offline license cached)
        var result = await client.ValidateAsync("TEST-KEY");

        Assert.True(result.Offline);
        // No offline license cached, so should be invalid
        Assert.False(result.Valid);
    }

    #region Synchronous Wrapper Tests

    [Fact]
    public void Activate_Sync_ReturnsLicense()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/activations/activate"))
            {
                return new HttpResponse(200, """
                    {
                        "success": true,
                        "license": {
                            "license_key": "SYNC-TEST-KEY",
                            "status": "active",
                            "plan_key": "pro"
                        }
                    }
                """);
            }
            return new HttpResponse(200, "{}");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        // Use synchronous method
        var license = client.Activate("SYNC-TEST-KEY");

        Assert.NotNull(license);
        Assert.Equal("SYNC-TEST-KEY", license.LicenseKey);
    }

    [Fact]
    public void Validate_Sync_ReturnsValidationResult()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((url, body) =>
        {
            if (url.Contains("/licenses/validate"))
            {
                return new HttpResponse(200, """
                    {
                        "valid": true,
                        "license": {
                            "license_key": "SYNC-VALIDATE-KEY",
                            "status": "active"
                        }
                    }
                """);
            }
            return new HttpResponse(200, "{}");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        // Use synchronous method
        var result = client.Validate("SYNC-VALIDATE-KEY");

        Assert.True(result.Valid);
        Assert.NotNull(result.License);
        Assert.Equal("SYNC-VALIDATE-KEY", result.License!.LicenseKey);
    }

    [Fact]
    public void Deactivate_Sync_ThrowsWhenNoActiveLicense()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        // Should throw because no active license
        var ex = Assert.Throws<LicenseException>(() => client.Deactivate());
        Assert.Contains("No active license", ex.Message);
    }

    [Fact]
    public void TestAuth_Sync_ReturnsTrue_WhenAuthenticated()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupGet(url =>
        {
            if (url.Contains("/test-auth"))
            {
                return new HttpResponse(200, """{"authenticated": true}""");
            }
            return new HttpResponse(200, "{}");
        });

        using var client = new LicenseSeatClient(options, mockHttp);

        // Use synchronous method
        var result = client.TestAuth();

        Assert.True(result);
    }

    [Fact]
    public void SyncMethods_WorkWithCustomHttpAdapter_FromOptions()
    {
        // This test verifies that sync methods work when using HttpClientAdapter from options
        var mockHttp = new MockHttpClient();
        var wasCalled = false;
        mockHttp.SetupPost((url, body) =>
        {
            wasCalled = true;
            if (url.Contains("/activations/activate"))
            {
                return new HttpResponse(200, """
                    {
                        "success": true,
                        "license": {
                            "license_key": "TEST",
                            "status": "active"
                        }
                    }
                """);
            }
            return new HttpResponse(200, "{}");
        });

        var options = new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ApiBaseUrl = "https://api.test.com",
            AutoInitialize = false,
            AutoValidateInterval = TimeSpan.Zero,
            HttpClientAdapter = mockHttp // Use adapter from options
        };

        // Use public constructor (not internal)
        using var client = new LicenseSeatClient(options);

        // Use synchronous method
        var license = client.Activate("TEST");

        Assert.True(wasCalled, "Custom HTTP adapter should be used");
        Assert.NotNull(license);
    }

    #endregion
}
