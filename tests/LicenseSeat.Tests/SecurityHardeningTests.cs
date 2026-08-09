using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace LicenseSeat.Tests;

public class SecurityHardeningTests
{
    private static readonly JsonSerializerOptions OfflineJsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        MaxDepth = SecurityValidation.MaxJsonDepth,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static LicenseSeatClientOptions CreateOptions() => new LicenseSeatClientOptions
    {
        ApiKey = "test-api-key",
        ProductSlug = "test-product",
        ApiBaseUrl = "https://api.test.com/api/v1",
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero,
        HeartbeatInterval = TimeSpan.Zero,
        MaxRetries = 0,
        DeviceId = "device-123"
    };

    [Fact]
    public async Task LicenseOperations_KeepLicenseKeyOutOfUrls()
    {
        var adapter = new CapturingAdapter
        {
            PostHandler = (url, _) =>
            {
                if (url.EndsWith("/licenses/activate", StringComparison.Ordinal))
                {
                    return new HttpResponse(200, TestResponses.Activation());
                }

                if (url.EndsWith("/licenses/validate", StringComparison.Ordinal))
                {
                    return new HttpResponse(200, TestResponses.ValidValidation());
                }

                if (url.EndsWith("/licenses/heartbeat", StringComparison.Ordinal))
                {
                    return new HttpResponse(200, TestResponses.Heartbeat());
                }

                if (url.EndsWith("/licenses/deactivate", StringComparison.Ordinal))
                {
                    return new HttpResponse(200, TestResponses.Deactivation());
                }

                return new HttpResponse(400, "{\"error\":{\"code\":\"unsupported\",\"message\":\"unsupported\"}}");
            }
        };

        using var client = new LicenseSeatClient(CreateOptions(), adapter);
        await client.ActivateAsync("TEST-KEY");
        await client.ValidateAsync("TEST-KEY");
        await client.HeartbeatAsync();
        await client.DeactivateAsync();

        var protectedRequests = adapter.Posts
            .Where(request => !request.Url.EndsWith("/licenses/offline-token", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(4, adapter.Posts.Count);
        Assert.Equal(4, protectedRequests.Count);
        Assert.All(protectedRequests, request => Assert.DoesNotContain("TEST-KEY", request.Url));
        Assert.All(protectedRequests, request =>
        {
            using var document = JsonDocument.Parse(request.Body);
            Assert.Equal("TEST-KEY", document.RootElement.GetProperty("license_key").GetString());
            Assert.Equal("device-123", document.RootElement.GetProperty("fingerprint").GetString());
            Assert.False(document.RootElement.TryGetProperty("device_id", out _));
        });
    }

    [Fact]
    public async Task TestAuth_UsesProtectedAuthEndpoint()
    {
        var adapter = new CapturingAdapter
        {
            GetHandler = (url) => new HttpResponse(200, TestResponses.Authentication())
        };

        using var client = new LicenseSeatClient(CreateOptions(), adapter);
        Assert.True(await client.TestAuthAsync());
        Assert.Equal("https://api.test.com/api/v1/auth", adapter.Gets.Single());
    }

    [Fact]
    public async Task Validation_ForbiddenEmitsAuthenticationFailureEvent()
    {
        var adapter = new CapturingAdapter
        {
            PostHandler = (_, _) => new HttpResponse(
                403,
                "{\"errors\":[{\"code\":\"FORBIDDEN\",\"title\":\"Forbidden\",\"detail\":\"invalid credential scope\"}]}")
        };
        using var client = new LicenseSeatClient(CreateOptions(), adapter);
        var authFailures = 0;
        client.Events.On(LicenseSeatEvents.ValidationAuthFailed, _ => Interlocked.Increment(ref authFailures));

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ValidateAsync("TEST-KEY"));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal(1, Volatile.Read(ref authFailures));
    }

    [Fact]
    public async Task OfflinePolicyDisabled_DoesNotFetchOrRefreshCredentials()
    {
        var adapter = new CapturingAdapter
        {
            PostHandler = (url, _) => url.EndsWith("/licenses/activate", StringComparison.Ordinal)
                ? new HttpResponse(200, TestResponses.Activation())
                : new HttpResponse(500, "{\"error\":{\"code\":\"unexpected\",\"message\":\"unexpected\"}}")
        };
        var options = CreateOptions();
        options.OfflineLicenseRefreshInterval = TimeSpan.FromMilliseconds(20);
        using var client = new LicenseSeatClient(options, adapter);

        await client.ActivateAsync("TEST-KEY");
        await Task.Delay(100);

        Assert.Single(adapter.Posts);
        Assert.DoesNotContain(adapter.Posts, request =>
            request.Url.EndsWith("/licenses/offline-token", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OfflinePolicyEnabled_PeriodicallyRefreshesCredentials()
    {
        var offlineRequests = 0;
        var adapter = new CapturingAdapter
        {
            PostHandler = (url, _) =>
            {
                if (url.EndsWith("/licenses/activate", StringComparison.Ordinal))
                {
                    return new HttpResponse(200, TestResponses.Activation());
                }

                if (url.EndsWith("/licenses/offline-token", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref offlineRequests);
                    return new HttpResponse(500, "{\"error\":{\"code\":\"unavailable\",\"message\":\"unavailable\"}}");
                }

                return new HttpResponse(500, "{\"error\":{\"code\":\"unexpected\",\"message\":\"unexpected\"}}");
            }
        };
        var options = CreateOptions();
        options.OfflineFallbackMode = OfflineFallbackMode.NetworkOnly;
        options.MaxOfflineDays = 7;
        options.OfflineLicenseRefreshInterval = TimeSpan.FromMilliseconds(20);
        using var client = new LicenseSeatClient(options, adapter);

        await client.ActivateAsync("TEST-KEY");
        for (var attempt = 0; attempt < 50 && Volatile.Read(ref offlineRequests) < 2; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(Volatile.Read(ref offlineRequests) >= 2);
    }

    [Fact]
    public async Task ApiClient_RejectsDuplicateJsonProperties()
    {
        var adapter = new CapturingAdapter
        {
            GetHandler = _ => new HttpResponse(200, "{\"valid\":true,\"valid\":false}")
        };

        using var client = new ApiClient(CreateOptions(), adapter);
        var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<ValidationResult>("/test"));
        Assert.Equal(200, exception.StatusCode);
        Assert.Contains("invalid or ambiguous JSON", exception.Message);
    }

    [Fact]
    public async Task ApiClient_RejectsEmptySuccessfulResponse()
    {
        var adapter = new CapturingAdapter { GetHandler = _ => new HttpResponse(204, string.Empty) };
        using var client = new ApiClient(CreateOptions(), adapter);

        await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<object>("/test"));
    }

    [Fact]
    public async Task ApiClient_RejectsOversizedCustomAdapterResponse()
    {
        var options = CreateOptions();
        options.MaxResponseBodyBytes = 1024;
        var adapter = new CapturingAdapter
        {
            GetHandler = _ => new HttpResponse(200, "{\"value\":\"" + new string('a', 1100) + "\"}")
        };
        using var client = new ApiClient(options, adapter);

        await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<object>("/test"));
    }

    [Theory]
    [InlineData("/../outside")]
    [InlineData("/%2e%2e/outside")]
    [InlineData("/safe%2foutside")]
    [InlineData("/safe%25%32%65%25%32%65/outside")]
    [InlineData("/malformed%2")]
    [InlineData("/malformed%zz")]
    [InlineData("//evil.example/path")]
    [InlineData("/path?secret=value")]
    public async Task ApiClient_RejectsUnsafePathsBeforeTransport(string path)
    {
        var adapter = new CapturingAdapter();
        using var client = new ApiClient(CreateOptions(), adapter);

        await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync<object>(path));
        Assert.Empty(adapter.Gets);
    }

    [Fact]
    public async Task ApiClient_DoesNotRetryMutationByDefault()
    {
        var options = CreateOptions();
        options.MaxRetries = 8;
        var adapter = new CapturingAdapter
        {
            PostHandler = (_, _) => new HttpResponse(503, "{\"error\":{\"code\":\"unavailable\",\"message\":\"unavailable\"}}")
        };
        using var client = new ApiClient(options, adapter);

        await Assert.ThrowsAsync<ApiException>(() =>
            client.PostAsync<ValidationRequest, ValidationResult>("/mutation", new ValidationRequest()));
        Assert.Single(adapter.Posts);
    }

    [Fact]
    public async Task Activation_RejectsMismatchedDeviceBinding()
    {
        var adapter = new CapturingAdapter
        {
            PostHandler = (_, _) => new HttpResponse(200, TestResponses.Activation(fingerprint: "other-device"))
        };
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ActivateAsync("TEST-KEY"));
        Assert.Equal("invalid_response", exception.Code);
        Assert.Null(client.GetCurrentLicense());
    }

