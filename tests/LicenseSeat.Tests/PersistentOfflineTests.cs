using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace LicenseSeat.Tests;

/// <summary>
/// Issue #25: activations and signed offline tokens survive process and OS
/// restarts, the activated fingerprint stays authoritative across OS updates,
/// and nothing read back from storage grants authority on its own.
/// </summary>
public sealed class PersistentOfflineTests : IDisposable
{
    private const string LicenseKey = "TEST-KEY";
    private const string ProductSlug = "test-product";
    private const string KeyId = "offline-v1";
    private const string ThisMachine = "anchor-this-machine";
    private const string OtherMachine = "anchor-other-machine";

    private static readonly JsonSerializerOptions TokenJsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        MaxDepth = SecurityValidation.MaxJsonDepth,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _directory;
    private readonly Ed25519PrivateKeyParameters _privateKey;
    private readonly string _publicKey;

    public PersistentOfflineTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "licenseseat-tests", Guid.NewGuid().ToString("N"));
        var generator = new Ed25519KeyPairGenerator();
        generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = generator.GenerateKeyPair();
        _privateKey = (Ed25519PrivateKeyParameters)keyPair.Private;
        _publicKey = Convert.ToBase64String(((Ed25519PublicKeyParameters)keyPair.Public).GetEncoded());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    // ------------------------------------------------------------------
    // The customer scenario: activate online, restart offline.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ColdStart_OfflineAfterRestart_VerifiesPersistedTokenWithPinnedKey()
    {
        var online = new FakeServer(this);
        using (var first = CreateClient(online, pinKey: true))
        {
            var ready = WaitFor(first, LicenseSeatEvents.OfflineLicenseReady);
            await first.ActivateAsync(LicenseKey);
            await ready;
        }

        using var second = CreateClient(FakeServer.Unreachable(), pinKey: true);
        Assert.Equal(LicenseStatusType.Pending, second.GetStatus().StatusType);
        Assert.NotNull(second.GetCurrentLicense());

        var result = await second.ValidateAsync(LicenseKey);

        Assert.True(result.Valid, result.Code);
        Assert.True(result.Offline);
        Assert.True(second.HasEntitlement("pro_feature"));
        Assert.Equal(LicenseStatusType.OfflineValid, second.GetStatus().StatusType);
    }

    [Fact]
    public async Task ColdStart_WithoutPinnedKey_FailsClosedBecauseKeysAreNeverPersisted()
    {
        var online = new FakeServer(this);
        using (var first = CreateClient(online, pinKey: false))
        {
            var ready = WaitFor(first, LicenseSeatEvents.OfflineLicenseReady);
            await first.ActivateAsync(LicenseKey);
            await ready;
        }

        Assert.DoesNotContain(Directory.GetFiles(_directory), path => path.Contains("key", StringComparison.OrdinalIgnoreCase));

        using var second = CreateClient(FakeServer.Unreachable(), pinKey: false);
        var result = await second.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("no_public_key", result.Code);
    }

    [Fact]
    public async Task ColdStart_RejectsAttackerSignedTokenAndKeyPlantedInStorage()
    {
        // An attacker who can write the store can plant a token signed by
        // their own key. Without a stored key there is nothing to plant it
        // next to: the pinned key decides.
        Seed(SignToken(SignedBy.Attacker));

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("signature_invalid", result.Code);
    }

    [Fact]
    public async Task ColdStart_OutsideMaxOfflineDays_IsRejected()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Seed(SignToken(iat: now - (8 * 86400), exp: now + 86400));

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true, maxOfflineDays: 7);
        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("grace_period_expired", result.Code);
    }

    [Fact]
    public async Task ColdStart_TokenNeverOutlivesLicenseExpiry()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Seed(SignToken(iat: now - 7200, exp: now + 86400, licenseExpiresAt: now - 60));

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("license_expired", result.Code);
    }

    [Fact]
    public async Task ClockRollback_IsDetectedAcrossRestarts()
    {
        Seed(SignToken());
        var store = new FileLicenseStore(_directory);
        var cache = new LicenseCache("licenseseat_", store, () => ThisMachine, null);
        cache.SetLastSeenTimestamp(DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds());

        // A fresh process sees "now" two days before a time it already saw.
        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("clock_tamper", result.Code);
    }

    [Fact]
    public async Task SuccessfulOfflineVerification_PersistsTheRollbackFloor()
    {
        Seed(SignToken());
        using (var client = CreateClient(FakeServer.Unreachable(), pinKey: true))
        {
            Assert.True((await client.ValidateAsync(LicenseKey)).Valid);
        }

        var reloaded = new LicenseCache("licenseseat_", new FileLicenseStore(_directory), () => ThisMachine, null);
        Assert.InRange(
            reloaded.GetLastSeenTimestamp(),
            DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task ColdStart_FolderCopiedToAnotherMachine_CarriesNoActivation()
    {
        Seed(SignToken());

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true, anchor: OtherMachine);

        Assert.Null(client.GetCurrentLicense());
        Assert.Equal(LicenseStatusType.Inactive, client.GetStatus().StatusType);
        var result = await client.ValidateAsync(LicenseKey);
        Assert.False(result.Valid);
    }

    [Fact]
    public async Task AuthoritativeOnlineRejection_DeletesThePersistedToken()
    {
        Seed(SignToken());
        var server = new FakeServer(this) { ValidationBody = TestResponses.InvalidValidation(LicenseKey, ProductSlug) };

        using (var client = CreateClient(server, pinKey: true))
        {
            var result = await client.ValidateAsync(LicenseKey);
            Assert.False(result.Valid);
            Assert.False(result.Offline);
        }

        Assert.False(File.Exists(Path.Combine(_directory, "licenseseat_offline_token.json")));
        using var offline = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var offlineResult = await offline.ValidateAsync(LicenseKey);
        Assert.False(offlineResult.Valid);
        Assert.Equal("no_offline_license", offlineResult.Code);
    }

    [Fact]
    public async Task RestoredSession_RefreshesOfflineTokenAfterOnlineValidation()
    {
        Seed(SignToken());
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: false, autoInitialize: true);
        var ready = WaitFor(client, LicenseSeatEvents.OfflineLicenseReady);

        await ready;

        Assert.Contains(server.Posts, url => url.EndsWith("/licenses/validate", StringComparison.Ordinal));
        Assert.Contains(server.Posts, url => url.EndsWith("/licenses/offline-token", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------
    // Fingerprint stability (the seat-burning regression).
    // ------------------------------------------------------------------

    [Fact]
    public async Task Activation_ReusesPersistedFingerprintAfterOsUpdateChangesTheDerivedOne()
    {
        // The install activated before a Windows update with a fingerprint
        // that DeviceIdentifier.Generate() no longer produces.
        const string ActivatedBeforeUpdate = "legacy0fingerprint0before0os0update";
        Seed(SignToken(fingerprint: ActivatedBeforeUpdate), fingerprint: ActivatedBeforeUpdate);
        Assert.NotEqual(ActivatedBeforeUpdate, DeviceIdentifier.Generate());

        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true, configuredDeviceId: null);
        await client.ActivateAsync(LicenseKey);

        Assert.Equal(ActivatedBeforeUpdate, server.LastFingerprint);
        Assert.Equal(ActivatedBeforeUpdate, client.GetCurrentLicense()!.DeviceId);
    }

    [Fact]
    public async Task Activation_WithoutPersistedFingerprint_UsesTheUnchangedLegacyDerivation()
    {
        // Existing installs (pre-0.6.0, no store) activated with Generate().
        // Upgrading must present the same fingerprint, or every upgrade burns a seat.
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true, configuredDeviceId: null);
        await client.ActivateAsync(LicenseKey);

        Assert.Equal(DeviceIdentifier.Generate(), server.LastFingerprint);
        var reloaded = new LicenseCache("licenseseat_", new FileLicenseStore(_directory), () => ThisMachine, null);
        Assert.Equal(DeviceIdentifier.Generate(), reloaded.GetDeviceId());
    }

    [Fact]
    public async Task Activation_ExplicitDeviceIdStillWinsOverPersistedFingerprint()
    {
        Seed(SignToken(fingerprint: "persisted-fingerprint"), fingerprint: "persisted-fingerprint");
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true, configuredDeviceId: "configured-device");

        await client.ActivateAsync(LicenseKey);

        Assert.Equal("configured-device", server.LastFingerprint);
    }

    [Fact]
    public void LegacyFingerprintDerivation_IsPinned()
    {
        // Golden vector: SHA-256, first 16 bytes, lowercase hex. Changing this
        // re-fingerprints every existing install.
        Assert.Equal(
            "44bae8042dfecd4448362d1ed54ba2a7",
            DeviceIdentifier.FromInput("HOST:user:Microsoft Windows NT 10.0.19045.0"));
        Assert.Equal(
            DeviceIdentifier.FromInput($"{Environment.MachineName}:{Environment.UserName}:{Environment.OSVersion}"),
            DeviceIdentifier.Generate());
    }

    [Fact]
    public void DeviceAnchor_IgnoresOsVersionAndDiffersFromTheFingerprint()
    {
        var anchor = DeviceIdentifier.ComputeAnchor();

        Assert.Equal(
            DeviceIdentifier.FromInput($"licenseseat-device-anchor-v1:{Environment.MachineName}:{Environment.UserName}"),
            anchor);
        Assert.NotEqual(DeviceIdentifier.Generate(), anchor);
    }

    // ------------------------------------------------------------------
    // Options.
    // ------------------------------------------------------------------

    [Fact]
    public void Options_PinnedKeyRequiresKeyIdAndCanonicalKey()
    {
        var keyOnly = Options(pinKey: false);
        keyOnly.SigningPublicKey = _publicKey;
        Assert.Throws<InvalidOperationException>(() => keyOnly.Validate());

        var idOnly = Options(pinKey: false);
        idOnly.SigningKeyId = KeyId;
        Assert.Throws<InvalidOperationException>(() => idOnly.Validate());

        var badKey = Options(pinKey: true);
        badKey.SigningPublicKey = Convert.ToBase64String(new byte[31]) + "A";
        Assert.Throws<InvalidOperationException>(() => badKey.Validate());

        var badId = Options(pinKey: true);
        badId.SigningKeyId = "../key";
        Assert.Throws<InvalidOperationException>(() => badId.Validate());

        var good = Options(pinKey: true);
        good.Validate();
        var clone = good.Clone();
        Assert.Same(good.LicenseStore, clone.LicenseStore);
        Assert.Equal(_publicKey, clone.SigningPublicKey);
        Assert.Equal(KeyId, clone.SigningKeyId);
    }

    [Fact]
    public async Task PinnedKey_IsUsedInsteadOfFetchingAndRejectsAMismatchedServerToken()
    {
        var server = new FakeServer(this) { SignOfflineTokensWith = SignedBy.Attacker };
        using var client = CreateClient(server, pinKey: true);
        var errors = WaitFor(client, LicenseSeatEvents.OfflineLicenseFetched);

        await client.ActivateAsync(LicenseKey);
        await errors;
        await Task.Delay(50);

        Assert.Empty(server.Gets);
        Assert.False(File.Exists(Path.Combine(_directory, "licenseseat_offline_token.json")));
    }

    // ------------------------------------------------------------------
    // Helpers.
    // ------------------------------------------------------------------

    private enum SignedBy
    {
        Vendor,
        Attacker
    }

    private LicenseSeatClientOptions Options(bool pinKey, int maxOfflineDays = 30, bool autoInitialize = false, string? configuredDeviceId = "device-123")
    {
        return new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ProductSlug = ProductSlug,
            ApiBaseUrl = "https://api.test.com/api/v1",
            AutoInitialize = autoInitialize,
            AutoValidateInterval = TimeSpan.Zero,
            HeartbeatInterval = TimeSpan.Zero,
            MaxRetries = 0,
            DeviceId = configuredDeviceId,
            OfflineFallbackMode = OfflineFallbackMode.NetworkOnly,
            MaxOfflineDays = maxOfflineDays,
            LicenseStore = new FileLicenseStore(_directory),
            SigningPublicKey = pinKey ? _publicKey : null,
            SigningKeyId = pinKey ? KeyId : null
        };
    }

    private LicenseSeatClient CreateClient(
        FakeServer server,
        bool pinKey,
        int maxOfflineDays = 30,
        string anchor = ThisMachine,
        bool autoInitialize = false,
        string? configuredDeviceId = "device-123")
    {
        return new LicenseSeatClient(Options(pinKey, maxOfflineDays, autoInitialize, configuredDeviceId), server, () => anchor);
    }

    /// <summary>Writes the state a previous process on this machine would have left behind.</summary>
    private void Seed(OfflineTokenResponse token, string fingerprint = "device-123")
    {
        var cache = new LicenseCache("licenseseat_", new FileLicenseStore(_directory), () => ThisMachine, null);
        cache.SetLicense(new License
        {
            Key = LicenseKey,
            DeviceId = fingerprint,
            Status = "active",
            Product = new Product { Slug = ProductSlug }
        });
        cache.SetDeviceId(fingerprint);
        cache.SetOfflineToken(token);
    }

    private OfflineTokenResponse SignToken(
        SignedBy signedBy = SignedBy.Vendor,
        long? iat = null,
        long? exp = null,
        long? licenseExpiresAt = null,
        string fingerprint = "device-123")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var issued = iat ?? now - 10;
        var token = new OfflineToken
        {
            SchemaVersion = 1,
            LicenseKey = LicenseKey,
            ProductSlug = ProductSlug,
            PlanKey = "pro",
            Mode = "hardware_locked",
            SeatLimit = 1,
            Fingerprint = fingerprint,
            Iat = issued,
            Nbf = issued,
            Exp = exp ?? now + 86400,
            LicenseExpiresAt = licenseExpiresAt ?? now + (30 * 86400),
            Kid = KeyId,
            Entitlements = new List<OfflineEntitlement> { new OfflineEntitlement { Key = "pro_feature" } },
            Metadata = new Dictionary<string, object>()
        };

        var canonical = JsonSerializer.Serialize(token, TokenJsonOptions);
        var signer = new Ed25519Signer();
        if (signedBy == SignedBy.Vendor)
        {
            signer.Init(true, _privateKey);
        }
        else
        {
            var generator = new Ed25519KeyPairGenerator();
            generator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
            signer.Init(true, generator.GenerateKeyPair().Private);
        }

        var bytes = Encoding.UTF8.GetBytes(canonical);
        signer.BlockUpdate(bytes, 0, bytes.Length);

        return new OfflineTokenResponse
        {
            Object = "offline_token",
            Token = token,
            Signature = new OfflineTokenSignature
            {
                Algorithm = "Ed25519",
                KeyId = KeyId,
                Value = Convert.ToBase64String(signer.GenerateSignature())
            },
            Canonical = canonical
        };
    }

    private static Task<bool> WaitFor(LicenseSeatClient client, string eventName)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Events.On(eventName, _ => completion.TrySetResult(true));
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class FakeServer : IHttpClientAdapter
    {
        private readonly PersistentOfflineTests? _owner;
        private readonly bool _unreachable;

        internal FakeServer(PersistentOfflineTests owner)
        {
            _owner = owner;
        }

        private FakeServer()
        {
            _unreachable = true;
        }

        internal static FakeServer Unreachable() => new FakeServer();

        internal string? ValidationBody { get; set; }

        internal SignedBy SignOfflineTokensWith { get; set; } = SignedBy.Vendor;

        internal string? LastFingerprint { get; private set; }

        internal ConcurrentQueue<string> Posts { get; } = new ConcurrentQueue<string>();

        internal ConcurrentQueue<string> Gets { get; } = new ConcurrentQueue<string>();

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            Gets.Enqueue(url);
            if (_unreachable)
            {
                return Task.FromResult(new HttpResponse(0, "offline"));
            }

            var body = JsonSerializer.Serialize(new
            {
                @object = "signing_key",
                key_id = KeyId,
                algorithm = "Ed25519",
                public_key = _owner!._publicKey,
                created_at = "2026-01-01T00:00:00Z",
                status = "active"
            });
            return Task.FromResult(new HttpResponse(200, body));
        }

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            Posts.Enqueue(url);
            if (_unreachable)
            {
                return Task.FromResult(new HttpResponse(0, "offline"));
            }

            using var request = JsonDocument.Parse(jsonBody);
            var fingerprint = request.RootElement.TryGetProperty("fingerprint", out var value) ? value.GetString() : null;

            if (url.EndsWith("/licenses/activate", StringComparison.Ordinal))
            {
                LastFingerprint = fingerprint;
                return Task.FromResult(new HttpResponse(200, TestResponses.Activation(LicenseKey, fingerprint!, ProductSlug)));
            }

            if (url.EndsWith("/licenses/validate", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponse(200, ValidationBody ?? TestResponses.ValidValidation(LicenseKey, ProductSlug)));
            }

            if (url.EndsWith("/licenses/offline-token", StringComparison.Ordinal))
            {
                var token = _owner!.SignToken(SignOfflineTokensWith, fingerprint: fingerprint!);
                return Task.FromResult(new HttpResponse(200, JsonSerializer.Serialize(token, TokenJsonOptions)));
            }

            return Task.FromResult(new HttpResponse(404, "{\"error\":{\"code\":\"not_found\",\"message\":\"not found\"}}"));
        }
    }
}
