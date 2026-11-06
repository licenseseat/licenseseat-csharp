#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LicenseSeat
{

    /// <summary>
    /// Thread-safe cache for license and offline-verification data, optionally
    /// written through to an <see cref="ILicenseStore"/> so it survives restarts.
    /// Values are copied at the cache boundary so caller-owned mutable models
    /// cannot silently change authorization state after validation.
    /// </summary>
    /// <remarks>
    /// What is persisted, and why it is safe to read back:
    /// <list type="bullet">
    /// <item><description>
    /// <c>device</c>: a fingerprint produced by the default derivation plus the
    /// OS version string it was derived from. On load the fingerprint must
    /// re-derive from <em>this</em> machine's name and user name and the stored
    /// OS version, so the record survives OS updates (which change only the OS
    /// version) but cannot be moved to, or rewritten for, a machine with a
    /// different name or account: that would need a SHA-256 preimage. Custom
    /// fingerprints are never persisted as device identity; apps that use them
    /// configure <see cref="LicenseSeatClientOptions.DeviceId"/>.
    /// </description></item>
    /// <item><description>
    /// <c>license</c>: key, device and activation time only. Status, expiry,
    /// plan, entitlements and validation results are never stored, so a
    /// restored license is <c>Pending</c> and not <c>IsActive</c> until it is
    /// validated online or by a verified offline token.
    /// </description></item>
    /// <item><description>
    /// <c>offline_token</c>: re-verified in full on every use, and only
    /// restored together with a rollback floor.
    /// </description></item>
    /// <item><description>
    /// <c>last_seen</c>: the clock-rollback floor; only ever moves forward,
    /// including across processes sharing the store.
    /// </description></item>
    /// </list>
    /// Signing keys are never persisted: local storage is writable by the
    /// party the signature protects against.
    /// </remarks>
    internal sealed class LicenseCache
    {
        internal const string LicenseEntry = "license";
        internal const string OfflineTokenEntry = "offline_token";
        internal const string DeviceEntry = "device";
        internal const string LastSeenEntry = "last_seen";
        internal const string DenialEntry = "denial";
        private const int RecordVersion = 1;
        private const string Tombstone = "{\"deleted\":true}";
        private const int MaxEntryBytes = 1024 * 1024;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            MaxDepth = SecurityValidation.MaxJsonDepth,
            WriteIndented = false
        };

        private readonly object _lock = new object();
        private readonly string _prefix;
        private readonly ILicenseStore? _store;
        private readonly Func<DeviceInputs?> _environment;
        private readonly string? _configuredDeviceId;
        private readonly Action<string> _log;

        private License? _license;
        private OfflineTokenResponse? _offlineToken;
        private string? _deviceId;
        private string? _persistedFingerprint;
        private string? _persistedOsVersion;
        private bool _deviceRecordUnreadable;
        private readonly Dictionary<string, string> _publicKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        private long _lastSeenTimestamp;
        private DenialRecord? _denial;

        /// <summary>
        /// Creates a new in-memory license cache.
        /// </summary>
        /// <param name="prefix">Validated namespace for persisted entry names.</param>
        public LicenseCache(string prefix)
            : this(prefix, null, null, null, null)
        {
        }

        /// <summary>
        /// Creates a license cache, hydrating it from <paramref name="store"/> when one is supplied.
        /// </summary>
        /// <param name="prefix">Validated namespace for persisted entry names.</param>
        /// <param name="store">Optional durable store.</param>
        /// <param name="environment">Machine facts provider; defaults to <see cref="DeviceIdentifier.CurrentInputs"/>.</param>
        /// <param name="configuredDeviceId">The application-configured device ID, which is authoritative when set.</param>
        /// <param name="log">Diagnostic sink for storage faults.</param>
        public LicenseCache(
            string prefix,
            ILicenseStore? store,
            Func<DeviceInputs?>? environment,
            string? configuredDeviceId,
            Action<string>? log)
        {
            if (!SecurityValidation.IsSafeIdentifier(prefix, 1, 64))
            {
                throw new ArgumentException("Cache prefix is invalid.", nameof(prefix));
            }

            _prefix = prefix;
            _store = store;
            _environment = environment ?? DeviceIdentifier.CurrentInputs;
            _configuredDeviceId = configuredDeviceId;
            _log = log ?? (_ => { });

            if (_store != null)
            {
                Hydrate();
            }
        }

        /// <summary>Gets a defensive copy of the cached license.</summary>
        public License? GetLicense()
        {
            lock (_lock)
            {
                return _license == null ? null : CloneLicense(_license);
            }
        }

        /// <summary>Stores a defensive copy of a license.</summary>
        public void SetLicense(License license)
        {
            if (license == null)
            {
                throw new ArgumentNullException(nameof(license));
            }

            lock (_lock)
            {
                _license = CloneLicense(license);
                Persist(LicenseEntry, JsonSerializer.Serialize(new LicenseRecord
                {
                    Version = RecordVersion,
                    Key = _license.Key,
                    DeviceId = _license.DeviceId,
                    ActivatedAt = _license.ActivatedAt
                }, JsonOptions));
            }
        }

        /// <summary>Clears the cached license (and any denial recorded for it).</summary>
        public void ClearLicense()
        {
            lock (_lock)
            {
                _license = null;
                Remove(LicenseEntry);
                ClearDenialLocked();
            }
        }

        /// <summary>
        /// Records the server's latest authoritative denial for the cached
        /// license. It is the current decision: offline fallback must not
        /// override it, in this process or after a relaunch, until an online
        /// success for that license clears it. The offline grant is kept in
        /// memory; if the denial cannot be persisted, the persisted grant is
        /// retired instead so a relaunch cannot revive it.
        /// </summary>
        public void SetDenial(string licenseKey, string code)
        {
            lock (_lock)
            {
                // Offline fallback only ever serves the cached license, so a
                // denial for any other key has nothing to protect and must not
                // displace the cached license's denial.
                if (_license == null || !string.Equals(_license.Key, licenseKey, StringComparison.Ordinal))
                {
                    return;
                }

                _denial = new DenialRecord { Version = RecordVersion, LicenseKey = licenseKey, Code = code };
                if (_store != null && !Persist(DenialEntry, JsonSerializer.Serialize(_denial, JsonOptions)))
                {
                    _log("Could not persist the denial; retiring the persisted offline token so a relaunch cannot override it.");
                    Remove(OfflineTokenEntry);
                }
            }
        }

        /// <summary>The recorded denial code for a license, or <c>null</c>.</summary>
        public string? GetDenial(string licenseKey)
        {
            lock (_lock)
            {
                return _denial != null && string.Equals(_denial.LicenseKey, licenseKey, StringComparison.Ordinal)
                    ? _denial.Code
                    : null;
            }
        }

        /// <summary>Clears a denial recorded for <paramref name="licenseKey"/> after an online success.</summary>
        public void ClearDenial(string licenseKey)
        {
            lock (_lock)
            {
                if (_denial != null)
                {
                    if (string.Equals(_denial.LicenseKey, licenseKey, StringComparison.Ordinal))
                    {
                        ClearDenialLocked();
                    }

                    return;
                }

                if (_store == null)
                {
                    return;
                }

                // A persisted denial that was not hydrated (its license failed to
                // load) must still be cleared, or a later relaunch would resurrect it.
                var persisted = Load<DenialRecord>(DenialEntry, out var transient, out var present);
                if (present && (transient || persisted == null ||
                                string.Equals(persisted.LicenseKey, licenseKey, StringComparison.Ordinal)))
                {
                    Remove(DenialEntry);
                }
            }
        }

        private void ClearDenialLocked()
        {
            _denial = null;
            Remove(DenialEntry);
        }

        /// <summary>Updates validation state using defensive copies.</summary>
        /// <remarks>Validation state is never persisted.</remarks>
        public void UpdateValidation(ValidationResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            lock (_lock)
            {
                if (_license == null)
                {
                    return;
                }

                var clonedResult = Clone(result);
                _license.Validation = clonedResult;
                _license.LastValidated = DateTimeOffset.UtcNow;
                _license.ActiveEntitlements = clonedResult.ActiveEntitlements == null
                    ? null
                    : Clone(clonedResult.ActiveEntitlements);
            }
        }

        /// <summary>Gets the cached device fingerprint.</summary>
        public string? GetDeviceId()
        {
            lock (_lock)
            {
                return _deviceId ?? _license?.DeviceId;
            }
        }

        /// <summary>
        /// The persisted default-derivation fingerprint verified for this
        /// machine, with the OS version it was derived from. Activation reuses it
        /// so an OS update does not create a new device.
        /// </summary>
        public bool TryGetPersistedFingerprint(out string fingerprint, out string osVersion)
        {
            lock (_lock)
            {
                fingerprint = _persistedFingerprint ?? string.Empty;
                osVersion = _persistedOsVersion ?? string.Empty;
                return _persistedFingerprint != null;
            }
        }

        /// <summary>
        /// Whether a device record exists but could not be read. Re-reads it
        /// first, so a transient fault (antivirus, sync client) clears itself.
        /// While this is true a new device must not be established: doing so
        /// could replace the activated fingerprint and consume another seat.
        /// </summary>
        public bool DeviceRecordIsUnreadable()
        {
            lock (_lock)
            {
                if (_deviceRecordUnreadable && _store != null)
                {
                    HydrateDevice();
                }

                return _deviceRecordUnreadable;
            }
        }

        /// <summary>Sets the cached device fingerprint.</summary>
        /// <param name="deviceId">The fingerprint the license was activated with.</param>
        /// <param name="derivedFromOsVersion">
        /// The OS version when <paramref name="deviceId"/> came from the default
        /// derivation; <c>null</c> for custom fingerprints, which are never
        /// persisted as device identity.
        /// </param>
        public void SetDeviceId(string deviceId, string? derivedFromOsVersion = null)
        {
            if (!SecurityValidation.IsSafeText(deviceId, 8, 255))
            {
                throw new ArgumentException("Device fingerprint is invalid.", nameof(deviceId));
            }

            lock (_lock)
            {
                _deviceId = deviceId;
                if (_store == null || derivedFromOsVersion == null || _configuredDeviceId != null)
                {
                    return;
                }

                _persistedFingerprint = deviceId;
                _persistedOsVersion = derivedFromOsVersion;
                _deviceRecordUnreadable = false;
                Persist(DeviceEntry, JsonSerializer.Serialize(new DeviceRecord
                {
                    Version = RecordVersion,
                    Fingerprint = deviceId,
                    OsVersion = derivedFromOsVersion
                }, JsonOptions));
            }
        }

        /// <summary>Gets a defensive copy of the cached offline token.</summary>
        public OfflineTokenResponse? GetOfflineToken()
        {
            lock (_lock)
            {
                return _offlineToken == null ? null : Clone(_offlineToken);
            }
        }

        /// <summary>Stores a defensive copy of an offline token.</summary>
        public void SetOfflineToken(OfflineTokenResponse offlineToken)
        {
            if (offlineToken == null)
            {
                throw new ArgumentNullException(nameof(offlineToken));
            }

            lock (_lock)
            {
                _offlineToken = Clone(offlineToken);
                Persist(OfflineTokenEntry, JsonSerializer.Serialize(_offlineToken, JsonOptions));
            }
        }

        /// <summary>Clears offline token and signing-key material.</summary>
        public void ClearOfflineToken()
        {
            lock (_lock)
            {
                _offlineToken = null;
                _publicKeys.Clear();
                Remove(OfflineTokenEntry);
            }
        }

        /// <summary>Gets a cached signing key by exact key ID.</summary>
        public string? GetPublicKey(string keyId)
        {
            lock (_lock)
            {
                return _publicKeys.TryGetValue(keyId, out var publicKey) ? publicKey : null;
            }
        }

        /// <summary>Stores a signing key (memory only) and prevents same-ID key substitution.</summary>
        public void SetPublicKey(string keyId, string publicKey)
        {
            if (!SecurityValidation.IsSafeIdentifier(keyId, 1, 255, allowColon: true))
            {
                throw new ArgumentException("Signing key ID is invalid.", nameof(keyId));
            }

            lock (_lock)
            {
                if (_publicKeys.TryGetValue(keyId, out var existingKey) &&
                    !Ed25519Verifier.ConstantTimeEquals(existingKey, publicKey))
                {
                    throw new CryptoException("A different public key is already pinned for this key ID.", CryptoException.InvalidKeyCode);
                }

                if (_publicKeys.Count >= 64 && !_publicKeys.ContainsKey(keyId))
                {
                    throw new CryptoException("Signing key cache limit exceeded.", CryptoException.InvalidKeyCode);
                }

                _publicKeys[keyId] = publicKey;
            }
        }

        /// <summary>Gets the monotonic last-seen wall-clock timestamp.</summary>
        public long GetLastSeenTimestamp()
        {
            lock (_lock)
            {
                return _lastSeenTimestamp;
            }
        }

        /// <summary>Advances the last-seen timestamp without allowing rollback.</summary>
        public void SetLastSeenTimestamp(long timestamp)
        {
            if (timestamp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(timestamp));
            }

            lock (_lock)
            {
                if (timestamp <= _lastSeenTimestamp)
                {
                    return;
                }

                _lastSeenTimestamp = timestamp;
                if (_store == null)
                {
                    return;
                }

                // Another process sharing the store may have written a later
                // value; never overwrite it with an earlier one.
                var stored = Load<LastSeenRecord>(LastSeenEntry, out _);
                if (stored != null && stored.LastSeen > _lastSeenTimestamp)
                {
                    _lastSeenTimestamp = stored.LastSeen;
                    return;
                }

                Persist(LastSeenEntry, JsonSerializer.Serialize(new LastSeenRecord { LastSeen = _lastSeenTimestamp }, JsonOptions));
            }
        }

        /// <summary>
        /// Clears cached authorization state: the license, offline token and
        /// signing keys. The device fingerprint and the clock-rollback floor are
        /// kept: they are not authority, and discarding them would let a later
        /// activation on this machine consume a new seat or reset rollback
        /// detection.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _license = null;
                _offlineToken = null;
                _publicKeys.Clear();
                Remove(LicenseEntry);
                Remove(OfflineTokenEntry);
                ClearDenialLocked();
            }
        }

        private void Hydrate()
        {
            lock (_lock)
            {
                var lastSeen = Load<LastSeenRecord>(LastSeenEntry, out _);
                var hasFloor = lastSeen != null && lastSeen.LastSeen > 0;
                if (hasFloor)
                {
                    _lastSeenTimestamp = lastSeen!.LastSeen;
                }

                HydrateDevice();
                var trusted = _configuredDeviceId ?? _persistedFingerprint;
                if (trusted == null)
                {
                    return;
                }

                _deviceId = trusted;

                var record = Load<LicenseRecord>(LicenseEntry, out _);
                if (record == null)
                {
                    return;
                }

                if (record.Version != RecordVersion ||
                    !SecurityValidation.IsAsciiPrintable(record.Key, 1, 512) ||
                    !string.Equals(record.DeviceId, trusted, StringComparison.Ordinal))
                {
                    _log("Persisted license does not belong to this device; ignoring it.");
                    return;
                }

                _license = new License
                {
                    Key = record.Key!,
                    DeviceId = trusted,
                    ActivatedAt = record.ActivatedAt
                };

                var denial = Load<DenialRecord>(DenialEntry, out var denialUnreadable, out var denialPresent);
                var denialValid = denial != null && denial.Version == RecordVersion &&
                                  SecurityValidation.IsSafeIdentifier(denial.Code, 1, 100);
                if (denialPresent && (denialUnreadable || !denialValid))
                {
                    // Cannot tell whether the server's last word was a denial:
                    // fail closed for offline use until an online validation.
                    _denial = new DenialRecord { Version = RecordVersion, LicenseKey = record.Key, Code = "denial_state_unreadable" };
                }
                else if (denialValid && string.Equals(denial!.LicenseKey, record.Key, StringComparison.Ordinal))
                {
                    _denial = denial;
                }

                if (!hasFloor)
                {
                    // A token without its rollback floor (deleted or unreadable)
                    // is not restored; the next online validation re-issues one.
                    _log("No persisted rollback floor; not restoring the offline token.");
                    return;
                }

                var offlineToken = Load<OfflineTokenResponse>(OfflineTokenEntry, out _);
                if (offlineToken != null && offlineToken.Object == "offline_token")
                {
                    // Re-verified in full every time it is used.
                    _offlineToken = offlineToken;
                }
            }
        }

        // Caller holds _lock.
        private void HydrateDevice()
        {
            _deviceRecordUnreadable = false;
            if (_configuredDeviceId != null)
            {
                return;
            }

            var device = Load<DeviceRecord>(DeviceEntry, out var transientFault);
            if (transientFault)
            {
                _deviceRecordUnreadable = true;
                return;
            }

            if (device == null)
            {
                return;
            }

            var inputs = SafeEnvironment();
            if (device.Version != RecordVersion ||
                inputs == null ||
                !SecurityValidation.IsSafeText(device.Fingerprint, 8, 255) ||
                !SecurityValidation.IsSafeText(device.OsVersion, 1, 1024) ||
                !Ed25519Verifier.ConstantTimeEquals(
                    DeviceIdentifier.FromInputs(inputs.MachineName, inputs.UserName, device.OsVersion!),
                    device.Fingerprint))
            {
                _log("Persisted device identity does not derive from this machine and user; ignoring persisted license state.");
                return;
            }

            _persistedFingerprint = device.Fingerprint;
            _persistedOsVersion = device.OsVersion;
        }

        private T? Load<T>(string entry, out bool transientFault)
            where T : class
        {
            return Load<T>(entry, out transientFault, out _);
        }

        // present: the entry exists (or might: a read fault counts as present).
        private T? Load<T>(string entry, out bool transientFault, out bool present)
            where T : class
        {
            transientFault = false;
            present = true;
            string? text;
            try
            {
                text = _store!.Read(EntryName(entry));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                // Possibly transient (locked by antivirus or a sync client).
                transientFault = !(ex is InvalidDataException);
                _log($"Could not read persisted entry '{entry}' ({ex.GetType().Name}).");
                return null;
            }
            catch (Exception ex)
            {
                _log($"Ignoring unreadable persisted entry '{entry}' ({ex.GetType().Name}).");
                return null;
            }

            if (text == null || string.Equals(text, Tombstone, StringComparison.Ordinal))
            {
                // Absent, or deleted via tombstone when a delete failed.
                present = false;
                return null;
            }

            try
            {
                using (SecurityValidation.ParseStrictJson(text, MaxEntryBytes))
                {
                    // Structural validation only: bounded size and depth, no duplicate properties.
                }

                return JsonSerializer.Deserialize<T>(text, JsonOptions);
            }
            catch (Exception ex)
            {
                _log($"Ignoring corrupt persisted entry '{entry}' ({ex.GetType().Name}).");
                return null;
            }
        }

        private bool Persist(string entry, string contents)
        {
            if (_store == null)
            {
                return false;
            }

            try
            {
                _store.Write(EntryName(entry), contents);
                return true;
            }
            catch (Exception ex)
            {
                _log($"Failed to persist '{entry}' ({ex.GetType().Name}); continuing with the in-memory value.");
                return false;
            }
        }

        private void Remove(string entry)
        {
            if (_store == null)
            {
                return;
            }

            try
            {
                _store.Delete(EntryName(entry));
                return;
            }
            catch (Exception ex)
            {
                _log($"Failed to delete persisted '{entry}' ({ex.GetType().Name}); writing a tombstone.");
            }

            // A deletion that fails must not leave a revoked token or license
            // restorable: overwrite it with a value that never loads.
            Persist(entry, Tombstone);
        }

        private DeviceInputs? SafeEnvironment()
        {
            try
            {
                return _environment();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private string EntryName(string entry)
        {
            return string.Concat(_prefix, entry);
        }

        private static T Clone<T>(T value)
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                   ?? throw new InvalidOperationException("Cached value could not be copied safely.");
        }

        private static License CloneLicense(License license)
        {
            var clone = Clone(license);
            clone.Validation = license.Validation == null ? null : Clone(license.Validation);
            return clone;
        }

        private sealed class DeviceRecord
        {
            public int Version { get; set; }

            public string? Fingerprint { get; set; }

            public string? OsVersion { get; set; }
        }

        private sealed class LicenseRecord
        {
            public int Version { get; set; }

            public string? Key { get; set; }

            public string? DeviceId { get; set; }

            public DateTimeOffset? ActivatedAt { get; set; }
        }

        private sealed class DenialRecord
        {
            public int Version { get; set; }

            public string? LicenseKey { get; set; }

            public string? Code { get; set; }
        }

        private sealed class LastSeenRecord
        {
            public long LastSeen { get; set; }
        }
    }
}
