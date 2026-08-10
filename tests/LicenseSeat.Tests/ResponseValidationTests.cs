using System.Text.Json.Nodes;

namespace LicenseSeat.Tests;

public class ResponseValidationTests
{
    private static LicenseSeatClientOptions CreateOptions() => new LicenseSeatClientOptions
    {
        ApiKey = "test-api-key",
        ProductSlug = "test-product",
        ApiBaseUrl = "https://api.test.com/api/v1",
        DeviceId = "device-123",
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero,
        HeartbeatInterval = TimeSpan.Zero,
        TelemetryEnabled = false,
        MaxRetries = 0
    };

    [Theory]
    [InlineData("wrong-envelope")]
    [InlineData("missing-license")]
    [InlineData("invalid-result-without-code")]
    [InlineData("unsafe-message")]
    [InlineData("too-many-warnings")]
    [InlineData("invalid-warning-code")]
    [InlineData("invalid-warning-message")]
    [InlineData("activation-wrong-device")]
    [InlineData("activation-wrong-license")]
    [InlineData("activation-invalid-time")]
    [InlineData("wrong-license-object")]
    [InlineData("wrong-license-key")]
    [InlineData("unsafe-status")]
    [InlineData("unsupported-status")]
    [InlineData("unsafe-mode")]
    [InlineData("unsupported-mode")]
    [InlineData("unsafe-plan")]
    [InlineData("wrong-product-object")]
    [InlineData("wrong-product-slug")]
    [InlineData("invalid-start")]
    [InlineData("future-start")]
    [InlineData("expiry-before-start")]
    [InlineData("expired")]
    [InlineData("zero-seat-limit")]
    [InlineData("excessive-seat-limit")]
    [InlineData("negative-active-seats")]
    [InlineData("active-seats-over-limit")]
    [InlineData("too-many-entitlements")]
    [InlineData("invalid-entitlement")]
    [InlineData("duplicate-entitlement")]
    [InlineData("invalid-entitlement-expiry")]
    public async Task Validation_RejectsEveryMalformedOrInconsistentClaim(string mutation)
    {
        var root = JsonNode.Parse(TestResponses.ValidValidation())!.AsObject();
        var license = root["license"]!.AsObject();
        var product = license["product"]!.AsObject();

        switch (mutation)
        {
            case "wrong-envelope": root["object"] = "license"; break;
            case "missing-license": root["license"] = null; break;
            case "invalid-result-without-code": root["valid"] = false; root.Remove("code"); break;
            case "unsafe-message": root["message"] = "unsafe\nmessage"; break;
            case "too-many-warnings": root["warnings"] = WarningArray(101); break;
            case "invalid-warning-code": root["warnings"] = WarningArray(1, "bad code", "message"); break;
            case "invalid-warning-message": root["warnings"] = WarningArray(1, "code", "bad\nmessage"); break;
            case "activation-wrong-device": root["activation"] = Activation("other-device", "TEST-KEY", ValidPast()); break;
            case "activation-wrong-license": root["activation"] = Activation("device-123", "OTHER-KEY", ValidPast()); break;
            case "activation-invalid-time": root["activation"] = Activation("device-123", "TEST-KEY", "not-a-time"); break;
            case "wrong-license-object": license["object"] = "product"; break;
            case "wrong-license-key": license["key"] = "OTHER-KEY"; break;
            case "unsafe-status": license["status"] = "bad status"; break;
            case "unsupported-status": license["status"] = "unknown"; break;
            case "unsafe-mode": license["mode"] = "bad mode"; break;
            case "unsupported-mode": license["mode"] = "unknown"; break;
            case "unsafe-plan": license["plan_key"] = "bad plan"; break;
            case "wrong-product-object": product["object"] = "license"; break;
            case "wrong-product-slug": product["slug"] = "other-product"; break;
            case "invalid-start": license["starts_at"] = "not-a-time"; break;
            case "future-start": license["starts_at"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O"); break;
            case "expiry-before-start": license["expires_at"] = "2023-01-01T00:00:00Z"; break;
            case "expired": license["expires_at"] = "2024-02-01T00:00:00Z"; break;
            case "zero-seat-limit": license["seat_limit"] = 0; break;
            case "excessive-seat-limit": license["seat_limit"] = 1_000_001; break;
            case "negative-active-seats": license["active_seats"] = -1; break;
            case "active-seats-over-limit": license["seat_limit"] = 1; license["active_seats"] = 2; break;
            case "too-many-entitlements": license["active_entitlements"] = EntitlementArray(501); break;
            case "invalid-entitlement": license["active_entitlements"] = new JsonArray(new JsonObject { ["key"] = "bad key" }); break;
            case "duplicate-entitlement":
                license["active_entitlements"] = new JsonArray(
                new JsonObject { ["key"] = "same" },
                new JsonObject { ["key"] = "same" }); break;
            case "invalid-entitlement-expiry":
                license["active_entitlements"] = new JsonArray(
                new JsonObject { ["key"] = "feature", ["expires_at"] = "not-a-time" }); break;
            default: throw new InvalidOperationException($"Unknown mutation {mutation}");
        }

        var adapter = new StaticAdapter(new HttpResponse(200, root.ToJsonString()));
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ValidateAsync("TEST-KEY"));
        Assert.Equal("invalid_response", exception.Code);
        Assert.Null(client.GetCurrentLicense());
    }

