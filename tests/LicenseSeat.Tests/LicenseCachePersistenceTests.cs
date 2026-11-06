using System.Collections.Concurrent;
using System.Text.Json;

namespace LicenseSeat.Tests;

public sealed class LicenseCachePersistenceTests
{
    private const string Prefix = "licenseseat_";
    private static readonly DeviceInputs Machine = new DeviceInputs("GAMING-PC", "denis", "Microsoft Windows NT 10.0.19045.0");
    private static readonly string Fingerprint = DeviceIdentifier.FromInputs(Machine.MachineName, Machine.UserName, Machine.OsVersion);

    [Fact]
    public void Hydrates_IdentityOnly_NeverStatusValidationOrEntitlements()
    {
        var store = Seeded();
        var reader = Open(store);

        var license = reader.GetLicense();
        Assert.NotNull(license);
        Assert.Equal("TEST-KEY", license!.Key);
        Assert.Equal(Fingerprint, license.DeviceId);
        Assert.Null(license.Status);
        Assert.False(license.IsActive);
        Assert.Null(license.ExpiresAt);
        Assert.Null(license.Validation);
        Assert.Null(license.ActiveEntitlements);
        Assert.Equal(Fingerprint, reader.GetDeviceId());
        Assert.NotNull(reader.GetOfflineToken());
        Assert.Equal(1_800_000_000, reader.GetLastSeenTimestamp());
        Assert.True(reader.TryGetPersistedFingerprint(out var fingerprint, out var osVersion));
        Assert.Equal(Fingerprint, fingerprint);
        Assert.Equal(Machine.OsVersion, osVersion);
    }

    [Fact]
    public void ForgedLicenseFields_AreNotRestored()
    {
        var store = Seeded();
        store.Entries[Prefix + "license"] = JsonSerializer.Serialize(new
        {
            version = 1,
            key = "TEST-KEY",
            device_id = Fingerprint,
            status = "active",
            expires_at = (string?)null,
            active_entitlements = new[] { new { key = "pro" } }
        });

        var license = Open(store).GetLicense();

        Assert.NotNull(license);
        Assert.False(license!.IsActive);
        Assert.Null(license.ActiveEntitlements);
    }

    [Fact]
    public void AnotherMachineOrUser_RestoresOnlyTheRollbackFloor()
    {
        var store = Seeded();

        foreach (var elsewhere in new[]
                 {
                     new DeviceInputs("OTHER-PC", Machine.UserName, Machine.OsVersion),
                     new DeviceInputs(Machine.MachineName, "someone-else", Machine.OsVersion)
                 })
        {
            var cache = new LicenseCache(Prefix, store, () => elsewhere, null, null);
            Assert.Null(cache.GetLicense());
            Assert.Null(cache.GetDeviceId());
            Assert.Null(cache.GetOfflineToken());
            Assert.False(cache.TryGetPersistedFingerprint(out _, out _));
            Assert.Equal(1_800_000_000, cache.GetLastSeenTimestamp());
        }

        Assert.Null(new LicenseCache(Prefix, store, () => null, null, null).GetDeviceId());
        Assert.Null(new LicenseCache(Prefix, store, () => throw new InvalidOperationException("no identity"), null, null).GetDeviceId());
    }

    [Fact]
    public void OsUpdate_KeepsTheStoredFingerprint()
    {
        var store = Seeded();
        var updated = new DeviceInputs(Machine.MachineName, Machine.UserName, "Microsoft Windows NT 10.0.26100.0");

        var cache = new LicenseCache(Prefix, store, () => updated, null, null);

        Assert.Equal(Fingerprint, cache.GetDeviceId());
        Assert.NotNull(cache.GetLicense());
    }

