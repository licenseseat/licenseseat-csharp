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
    private const string Configured = "device-123";

    private static readonly DeviceInputs BeforeUpdate = new DeviceInputs("GAMING-PC", "denis", "Microsoft Windows NT 10.0.19045.0");
    private static readonly DeviceInputs AfterUpdate = new DeviceInputs("GAMING-PC", "denis", "Microsoft Windows NT 10.0.22631.0");
    private static readonly DeviceInputs OtherMachine = new DeviceInputs("OTHER-PC", "mallory", "Microsoft Windows NT 10.0.19045.0");

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

    private static string Derived(DeviceInputs inputs) => DeviceIdentifier.FromInputs(inputs.MachineName, inputs.UserName, inputs.OsVersion);

    // ------------------------------------------------------------------
    // The customer scenario: activate online, restart offline.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ColdStart_OfflineAfterRestart_VerifiesPersistedTokenWithPinnedKey()
    {
        using (var first = CreateClient(new FakeServer(this), pinKey: true))
        {
            var ready = WaitFor(first, LicenseSeatEvents.OfflineLicenseReady);
            await first.ActivateAsync(LicenseKey);
            await ready;
        }

        using var second = CreateClient(FakeServer.Unreachable(), pinKey: true);
        Assert.Equal(LicenseStatusType.Pending, second.GetStatus().StatusType);
        Assert.False(second.GetCurrentLicense()!.IsActive);

        var result = await second.ValidateAsync(LicenseKey);

        Assert.True(result.Valid, result.Code);
        Assert.True(result.Offline);
        Assert.True(second.HasEntitlement("pro_feature"));
        Assert.Equal(LicenseStatusType.OfflineValid, second.GetStatus().StatusType);
    }

    [Fact]
    public async Task ColdStart_AfterOsUpdate_DerivedFingerprintIsRestoredAndVerifiesOffline()
    {
        using (var first = CreateClient(new FakeServer(this), pinKey: true, configuredDeviceId: null, environment: BeforeUpdate))
        {
            var ready = WaitFor(first, LicenseSeatEvents.OfflineLicenseReady);
            await first.ActivateAsync(LicenseKey);
            await ready;
        }

        using var second = CreateClient(FakeServer.Unreachable(), pinKey: true, configuredDeviceId: null, environment: AfterUpdate);
        var result = await second.ValidateAsync(LicenseKey);

        Assert.True(result.Valid, result.Code);
        Assert.Equal(Derived(BeforeUpdate), second.GetCurrentLicense()!.DeviceId);
    }

    [Fact]
    public async Task ColdStart_WithoutPinnedKey_FailsClosedBecauseKeysAreNeverPersisted()
    {
        using (var first = CreateClient(new FakeServer(this), pinKey: false))
        {
            var ready = WaitFor(first, LicenseSeatEvents.OfflineLicenseReady);
            await first.ActivateAsync(LicenseKey);
            await ready;
        }

        Assert.DoesNotContain(Directory.GetFiles(_directory), path => File.ReadAllText(path).Contains(_publicKey, StringComparison.Ordinal));

        using var second = CreateClient(FakeServer.Unreachable(), pinKey: false);
        var result = await second.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("no_public_key", result.Code);
    }

    [Fact]
    public async Task ColdStart_RejectsAttackerSignedTokenPlantedInStorage()
    {
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
    public async Task ColdStart_MalformedPersistedTokenFailsClosedWithoutThrowing()
    {
        Seed(SignToken());
        var path = Path.Combine(_directory, "licenseseat_offline_token.json");
        var original = File.ReadAllText(path);
        var malformed = original.Replace("\"entitlements\":[{", "\"entitlements\":[null,{", StringComparison.Ordinal);
        Assert.NotEqual(original, malformed);
        File.WriteAllText(path, malformed);

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
    }

    // ------------------------------------------------------------------
    // Clock rollback.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ClockRollback_IsDetectedAcrossRestarts()
    {
        Seed(SignToken());
        new LicenseCache("licenseseat_", Store(), () => BeforeUpdate, Configured, null)
            .SetLastSeenTimestamp(DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds());

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("clock_tamper", result.Code);
    }

    [Fact]
    public async Task DeletingTheRollbackFloor_DoesNotRestoreTheToken()
    {
        Seed(SignToken());
        File.Delete(Path.Combine(_directory, "licenseseat_last_seen.json"));

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("no_offline_license", result.Code);
    }

    [Fact]
    public async Task SuccessfulOfflineVerification_PersistsTheRollbackFloor()
    {
        Seed(SignToken());
        using (var client = CreateClient(FakeServer.Unreachable(), pinKey: true))
        {
            Assert.True((await client.ValidateAsync(LicenseKey)).Valid);
        }

        var reloaded = new LicenseCache("licenseseat_", Store(), () => BeforeUpdate, Configured, null);
        Assert.InRange(
            reloaded.GetLastSeenTimestamp(),
            DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());
    }

    // ------------------------------------------------------------------
    // Device binding.
    // ------------------------------------------------------------------

    [Fact]
    public async Task ColdStart_FolderCopiedToAnotherMachine_CarriesNoActivation()
    {
        SeedDerived(BeforeUpdate);

        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true, configuredDeviceId: null, environment: OtherMachine);

        Assert.Null(client.GetCurrentLicense());
        Assert.Equal(LicenseStatusType.Inactive, client.GetStatus().StatusType);
        Assert.False((await client.ValidateAsync(LicenseKey)).Valid);
    }

    [Fact]
    public void ColdStart_RewrittenDeviceRecordCannotAdoptAnotherMachinesFingerprint()
    {
        // The attacker copies a genuine install's files and edits the device
        // record any way they like: the stored fingerprint must re-derive from
        // this machine's name and user, which needs a SHA-256 preimage.
        SeedDerived(BeforeUpdate);
        var path = Path.Combine(_directory, "licenseseat_device.json");
        foreach (var osVersion in new[] { OtherMachine.OsVersion, "anything", BeforeUpdate.OsVersion })
        {
            var record = JsonSerializer.Serialize(new { version = 1, fingerprint = Derived(BeforeUpdate), os_version = osVersion });
            File.WriteAllText(path, record);

            var cache = new LicenseCache("licenseseat_", Store(), () => OtherMachine, null, null);
            Assert.Null(cache.GetLicense());
            Assert.Null(cache.GetDeviceId());
        }
    }

    // ------------------------------------------------------------------
    // Revocation and session lifecycle.
    // ------------------------------------------------------------------

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
    public async Task AuthoritativeOnlineRejection_TombstonesTheTokenWhenDeletionFails()
    {
        Seed(SignToken());
        var store = new UndeletableStore(Store());
        var server = new FakeServer(this) { ValidationBody = TestResponses.InvalidValidation(LicenseKey, ProductSlug) };

        using (var client = CreateClient(server, pinKey: true, store: store))
        {
            Assert.False((await client.ValidateAsync(LicenseKey)).Valid);
        }

        using var offline = CreateClient(FakeServer.Unreachable(), pinKey: true);
        var result = await offline.ValidateAsync(LicenseKey);
        Assert.False(result.Valid);
        Assert.Equal("no_offline_license", result.Code);
    }

    [Fact]
    public async Task InFlightOfflineSync_CannotWriteBackAfterARejection()
    {
        var server = new FakeServer(this) { HoldOfflineTokens = true };
        using var client = CreateClient(server, pinKey: true);
        await client.ActivateAsync(LicenseKey);
        await server.OfflineTokenRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));

        server.ValidationBody = TestResponses.InvalidValidation(LicenseKey, ProductSlug);
        Assert.False((await client.ValidateAsync(LicenseKey)).Valid);
        var fetched = WaitFor(client, LicenseSeatEvents.OfflineLicenseFetched);
        server.ReleaseOfflineTokens.SetResult(true);
        await fetched;
        await Task.Delay(100);

        Assert.Null(GetCacheToken(client));
        Assert.False(File.Exists(Path.Combine(_directory, "licenseseat_offline_token.json")));
    }

    [Fact]
    public async Task OnlineValidation_RefreshesAStaleTokenWithoutRestart()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Seed(SignToken(iat: now - (4 * 86400)));
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true);
        var ready = WaitFor(client, LicenseSeatEvents.OfflineLicenseReady);

        Assert.True((await client.ValidateAsync(LicenseKey)).Valid);
        await ready;

        Assert.InRange(GetCacheToken(client)!.Token!.Iat, now - 60, now + 60);
    }

    [Fact]
    public async Task ServerReportedShorterLicenseExpiry_BoundsOfflineUseOfAnOlderToken()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Seed(SignToken(licenseExpiresAt: now + (30 * 86400)));
        var shortened = DateTimeOffset.FromUnixTimeSeconds(now + 2).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var validBody = TestResponses.ValidValidation(LicenseKey, ProductSlug);
        var shortenedBody = validBody.Replace("\"status\":\"active\",", $"\"status\":\"active\",\"expires_at\":\"{shortened}\",", StringComparison.Ordinal);
        Assert.NotEqual(validBody, shortenedBody);
        var server = new FakeServer(this)
        {
            // The refresh fails, so only the older token is available offline.
            OfflineTokensUnavailable = true,
            ValidationBody = shortenedBody
        };
        using var client = CreateClient(server, pinKey: true);
        Assert.True((await client.ValidateAsync(LicenseKey)).Valid);

        server.GoOffline();
        while (DateTimeOffset.UtcNow.ToUnixTimeSeconds() < now + 2)
        {
            await Task.Delay(100);
        }

        var result = await client.ValidateAsync(LicenseKey);

        Assert.False(result.Valid);
        Assert.Equal("license_expired", result.Code);
    }

    [Fact]
    public async Task RestoredSession_FetchesSigningKeyAndRefreshesTokenAfterOnlineValidation()
    {
        Seed(SignToken());
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: false, autoInitialize: true);
        var ready = WaitFor(client, LicenseSeatEvents.OfflineLicenseReady);

        await ready;

        Assert.Contains(server.Posts, url => url.EndsWith("/licenses/validate", StringComparison.Ordinal));
        Assert.Contains(server.Posts, url => url.EndsWith("/licenses/offline-token", StringComparison.Ordinal));
        Assert.Contains(server.Gets, url => url.EndsWith("/signing_keys/" + KeyId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OfflineStatusAndEntitlements_LapseAtTheAuthorityDeadline()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Seed(SignToken(exp: now + 2));
        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);
        Assert.True((await client.ValidateAsync(LicenseKey)).Valid);
        Assert.True(client.HasEntitlement("pro_feature"));

        while (DateTimeOffset.UtcNow.ToUnixTimeSeconds() < now + 2)
        {
            await Task.Delay(100);
        }

        Assert.Equal(LicenseStatusType.OfflineInvalid, client.GetStatus().StatusType);
        Assert.False(client.HasEntitlement("pro_feature"));
    }

    [Fact]
    public async Task OfflineFallback_IsBoundToTheRequestedLicenseAndDevice()
    {
        Seed(SignToken());
        using var client = CreateClient(FakeServer.Unreachable(), pinKey: true);

        var otherLicense = await client.ValidateAsync("OTHER-KEY");
        var otherDevice = await client.ValidateAsync(LicenseKey, new ValidationOptions { DeviceId = "some-other-device" });

        Assert.False(otherLicense.Valid);
        Assert.Equal("license_mismatch", otherLicense.Code);
        Assert.False(otherDevice.Valid);
        Assert.Equal("device_mismatch", otherDevice.Code);
    }

    // ------------------------------------------------------------------
    // Fingerprint stability (the seat-burning regression).
    // ------------------------------------------------------------------

    [Fact]
    public async Task Activation_ReusesPersistedFingerprintAfterOsUpdateChangesTheDerivedOne()
    {
        SeedDerived(BeforeUpdate);
        Assert.NotEqual(Derived(BeforeUpdate), Derived(AfterUpdate));

        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true, configuredDeviceId: null, environment: AfterUpdate);
        await client.ActivateAsync(LicenseKey);

        Assert.Equal(Derived(BeforeUpdate), server.LastFingerprint);
    }

    [Fact]
    public async Task Activation_WithoutPersistedFingerprint_UsesTheUnchangedLegacyDerivation()
    {
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true, configuredDeviceId: null, environment: BeforeUpdate);
        await client.ActivateAsync(LicenseKey);

        Assert.Equal(Derived(BeforeUpdate), server.LastFingerprint);
        var reloaded = new LicenseCache("licenseseat_", Store(), () => AfterUpdate, null, null);
        Assert.Equal(Derived(BeforeUpdate), reloaded.GetDeviceId());
    }

    [Fact]
    public async Task Activation_RefusesToDeriveANewDeviceWhileTheStoredOneIsUnreadable()
    {
        SeedDerived(BeforeUpdate);
        var flaky = new FlakyReadStore(Store(), "licenseseat_device", failures: 2);
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true, configuredDeviceId: null, environment: AfterUpdate, store: flaky);

        var error = await Assert.ThrowsAsync<LicenseException>(() => client.ActivateAsync(LicenseKey));
        Assert.Equal("device_identity_unavailable", error.ErrorCode);
        Assert.Null(server.LastFingerprint);

        await client.ActivateAsync(LicenseKey);
        Assert.Equal(Derived(BeforeUpdate), server.LastFingerprint);
    }

    [Fact]
    public async Task Activation_ExplicitDeviceIdStillWinsOverPersistedFingerprint()
    {
        SeedDerived(BeforeUpdate);
        var server = new FakeServer(this);
        using var client = CreateClient(server, pinKey: true, configuredDeviceId: "configured-device", environment: BeforeUpdate);

        await client.ActivateAsync(LicenseKey);

        Assert.Equal("configured-device", server.LastFingerprint);
    }

    [Fact]
    public async Task WithoutAStore_ActivationFingerprintSelectionIsUnchanged()
    {
        var server = new FakeServer(this);
        var options = Options(pinKey: true, configuredDeviceId: null);
        options.LicenseStore = null;
        using var client = new LicenseSeatClient(options, server, () => BeforeUpdate);

        await client.ActivateAsync(LicenseKey, new ActivationOptions { DeviceId = "custom-device-for-this-call" });
        await client.ActivateAsync(LicenseKey);

        Assert.Equal(Derived(BeforeUpdate), server.LastFingerprint);
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
            "44bae8042dfecd4448362d1ed54ba2a7",
            DeviceIdentifier.FromInputs("HOST", "user", "Microsoft Windows NT 10.0.19045.0"));
        Assert.Equal(
            DeviceIdentifier.FromInput($"{Environment.MachineName}:{Environment.UserName}:{Environment.OSVersion}"),
            DeviceIdentifier.Generate());
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
        var fetched = WaitFor(client, LicenseSeatEvents.OfflineLicenseFetched);

        await client.ActivateAsync(LicenseKey);
        await fetched;
        await Task.Delay(100);

        Assert.Empty(server.Gets);
        Assert.Null(GetCacheToken(client));
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

    private FileLicenseStore Store() => new FileLicenseStore(_directory);

    private LicenseSeatClientOptions Options(bool pinKey, int maxOfflineDays = 30, bool autoInitialize = false, string? configuredDeviceId = Configured, ILicenseStore? store = null)
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
            LicenseStore = store ?? Store(),
            SigningPublicKey = pinKey ? _publicKey : null,
            SigningKeyId = pinKey ? KeyId : null
        };
    }

    private LicenseSeatClient CreateClient(
        FakeServer server,
        bool pinKey,
        int maxOfflineDays = 30,
        bool autoInitialize = false,
        string? configuredDeviceId = Configured,
        DeviceInputs? environment = null,
        ILicenseStore? store = null)
    {
        var inputs = environment ?? BeforeUpdate;
        return new LicenseSeatClient(Options(pinKey, maxOfflineDays, autoInitialize, configuredDeviceId, store), server, () => inputs);
    }

    /// <summary>State a previous process left behind, for an app that configures DeviceId.</summary>
    private void Seed(OfflineTokenResponse token)
    {
        var cache = new LicenseCache("licenseseat_", Store(), () => BeforeUpdate, Configured, null);
        cache.SetLicense(new License { Key = LicenseKey, DeviceId = Configured, Status = "active" });
        cache.SetDeviceId(Configured);
        cache.SetOfflineToken(token);
        cache.SetLastSeenTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 5);
    }

    /// <summary>State a previous process left behind, for an app using the default fingerprint.</summary>
    private void SeedDerived(DeviceInputs inputs)
    {
        var fingerprint = Derived(inputs);
        var cache = new LicenseCache("licenseseat_", Store(), () => inputs, null, null);
        cache.SetLicense(new License { Key = LicenseKey, DeviceId = fingerprint, Status = "active" });
        cache.SetDeviceId(fingerprint, inputs.OsVersion);
        cache.SetOfflineToken(SignToken(fingerprint: fingerprint));
        cache.SetLastSeenTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 5);
    }

    private static OfflineTokenResponse? GetCacheToken(LicenseSeatClient client)
    {
        var field = typeof(LicenseSeatClient).GetField("_cache", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return ((LicenseCache)field!.GetValue(client)!).GetOfflineToken();
    }

    private OfflineTokenResponse SignToken(
        SignedBy signedBy = SignedBy.Vendor,
        long? iat = null,
        long? exp = null,
        long? licenseExpiresAt = null,
        string fingerprint = Configured)
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
            LicenseExpiresAt = licenseExpiresAt,
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

    private sealed class UndeletableStore(ILicenseStore inner) : ILicenseStore
    {
        public string? Read(string entryName) => inner.Read(entryName);

        public void Write(string entryName, string contents) => inner.Write(entryName, contents);

        public void Delete(string entryName) => throw new IOException("locked by antivirus");
    }

    private sealed class FlakyReadStore(ILicenseStore inner, string flakyEntry, int failures) : ILicenseStore
    {
        private int _remaining = failures;

        public string? Read(string entryName)
        {
            if (entryName == flakyEntry && Interlocked.Decrement(ref _remaining) >= 0)
            {
                throw new IOException("locked by a sync client");
            }

            return inner.Read(entryName);
        }

        public void Write(string entryName, string contents) => inner.Write(entryName, contents);

        public void Delete(string entryName) => inner.Delete(entryName);
    }

    private sealed class FakeServer : IHttpClientAdapter
    {
        private readonly PersistentOfflineTests? _owner;
        private volatile bool _unreachable;

        internal FakeServer(PersistentOfflineTests owner)
        {
            _owner = owner;
        }

        private FakeServer()
        {
            _unreachable = true;
        }

        internal static FakeServer Unreachable() => new FakeServer();

        internal void GoOffline() => _unreachable = true;

        internal string? ValidationBody { get; set; }

        internal SignedBy SignOfflineTokensWith { get; set; } = SignedBy.Vendor;

        internal bool HoldOfflineTokens { get; set; }

        internal bool OfflineTokensUnavailable { get; set; }

        internal TaskCompletionSource<bool> OfflineTokenRequested { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<bool> ReleaseOfflineTokens { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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

        public async Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            Posts.Enqueue(url);
            if (_unreachable)
            {
                return new HttpResponse(0, "offline");
            }

            string? fingerprint;
            using (var request = JsonDocument.Parse(jsonBody))
            {
                fingerprint = request.RootElement.TryGetProperty("fingerprint", out var value) ? value.GetString() : null;
            }

            if (url.EndsWith("/licenses/activate", StringComparison.Ordinal))
            {
                LastFingerprint = fingerprint;
                return new HttpResponse(200, TestResponses.Activation(LicenseKey, fingerprint!, ProductSlug));
            }

            if (url.EndsWith("/licenses/validate", StringComparison.Ordinal))
            {
                return new HttpResponse(200, ValidationBody ?? TestResponses.ValidValidation(LicenseKey, ProductSlug));
            }

            if (url.EndsWith("/licenses/offline-token", StringComparison.Ordinal))
            {
                if (OfflineTokensUnavailable)
                {
                    return new HttpResponse(503, "{\"error\":{\"code\":\"unavailable\",\"message\":\"unavailable\"}}");
                }

                if (HoldOfflineTokens)
                {
                    OfflineTokenRequested.TrySetResult(true);
                    await ReleaseOfflineTokens.Task.ConfigureAwait(false);
                }

                var token = _owner!.SignToken(SignOfflineTokensWith, fingerprint: fingerprint!);
                return new HttpResponse(200, JsonSerializer.Serialize(token, TokenJsonOptions));
            }

            return new HttpResponse(404, "{\"error\":{\"code\":\"not_found\",\"message\":\"not found\"}}");
        }
    }
}