    [Theory]
    [InlineData("active")]
    [InlineData("revoked")]
    [InlineData("canceled")]
    [InlineData("expired")]
    [InlineData("pending")]
    [InlineData("suspended")]
    public async Task Validation_AcceptsDocumentedStatusVocabularyForInvalidResults(string status)
    {
        var root = JsonNode.Parse(TestResponses.InvalidValidation("TEST-KEY"))!.AsObject();
        root["license"]!["status"] = status;
        var adapter = new StaticAdapter(new HttpResponse(200, root.ToJsonString()));
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        var result = await client.ValidateAsync("TEST-KEY");
        Assert.False(result.Valid);
    }

    [Theory]
    [InlineData("hardware_locked")]
    [InlineData("floating")]
    [InlineData("named_user")]
    public async Task Validation_AcceptsDocumentedLicenseModes(string mode)
    {
        var root = JsonNode.Parse(TestResponses.ValidValidation())!.AsObject();
        root["license"]!["mode"] = mode;
        var adapter = new StaticAdapter(new HttpResponse(200, root.ToJsonString()));
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        Assert.True((await client.ValidateAsync("TEST-KEY")).Valid);
    }

    [Fact]
    public async Task Validation_BindsReturnedActivationToConfiguredDevice()
    {
        var root = JsonNode.Parse(TestResponses.ValidValidation())!.AsObject();
        root["activation"] = Activation("device-123", "TEST-KEY", ValidPast());
        var adapter = new StaticAdapter(new HttpResponse(200, root.ToJsonString()));
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        Assert.True((await client.ValidateAsync("TEST-KEY")).Valid);
        using var body = System.Text.Json.JsonDocument.Parse(adapter.LastBody!);
        Assert.Equal("device-123", body.RootElement.GetProperty("fingerprint").GetString());
    }

    [Theory]
    [InlineData(404, null)]
    [InlineData(410, null)]
    [InlineData(422, "revoked")]
    [InlineData(422, "already_deactivated")]
    [InlineData(422, "not_active")]
    [InlineData(422, "not_found")]
    [InlineData(422, "suspended")]
    [InlineData(422, "expired")]
    public async Task Deactivation_ClearsOnlyForExplicitIdempotentServerOutcomes(int status, string? code)
    {
        var adapter = new LifecycleAdapter(status, code);
        using var client = new LicenseSeatClient(CreateOptions(), adapter);
        await client.ActivateAsync("TEST-KEY");

        await client.DeactivateAsync();
        Assert.Null(client.GetCurrentLicense());
    }

    [Fact]
    public async Task Deactivation_DoesNotTrustErrorMessageHeuristics()
    {
        var adapter = new LifecycleAdapter(422, null, "license already deactivated or not found");
        using var client = new LicenseSeatClient(CreateOptions(), adapter);
        await client.ActivateAsync("TEST-KEY");

        await Assert.ThrowsAsync<ApiException>(() => client.DeactivateAsync());
        Assert.NotNull(client.GetCurrentLicense());
    }

    private static JsonArray WarningArray(int count, string code = "notice", string message = "message")
    {
        return new JsonArray(Enumerable.Range(0, count)
            .Select(_ => (JsonNode)new JsonObject { ["code"] = code, ["message"] = message })
            .ToArray());
    }

    private static JsonArray EntitlementArray(int count)
    {
        return new JsonArray(Enumerable.Range(0, count)
            .Select(index => (JsonNode)new JsonObject { ["key"] = $"feature-{index}" })
            .ToArray());
    }

    private static JsonObject Activation(string fingerprint, string licenseKey, string activatedAt)
    {
        return new JsonObject
        {
            ["id"] = "activation-1",
            ["fingerprint"] = fingerprint,
            ["license_key"] = licenseKey,
            ["activated_at"] = activatedAt
        };
    }

    private static string ValidPast() => DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O");

    private sealed class StaticAdapter : IHttpClientAdapter
    {
        private readonly HttpResponse _response;

        internal StaticAdapter(HttpResponse response)
        {
            _response = response;
        }

        internal string? LastBody { get; private set; }

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_response);
        }

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastBody = jsonBody;
            return Task.FromResult(_response);
        }
    }

    private sealed class LifecycleAdapter : IHttpClientAdapter
    {
        private readonly int _deactivationStatus;
        private readonly string? _deactivationCode;
        private readonly string _deactivationMessage;

        internal LifecycleAdapter(int status, string? code, string message = "request rejected")
        {
            _deactivationStatus = status;
            _deactivationCode = code;
            _deactivationMessage = message;
        }

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponse(404, "{}"));
        }

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (url.EndsWith("/licenses/activate", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponse(200, TestResponses.Activation()));
            }

            if (url.EndsWith("/licenses/deactivate", StringComparison.Ordinal))
            {
                var error = new JsonObject
                {
                    ["error"] = new JsonObject
                    {
                        ["code"] = _deactivationCode,
                        ["message"] = _deactivationMessage
                    }
                };
                return Task.FromResult(new HttpResponse(_deactivationStatus, error.ToJsonString()));
            }

            return Task.FromResult(new HttpResponse(400, "{\"error\":{\"code\":\"unsupported\",\"message\":\"unsupported\"}}"));
        }
    }
}