    [Fact]
    public void ConfiguredDeviceId_IsAuthoritativeAndCustomIdsAreNotPersistedAsIdentity()
    {
        var store = new MemoryStore();
        var writer = new LicenseCache(Prefix, store, () => Machine, "configured-device", null);
        writer.SetLicense(new License { Key = "TEST-KEY", DeviceId = "configured-device" });
        writer.SetDeviceId("configured-device", Machine.OsVersion);
        writer.SetLastSeenTimestamp(10);
        Assert.False(store.Entries.ContainsKey(Prefix + "device"));

        Assert.NotNull(new LicenseCache(Prefix, store, () => Machine, "configured-device", null).GetLicense());
        Assert.Null(new LicenseCache(Prefix, store, () => Machine, null, null).GetLicense());

        var custom = new LicenseCache(Prefix, new MemoryStore(), () => Machine, null, null);
        custom.SetDeviceId("custom-device-id");
        Assert.False(custom.TryGetPersistedFingerprint(out _, out _));
    }

    [Fact]
    public void LicenseBoundToADifferentDevice_IsIgnored()
    {
        var store = Seeded();
        store.Entries[Prefix + "license"] = store.Entries[Prefix + "license"].Replace(Fingerprint, "device-999-device", StringComparison.Ordinal);

        var cache = Open(store);

        Assert.Equal(Fingerprint, cache.GetDeviceId());
        Assert.Null(cache.GetLicense());
        Assert.Null(cache.GetOfflineToken());
    }

    [Fact]
    public void MissingRollbackFloor_DropsTheTokenButKeepsTheActivation()
    {
        var store = Seeded();
        store.Entries.TryRemove(Prefix + "last_seen", out _);

        var cache = Open(store);

        Assert.NotNull(cache.GetLicense());
        Assert.Null(cache.GetOfflineToken());
    }

    [Theory]
    [InlineData("device", "not json")]
    [InlineData("device", "{\"version\":2,\"fingerprint\":\"x\",\"os_version\":\"y\"}")]
    [InlineData("device", "{\"version\":1,\"version\":1}")]
    [InlineData("license", "[]")]
    [InlineData("license", "{\"version\":1,\"key\":\"has space\",\"device_id\":\"x\"}")]
    public void CorruptOrInvalidEntries_AreIgnoredWithoutThrowing(string entry, string contents)
    {
        var store = Seeded();
        store.Entries[Prefix + entry] = contents;
        var log = new ConcurrentQueue<string>();

        var cache = new LicenseCache(Prefix, store, () => Machine, null, log.Enqueue);

        Assert.Null(cache.GetLicense());
        Assert.False(cache.DeviceRecordIsUnreadable());
        Assert.NotEmpty(log);
    }

    [Fact]
    public void TokenTombstone_IsNotRestored()
    {
        var store = Seeded();
        store.Entries[Prefix + "offline_token"] = "{\"deleted\":true}";

        Assert.Null(Open(store).GetOfflineToken());
    }

    [Fact]
    public void TransientDeviceReadFault_IsReportedUntilTheRecordIsReadable()
    {
        var store = Seeded();
        store.FailReadsOf = Prefix + "device";

        var cache = Open(store);
        Assert.Null(cache.GetLicense());
        Assert.True(cache.DeviceRecordIsUnreadable());

        store.FailReadsOf = null;
        Assert.False(cache.DeviceRecordIsUnreadable());
        Assert.True(cache.TryGetPersistedFingerprint(out var fingerprint, out _));
        Assert.Equal(Fingerprint, fingerprint);
    }

    [Fact]
    public void StoreFaults_NeverFailCacheOperations()
    {
        var store = new MemoryStore { Fail = true };
        var log = new ConcurrentQueue<string>();
        var cache = new LicenseCache(Prefix, store, () => Machine, null, log.Enqueue);

        cache.SetLicense(new License { Key = "TEST-KEY", DeviceId = Fingerprint });
        cache.SetDeviceId(Fingerprint, Machine.OsVersion);
        cache.SetOfflineToken(new OfflineTokenResponse { Object = "offline_token" });
        cache.SetLastSeenTimestamp(10);
        cache.ClearOfflineToken();
        cache.ClearLicense();
        cache.Clear();

        Assert.Equal(Fingerprint, cache.GetDeviceId());
        Assert.NotEmpty(log);
    }