    [Fact]
    public async Task Activation_AcceptsServerNumericIdentifierWithoutWeakeningBinding()
    {
        var body = TestResponses.Activation().Replace("\"id\":\"123\"", "\"id\":123", StringComparison.Ordinal);
        var adapter = new CapturingAdapter
        {
            PostHandler = (_, _) => new HttpResponse(200, body)
        };
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        var license = await client.ActivateAsync("TEST-KEY");
        Assert.Equal("TEST-KEY", license.Key);
    }

    [Fact]
    public async Task Validation_RejectsMismatchedProductBinding()
    {
        var adapter = new CapturingAdapter
        {
            PostHandler = (_, _) => new HttpResponse(200, TestResponses.ValidValidation(productSlug: "other-product"))
        };
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ValidateAsync("TEST-KEY"));
        Assert.Equal("invalid_response", exception.Code);
    }

    [Fact]
    public async Task Validation_RejectsValidClaimForExpiredLicense()
    {
        var body = TestResponses.InvalidValidation("TEST-KEY").Replace("\"valid\":false", "\"valid\":true", StringComparison.Ordinal);
        var adapter = new CapturingAdapter { PostHandler = (_, _) => new HttpResponse(200, body) };
        using var client = new LicenseSeatClient(CreateOptions(), adapter);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ValidateAsync("TEST-KEY"));
        Assert.Equal("invalid_response", exception.Code);
    }

    [Fact]
    public async Task OfflineFallback_AcceptsOnlyFullyVerifiedBoundToken()
    {
        var fixture = CreateOfflineFixture();
        using var client = CreateOfflineClient(fixture.Response, fixture.PublicKey, new HttpResponse(0, "offline"));

        var result = await client.ValidateAsync("TEST-KEY");

        Assert.True(result.Valid);
        Assert.True(result.Offline);
        Assert.Single(result.ActiveEntitlements!);
        Assert.Equal("pro_feature", result.ActiveEntitlements![0].Key);
    }

    [Fact]
    public async Task OfflineFallback_ZeroDayPolicyDoesNotAuthorizeCachedToken()
    {
        var fixture = CreateOfflineFixture();
        using var client = CreateOfflineClient(
            fixture.Response,
            fixture.PublicKey,
            new HttpResponse(0, "offline"),
            maxOfflineDays: 0);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ValidateAsync("TEST-KEY"));

        Assert.True(exception.IsNetworkError);
    }

    [Fact]
    public async Task OfflineFallback_AcceptsRubySignedCrossLanguageFixture()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "ruby_signed_offline_token.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var publicKey = document.RootElement.GetProperty("public_key").GetString();
        var offlineToken = JsonSerializer.Deserialize<OfflineTokenResponse>(
            document.RootElement.GetProperty("offline_token").GetRawText());
        Assert.NotNull(publicKey);
        Assert.NotNull(offlineToken?.Token);

        var options = CreateOptions();
        options.ProductSlug = offlineToken!.Token!.ProductSlug;
        options.OfflineFallbackMode = OfflineFallbackMode.Always;
        options.MaxOfflineDays = 36_600;
        var adapter = new CapturingAdapter
        {
            PostHandler = (_, _) => new HttpResponse(0, "offline")
        };
        using var client = new LicenseSeatClient(options, adapter);
        var cache = GetCache(client);
        cache.SetLicense(new License
        {
            Key = offlineToken.Token.LicenseKey!,
            DeviceId = offlineToken.Token.Fingerprint!,
            Status = "active",
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            Product = new Product { Slug = offlineToken.Token.ProductSlug! }
        });
        cache.SetDeviceId(offlineToken.Token.Fingerprint!);
        cache.SetOfflineToken(offlineToken);
        cache.SetPublicKey(offlineToken.Signature!.KeyId!, publicKey!);

        var result = await client.ValidateAsync(offlineToken.Token.LicenseKey!);

        Assert.True(result.Valid, result.Code);
        Assert.True(result.Offline);
        Assert.Equal("pro", Assert.Single(result.ActiveEntitlements!).Key);
    }

    [Fact]
    public async Task OfflineFallback_RejectsTokenPayloadChangedAfterSigning()
    {
        var fixture = CreateOfflineFixture();
        fixture.Response.Token!.Entitlements![0].Key = "admin_feature";
        using var client = CreateOfflineClient(fixture.Response, fixture.PublicKey, new HttpResponse(0, "offline"));

        var result = await client.ValidateAsync("TEST-KEY");

        Assert.False(result.Valid);
        Assert.True(result.Offline);
    }

    [Fact]
    public async Task OfflineFallback_RejectsMissingSignatureMaterial()
    {
        var fixture = CreateOfflineFixture();
        fixture.Response.Signature!.Value = null;
        using var client = CreateOfflineClient(fixture.Response, fixture.PublicKey, new HttpResponse(0, "offline"));

        var result = await client.ValidateAsync("TEST-KEY");
        Assert.False(result.Valid);
    }

    [Fact]
    public async Task OfflineFallback_RejectsMissingPublicKey()
    {
        var fixture = CreateOfflineFixture();
        using var client = CreateOfflineClient(fixture.Response, null, new HttpResponse(0, "offline"));

        var result = await client.ValidateAsync("TEST-KEY");
        Assert.False(result.Valid);
        Assert.Equal("no_public_key", result.Code);
    }

    [Fact]
    public async Task OfflineFallback_RejectsSignedTokenForDifferentProduct()
    {
        var fixture = CreateOfflineFixture(productSlug: "other-product");
        using var client = CreateOfflineClient(fixture.Response, fixture.PublicKey, new HttpResponse(0, "offline"));

        var result = await client.ValidateAsync("TEST-KEY");
        Assert.False(result.Valid);
    }

    [Fact]
    public async Task OfflineFallback_RejectsSignedTokenForDifferentDevice()
    {
        var fixture = CreateOfflineFixture(fingerprint: "other-device");
        using var client = CreateOfflineClient(fixture.Response, fixture.PublicKey, new HttpResponse(0, "offline"));

        var result = await client.ValidateAsync("TEST-KEY");
        Assert.False(result.Valid);
    }

    [Fact]
    public async Task OfflineFallback_TreatsExactExpirationAsExpired()
    {
        var fixture = CreateOfflineFixture(expiration: DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var client = CreateOfflineClient(fixture.Response, fixture.PublicKey, new HttpResponse(0, "offline"));

        var result = await client.ValidateAsync("TEST-KEY");
        Assert.False(result.Valid);
        Assert.Equal("expired", result.Code);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(408)]
    [InlineData(503)]
    [InlineData(-1)]
    public async Task OfflineFallback_NeverOverridesAuthoritativeOrLocalSecurityFailure(int statusCode)
    {
        var fixture = CreateOfflineFixture();
        var response = new HttpResponse(
            statusCode,
            "{\"error\":{\"code\":\"rejected\",\"message\":\"rejected\"}}");
        using var client = CreateOfflineClient(fixture.Response, fixture.PublicKey, response);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.ValidateAsync("TEST-KEY"));
        Assert.Equal(statusCode, exception.StatusCode);
    }

    [Fact]
    public async Task DefaultAdapter_ScopesBearerCredentialToProtectedEndpoints()
    {
        var seen = new ConcurrentDictionary<string, string?>();
        using var httpClient = new HttpClient(new DelegateHandler(request =>
        {
            seen[request.RequestUri!.AbsolutePath] = request.Headers.Authorization?.ToString();
            return JsonResponse(HttpStatusCode.OK, "{}");
        }));
        using var adapter = new DefaultHttpClientAdapter(httpClient, CreateOptions());

        Assert.True((await adapter.GetAsync("https://api.test.com/api/v1/health")).IsSuccess);
        Assert.True((await adapter.GetAsync("https://api.test.com/api/v1/signing_keys/offline-v1")).IsSuccess);
        Assert.True((await adapter.GetAsync("https://api.test.com/api/v1/auth")).IsSuccess);

        Assert.Null(seen["/api/v1/health"]);
        Assert.Null(seen["/api/v1/signing_keys/offline-v1"]);
        Assert.Equal("Bearer test-api-key", seen["/api/v1/auth"]);
    }

    [Fact]
    public async Task DefaultAdapter_RejectsCrossOriginRequestBeforeSending()
    {
        var calls = 0;
        using var httpClient = new HttpClient(new DelegateHandler(_ =>
        {
            Interlocked.Increment(ref calls);
            return JsonResponse(HttpStatusCode.OK, "{}");
        }));
        using var adapter = new DefaultHttpClientAdapter(httpClient, CreateOptions());

        var response = await adapter.GetAsync("https://evil.example/api/v1/auth");

        Assert.Equal(-1, response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("https://api.test.com/api/v1/%zz")]
    [InlineData("https://api.test.com/api/v1/%2e%2e/auth")]
    [InlineData("https://api.test.com/api/v1/%252e%252e/auth")]
    [InlineData("https://api.test.com/api/v1/%2fadmin")]
    public async Task DefaultAdapter_RejectsAmbiguousEncodedUrlBeforeSending(string url)
    {
        var calls = 0;
        using var httpClient = new HttpClient(new DelegateHandler(_ =>
        {
            Interlocked.Increment(ref calls);
            return JsonResponse(HttpStatusCode.OK, "{}");
        }));
        using var adapter = new DefaultHttpClientAdapter(httpClient, CreateOptions());

        var response = await adapter.GetAsync(url);

        Assert.Equal(-1, response.StatusCode);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void DefaultAdapter_DoesNotExposeOpaqueCallerSuppliedHttpClientConstructor()
    {
        Assert.DoesNotContain(
            typeof(DefaultHttpClientAdapter).GetConstructors(),
            constructor => constructor.GetParameters().Any(
                parameter => parameter.ParameterType == typeof(HttpClient)));
    }

    [Fact]
    public async Task DefaultAdapter_RejectsRedirects()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://evil.example/");
            return response;
        }));
        using var adapter = new DefaultHttpClientAdapter(httpClient, CreateOptions());

        var response = await adapter.GetAsync("https://api.test.com/api/v1/auth");
        Assert.Equal(-1, response.StatusCode);
    }

    [Fact]
    public async Task DefaultAdapter_RejectsNonJsonSuccessfulResponse()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "text/plain")
            };
            return response;
        }));
        using var adapter = new DefaultHttpClientAdapter(httpClient, CreateOptions());

        var response = await adapter.GetAsync("https://api.test.com/api/v1/auth");
        Assert.Equal(-1, response.StatusCode);
    }

    [Fact]
    public async Task DefaultAdapter_RejectsInvalidUtf8Response()
    {
        using var httpClient = new HttpClient(new DelegateHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0xff, 0xfe })
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return response;
        }));
        using var adapter = new DefaultHttpClientAdapter(httpClient, CreateOptions());

        var response = await adapter.GetAsync("https://api.test.com/api/v1/auth");
        Assert.Equal(-1, response.StatusCode);
    }

    [Fact]
    public async Task DefaultAdapter_EnforcesTimeoutForSuppliedHttpClient()
    {
        var options = CreateOptions();
        options.HttpTimeout = TimeSpan.FromMilliseconds(50);
        using var httpClient = new HttpClient(new AsyncDelegateHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return JsonResponse(HttpStatusCode.OK, "{}");
            }));
        using var adapter = new DefaultHttpClientAdapter(httpClient, options);

        var response = await adapter.GetAsync("https://api.test.com/api/v1/auth");

        Assert.Equal(0, response.StatusCode);
        Assert.Equal("Network request timed out.", response.Body);
    }

    [Fact]
    public async Task ApiClient_DoesNotExposeRawErrorResponseOrUnsafeMessage()
    {
        const string secret = "SECRET-LICENSE-KEY";
        var adapter = new CapturingAdapter
        {
            GetHandler = _ => new HttpResponse(
                403,
                "{\"error\":{\"code\":\"denied\",\"message\":\"unsafe\\n" + secret + "\"},\"echo\":\"" + secret + "\"}")
        };
        using var client = new ApiClient(CreateOptions(), adapter);

        var exception = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<object>("/test"));

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("denied", exception.Code);
        Assert.Null(exception.ResponseBody);
        Assert.DoesNotContain(secret, exception.ToString());
    }

    [Fact]
    public async Task Dispose_CancelsInFlightOperationAndFutureOperationsFailClosed()
    {
        var adapter = new BlockingAdapter();
        var client = new LicenseSeatClient(CreateOptions(), adapter);
        var operation = client.ValidateAsync("TEST-KEY");
        await adapter.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        client.Dispose();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ValidateAsync("TEST-KEY"));
        Assert.Throws<ObjectDisposedException>(() => client.GetStatus());
    }

    [Fact]
    public void Options_RejectCredentialsQueriesFragmentsAndUnboundedValues()
    {
        var userInfo = CreateOptions();
        userInfo.ApiBaseUrl = "https://user:pass@api.test.com/api/v1";
        Assert.Throws<InvalidOperationException>(() => userInfo.Validate());

        var query = CreateOptions();
        query.ApiBaseUrl = "https://api.test.com/api/v1?tenant=secret";
        Assert.Throws<InvalidOperationException>(() => query.Validate());

        var retries = CreateOptions();
        retries.MaxRetries = 9;
        Assert.Throws<InvalidOperationException>(() => retries.Validate());

        var timeout = CreateOptions();
        timeout.HttpTimeout = TimeSpan.FromHours(1);
        Assert.Throws<InvalidOperationException>(() => timeout.Validate());
    }

    [Fact]
    public void SecurityValidation_RejectsControlsInvalidUnicodeAndAmbiguousJson()
    {
        Assert.False(SecurityValidation.IsSafeText("hello\nworld", 1, 100));
        Assert.False(SecurityValidation.IsSafeText("\ud800", 1, 100));
        Assert.False(SecurityValidation.TryParseRfc3339("2026-01-01T00:00:00", out _));
        Assert.Throws<JsonException>(() => SecurityValidation.ParseStrictJson("{\"a\":1,\"a\":2}", 1024));
    }

    private static LicenseSeatClient CreateOfflineClient(
        OfflineTokenResponse offlineToken,
        string? publicKey,
        HttpResponse onlineResponse,
        int maxOfflineDays = 30)
    {
        var options = CreateOptions();
        options.OfflineFallbackMode = OfflineFallbackMode.Always;
        options.MaxOfflineDays = maxOfflineDays;
        var adapter = new CapturingAdapter { PostHandler = (_, _) => onlineResponse };
        var client = new LicenseSeatClient(options, adapter);
        var cache = GetCache(client);
        cache.SetLicense(new License
        {
            Key = "TEST-KEY",
            DeviceId = "device-123",
            Status = "active",
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1),
            Product = new Product { Slug = "test-product" }
        });
        cache.SetDeviceId("device-123");
        cache.SetOfflineToken(offlineToken);
        if (publicKey != null)
        {
            cache.SetPublicKey(offlineToken.Signature!.KeyId!, publicKey);
        }

        return client;
    }

    private static LicenseCache GetCache(LicenseSeatClient client)
    {
        var field = typeof(LicenseSeatClient).GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<LicenseCache>(field!.GetValue(client));
    }

    private static OfflineFixture CreateOfflineFixture(
        string productSlug = "test-product",
        string fingerprint = "device-123",
        long? expiration = null)
    {
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = generator.GenerateKeyPair();
        var publicKey = (Ed25519PublicKeyParameters)keyPair.Public;
        var privateKey = (Ed25519PrivateKeyParameters)keyPair.Private;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var token = new OfflineToken
        {
            SchemaVersion = 1,
            LicenseKey = "TEST-KEY",
            ProductSlug = productSlug,
            PlanKey = "pro",
            Mode = "hardware_locked",
            SeatLimit = 1,
            Fingerprint = fingerprint,
            Iat = now - 10,
            Nbf = now - 10,
            Exp = expiration ?? now + 3600,
            LicenseExpiresAt = now + 7200,
            Kid = "offline-v1",
            Entitlements = new List<OfflineEntitlement>
            {
                new OfflineEntitlement { Key = "pro_feature", ExpiresAt = now + 1800 }
            },
            Metadata = new Dictionary<string, object>()
        };

        var canonical = JsonSerializer.Serialize(token, OfflineJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var signer = new Ed25519Signer();
        signer.Init(true, privateKey);
        signer.BlockUpdate(bytes, 0, bytes.Length);
        var signature = Convert.ToBase64String(signer.GenerateSignature());

        return new OfflineFixture(
            new OfflineTokenResponse
            {
                Object = "offline_token",
                Token = token,
                Signature = new OfflineTokenSignature
                {
                    Algorithm = "Ed25519",
                    KeyId = "offline-v1",
                    Value = signature
                },
                Canonical = canonical
            },
            Convert.ToBase64String(publicKey.GetEncoded()));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private sealed record OfflineFixture(OfflineTokenResponse Response, string PublicKey);

    private sealed class CapturingAdapter : IHttpClientAdapter
    {
        internal Func<string, HttpResponse>? GetHandler { get; set; }
        internal Func<string, string, HttpResponse>? PostHandler { get; set; }
        internal ConcurrentQueue<string> Gets { get; } = new ConcurrentQueue<string>();
        internal ConcurrentQueue<(string Url, string Body)> Posts { get; } = new ConcurrentQueue<(string, string)>();

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Gets.Enqueue(url);
            return Task.FromResult(GetHandler?.Invoke(url) ?? new HttpResponse(200, "{}"));
        }

        public Task<HttpResponse> PostAsync(
            string url,
            string jsonBody,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Posts.Enqueue((url, jsonBody));
            return Task.FromResult(PostHandler?.Invoke(url, jsonBody) ?? new HttpResponse(200, "{}"));
        }
    }

    private sealed class DelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        internal DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _handler(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private sealed class AsyncDelegateHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        internal AsyncDelegateHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await _handler(request, cancellationToken);
            response.RequestMessage = request;
            return response;
        }
    }

    private sealed class BlockingAdapter : IHttpClientAdapter
    {
        internal TaskCompletionSource<bool> Started { get; } =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return SendAsync(cancellationToken);
        }

        public Task<HttpResponse> PostAsync(
            string url,
            string jsonBody,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(cancellationToken);
        }

        private async Task<HttpResponse> SendAsync(CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponse(200, "{}");
        }
    }
}
