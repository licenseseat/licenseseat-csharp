using System.Collections.Concurrent;
using System.Text.Json;

namespace LicenseSeat.Tests;

public sealed class LicenseCachePersistenceTests
{
    private const string Prefix = "licenseseat_";
    private const string Anchor = "anchor-this-machine";

    [Fact]
    public void Hydrates_IdentityOnly_NeverValidationOrEntitlements()
    {
        var store = new MemoryStore();
        var writer = new LicenseCache(Prefix, store, () => Anchor, null);
        writer.SetLicense(License("device-123"));
        writer.SetDeviceId("device-123");
        writer.UpdateValidation(new ValidationResult
        {
            Valid = true,
            ActiveEntitlements = new List<Entitlement> { new Entitlement { Key = "pro" } }
        });
        writer.SetOfflineToken(new OfflineTokenResponse { Object = "offline_token", Canonical = "{}" });
        writer.SetLastSeenTimestamp(1_800_000_000);

        // Tamper with the stored license to claim entitlements.
        var tampered = store.Entries[Prefix + "license"].Replace("\"active_entitlements\":null", "\"active_entitlements\":[{\"key\":\"pro\"}]", StringComparison.Ordinal);
        Assert.NotEqual(store.Entries[Prefix + "license"], tampered);
        store.Entries[Prefix + "license"] = tampered;

        var reader = new LicenseCache(Prefix, store, () => Anchor, null);

        var license = reader.GetLicense();
        Assert.NotNull(license);
        Assert.Equal("TEST-KEY", license!.Key);
        Assert.Null(license.Validation);
        Assert.Null(license.ActiveEntitlements);
        Assert.Null(license.LastValidated);
        Assert.Equal("device-123", reader.GetDeviceId());
        Assert.NotNull(reader.GetOfflineToken());
        Assert.Equal(1_800_000_000, reader.GetLastSeenTimestamp());
    }

    [Fact]
    public void AnchorMismatch_RestoresOnlyTheRollbackFloor()
    {
        var store = Seeded();

        var elsewhere = new LicenseCache(Prefix, store, () => "anchor-other-machine", null);
        Assert.Null(elsewhere.GetLicense());
        Assert.Null(elsewhere.GetDeviceId());
        Assert.Null(elsewhere.GetOfflineToken());
        Assert.Equal(1_800_000_000, elsewhere.GetLastSeenTimestamp());

        var noIdentity = new LicenseCache(Prefix, store, () => null, null);
        Assert.Null(noIdentity.GetDeviceId());

        var throwing = new LicenseCache(Prefix, store, () => throw new InvalidOperationException("no identity"), null);
        Assert.Null(throwing.GetDeviceId());
    }

    [Fact]
    public void LicenseBoundToADifferentDevice_IsIgnored()
    {
        var store = Seeded();
        store.Entries[Prefix + "license"] = store.Entries[Prefix + "license"].Replace("device-123", "device-999", StringComparison.Ordinal);

        var cache = new LicenseCache(Prefix, store, () => Anchor, null);

        Assert.Equal("device-123", cache.GetDeviceId());
        Assert.Null(cache.GetLicense());
        Assert.Null(cache.GetOfflineToken());
    }

    [Theory]
    [InlineData("device", "not json")]
    [InlineData("device", "{\"version\":2,\"fingerprint\":\"device-123\",\"anchor\":\"anchor-this-machine\"}")]
    [InlineData("device", "{\"version\":1,\"fingerprint\":\"short\",\"anchor\":\"anchor-this-machine\"}")]
    [InlineData("device", "{\"version\":1,\"version\":1,\"fingerprint\":\"device-123\",\"anchor\":\"anchor-this-machine\"}")]
    [InlineData("license", "[]")]
    [InlineData("license", "{\"key\":\"has space\",\"device_id\":\"device-123\"}")]
    public void CorruptOrInvalidEntries_AreIgnoredWithoutThrowing(string entry, string contents)
    {
        var store = Seeded();
        store.Entries[Prefix + entry] = contents;
        var log = new ConcurrentQueue<string>();

        var cache = new LicenseCache(Prefix, store, () => Anchor, log.Enqueue);

        Assert.Null(cache.GetLicense());
        Assert.NotEmpty(log);
    }