    [Fact]
    public void FailedDeletion_WritesATombstone()
    {
        var store = Seeded();
        store.FailDeletes = true;
        var cache = Open(store);

        cache.ClearOfflineToken();
        cache.ClearLicense();

        Assert.Equal("{\"deleted\":true}", store.Entries[Prefix + "offline_token"]);
        Assert.Null(Open(store).GetLicense());
    }

    [Fact]
    public void Clear_DeletesAuthorizationButKeepsDeviceIdentityAndRollbackFloor()
    {
        var store = Seeded();
        var cache = Open(store);

        cache.Clear();

        Assert.Null(cache.GetLicense());
        Assert.Null(cache.GetOfflineToken());
        Assert.Equal(Fingerprint, cache.GetDeviceId());
        Assert.Equal(1_800_000_000, cache.GetLastSeenTimestamp());
        Assert.Equal(new[] { Prefix + "device", Prefix + "last_seen" }, store.Entries.Keys.OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void SigningKeys_AreNeverPersisted()
    {
        var store = Seeded();
        var cache = Open(store);
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray());
        cache.SetPublicKey("offline-v1", key);

        Assert.DoesNotContain(store.Entries.Values, value => value.Contains(key, StringComparison.Ordinal));
        Assert.Null(Open(store).GetPublicKey("offline-v1"));
    }

    [Fact]
    public void LastSeen_NeverMovesBackwardsAcrossInstancesSharingAStore()
    {
        var store = new MemoryStore();
        var first = new LicenseCache(Prefix, store, () => Machine, null, null);
        var second = new LicenseCache(Prefix, store, () => Machine, null, null);

        first.SetLastSeenTimestamp(300);
        second.SetLastSeenTimestamp(200);
        first.SetLastSeenTimestamp(250);

        using var document = JsonDocument.Parse(store.Entries[Prefix + "last_seen"]);
        Assert.Equal(300, document.RootElement.GetProperty("last_seen").GetInt64());
        Assert.Equal(300, second.GetLastSeenTimestamp());
        Assert.Equal(1, store.Writes[Prefix + "last_seen"]);
    }

    [Fact]
    public void Prefixes_Isolate_Products_SharingOneStore()
    {
        var store = Seeded();

        var other = new LicenseCache("otherproduct_", store, () => Machine, null, null);

        Assert.Null(other.GetLicense());
        Assert.Null(other.GetDeviceId());
        Assert.Equal(0, other.GetLastSeenTimestamp());
    }

    [Fact]
    public void Denial_IsPersistedPerLicenseAndClearedByOnlineSuccessOrDeactivation()
    {
        var store = Seeded();
        var cache = Open(store);
        cache.SetDenial("TEST-KEY", "not_entitled");

        var relaunched = Open(store);
        Assert.Equal("not_entitled", relaunched.GetDenial("TEST-KEY"));
        Assert.Null(relaunched.GetDenial("OTHER-KEY"));
        Assert.NotNull(relaunched.GetOfflineToken());

        relaunched.ClearDenial("TEST-KEY");
        Assert.Null(Open(store).GetDenial("TEST-KEY"));

        relaunched.SetDenial("TEST-KEY", "not_entitled");
        relaunched.ClearLicense();
        Assert.False(store.Entries.ContainsKey(Prefix + "denial"));
    }

    [Fact]
    public void UnreadableDenialRecord_FailsClosedForOfflineUse()
    {
        var store = Seeded();
        Open(store).SetDenial("TEST-KEY", "product_mismatch");
        store.FailReadsOf = Prefix + "denial";

        Assert.Equal("denial_state_unreadable", Open(store).GetDenial("TEST-KEY"));
    }

    [Fact]
    public async Task LastSeen_ConcurrentInstancesNeverPersistAnEarlierFloor()
    {
        var gated = new GatedStore(new MemoryStore(), Prefix + "last_seen");
        var first = new LicenseCache(Prefix, gated, () => Machine, null, null);
        var second = new LicenseCache(Prefix, gated, () => Machine, null, null);

        var slow = Task.Run(() => first.SetLastSeenTimestamp(200));
        Assert.True(gated.FirstWriteStarted.Wait(TimeSpan.FromSeconds(10)));
        var fast = Task.Run(() => second.SetLastSeenTimestamp(300));
        await Task.Delay(200);
        gated.Release.Set();
        await Task.WhenAll(slow, fast);

        using var document = JsonDocument.Parse(gated.Inner.Entries[Prefix + "last_seen"]);
        Assert.Equal(300, document.RootElement.GetProperty("last_seen").GetInt64());
    }

    [Fact]
    public void FileStore_InaccessibleDirectory_IsAFaultNotAnAbsence()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "licenseseat-tests", Guid.NewGuid().ToString("N"));
        var store = new FileLicenseStore(directory);
        store.Write("licenseseat_device", "{}");
        File.SetUnixFileMode(directory, UnixFileMode.None);
        try
        {
            Assert.ThrowsAny<UnauthorizedAccessException>(() => store.Read("licenseseat_device"));
            Assert.ThrowsAny<UnauthorizedAccessException>(() => store.Delete("licenseseat_device"));
        }
        finally
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(directory, recursive: true);
        }

        Assert.Null(new FileLicenseStore(directory).Read("licenseseat_device"));
        new FileLicenseStore(directory).Delete("licenseseat_device");
    }

    private sealed class GatedStore(MemoryStore inner, string gatedEntry) : ILicenseStore
    {
        private int _writes;

        internal MemoryStore Inner => inner;

        internal ManualResetEventSlim FirstWriteStarted { get; } = new ManualResetEventSlim();

        internal ManualResetEventSlim Release { get; } = new ManualResetEventSlim();

        public string? Read(string entryName) => inner.Read(entryName);

        public void Write(string entryName, string contents)
        {
            if (entryName == gatedEntry && Interlocked.Increment(ref _writes) == 1)
            {
                FirstWriteStarted.Set();
                Release.Wait(TimeSpan.FromSeconds(10));
            }

            inner.Write(entryName, contents);
        }

        public void Delete(string entryName) => inner.Delete(entryName);
    }

    private static LicenseCache Open(MemoryStore store) => new LicenseCache(Prefix, store, () => Machine, null, null);

    private static MemoryStore Seeded()
    {
        var store = new MemoryStore();
        var cache = Open(store);
        cache.SetLicense(new License
        {
            Key = "TEST-KEY",
            DeviceId = Fingerprint,
            Status = "active",
            ActiveEntitlements = new List<Entitlement> { new Entitlement { Key = "pro" } }
        });
        cache.SetDeviceId(Fingerprint, Machine.OsVersion);
        cache.SetOfflineToken(new OfflineTokenResponse { Object = "offline_token", Canonical = "{}" });
        cache.SetLastSeenTimestamp(1_800_000_000);
        return store;
    }

    private sealed class MemoryStore : ILicenseStore
    {
        internal ConcurrentDictionary<string, string> Entries { get; } = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        internal ConcurrentDictionary<string, int> Writes { get; } = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);

        internal bool Fail { get; set; }

        internal bool FailDeletes { get; set; }

        internal string? FailReadsOf { get; set; }

        public string? Read(string entryName)
        {
            if (Fail || entryName == FailReadsOf)
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
            if (Fail || FailDeletes)
            {
                throw new IOException("locked");
            }

            Entries.TryRemove(entryName, out _);
        }
    }
}