    [Fact]
    public void StoreFaults_NeverFailCacheOperations()
    {
        var store = new MemoryStore { Fail = true };
        var log = new ConcurrentQueue<string>();
        var cache = new LicenseCache(Prefix, store, () => Anchor, log.Enqueue);

        cache.SetLicense(License("device-123"));
        cache.SetDeviceId("device-123");
        cache.SetOfflineToken(new OfflineTokenResponse { Object = "offline_token" });
        cache.SetLastSeenTimestamp(10);
        cache.ClearOfflineToken();
        cache.ClearLicense();
        cache.Clear();

        Assert.Equal("device-123", cache.GetDeviceId());
        Assert.NotEmpty(log);
    }

    [Fact]
    public void SetDeviceId_WithoutMachineIdentity_DoesNotPersistAnUnanchoredRecord()
    {
        var store = Seeded();
        var cache = new LicenseCache(Prefix, store, () => null, null);

        cache.SetDeviceId("device-456");

        Assert.Equal("device-456", cache.GetDeviceId());
        Assert.False(store.Entries.ContainsKey(Prefix + "device"));
    }

    [Fact]
    public void Clear_DeletesAuthorizationButKeepsDeviceIdentityAndRollbackFloor()
    {
        var store = Seeded();
        var cache = new LicenseCache(Prefix, store, () => Anchor, null);

        cache.Clear();

        Assert.Null(cache.GetLicense());
        Assert.Null(cache.GetOfflineToken());
        Assert.Equal("device-123", cache.GetDeviceId());
        Assert.Equal(1_800_000_000, cache.GetLastSeenTimestamp());
        Assert.Equal(new[] { Prefix + "device", Prefix + "last_seen" }, store.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void SigningKeys_AreNeverPersisted()
    {
        var store = Seeded();
        var cache = new LicenseCache(Prefix, store, () => Anchor, null);
        cache.SetPublicKey("offline-v1", Convert.ToBase64String(new byte[32]));

        Assert.DoesNotContain(store.Entries.Values, value => value.Contains(Convert.ToBase64String(new byte[32]), StringComparison.Ordinal));
        Assert.Null(new LicenseCache(Prefix, store, () => Anchor, null).GetPublicKey("offline-v1"));
    }

    [Fact]
    public void LastSeen_IsPersistedOnlyWhenItAdvances()
    {
        var store = new MemoryStore();
        var cache = new LicenseCache(Prefix, store, () => Anchor, null);

        cache.SetLastSeenTimestamp(200);
        cache.SetLastSeenTimestamp(100);

        using var document = JsonDocument.Parse(store.Entries[Prefix + "last_seen"]);
        Assert.Equal(200, document.RootElement.GetProperty("last_seen").GetInt64());
        Assert.Equal(1, store.Writes[Prefix + "last_seen"]);
    }

    [Fact]
    public void Prefixes_Isolate_Products_SharingOneStore()
    {
        var store = Seeded();

        var other = new LicenseCache("otherproduct_", store, () => Anchor, null);

        Assert.Null(other.GetLicense());
        Assert.Null(other.GetDeviceId());
        Assert.Equal(0, other.GetLastSeenTimestamp());
    }

    private static MemoryStore Seeded()
    {
        var store = new MemoryStore();
        var cache = new LicenseCache(Prefix, store, () => Anchor, null);
        cache.SetLicense(License("device-123"));
        cache.SetDeviceId("device-123");
        cache.SetOfflineToken(new OfflineTokenResponse { Object = "offline_token", Canonical = "{}" });
        cache.SetLastSeenTimestamp(1_800_000_000);
        return store;
    }

    private static License License(string deviceId) => new License
    {
        Key = "TEST-KEY",
        DeviceId = deviceId,
        Status = "active",
        Product = new Product { Slug = "test-product" }
    };

    private sealed class MemoryStore : ILicenseStore
    {
        internal ConcurrentDictionary<string, string> Entries { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        internal ConcurrentDictionary<string, int> Writes { get; } = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        internal bool Fail { get; set; }

        public string? Read(string entryName)
        {
            if (Fail)
            {
                throw new IOException("locked");
            }

            return Entries.TryGetValue(entryName, out var value) ? value : null;
        }

        public void Write(string entryName, string contents)
        {
            if (Fail)
            {
                throw new UnauthorizedAccessException("denied");
            }

            Entries[entryName] = contents;
            Writes.AddOrUpdate(entryName, 1, (_, count) => count + 1);
        }

        public void Delete(string entryName)
        {
            if (Fail)
            {
                throw new IOException("locked");
            }

            Entries.TryRemove(entryName, out _);
        }
    }
}
