#nullable enable
using System;
using System.Collections.Generic;
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
    /// <c>device</c>: the fingerprint the license was activated with, plus a
    /// machine anchor (<see cref="DeviceIdentifier.ComputeAnchor"/>). The
    /// stored fingerprint is authoritative on the machine it was written on,
    /// so a Windows update that changes <see cref="DeviceIdentifier.Generate"/>
    /// no longer orphans the activation or burns a seat. A record whose anchor
    /// does not match this machine and user is ignored together with the
    /// license and token, so copying the folder to another machine does not
    /// carry the activation with it.
    /// </description></item>
    /// <item><description>
    /// <c>license</c>: identity only. Cached validation results, entitlements
    /// and the last-validated time are discarded on load; the license is
    /// <c>Pending</c> until validated online or by a verified offline token.
    /// </description></item>
    /// <item><description>
    /// <c>offline_token</c>: re-verified in full on every use.
    /// </description></item>
    /// <item><description>
    /// <c>last_seen</c>: the clock-rollback floor. It can only move forward,
    /// so a persisted value can only make verification stricter.
    /// </description></item>
    /// </list>
    /// Signing keys are never persisted: local storage is writable by the
    /// party the signature protects against, so it cannot establish signing
    /// authority. Pin the key with
    /// <see cref="LicenseSeatClientOptions.SigningPublicKey"/> for offline
    /// verification after a restart.
    /// </remarks>
    internal sealed class LicenseCache
    {
        internal const string LicenseEntry = "license";
        internal const string OfflineTokenEntry = "offline_token";
        internal const string DeviceEntry = "device";
        internal const string LastSeenEntry = "last_seen";
        private const int DeviceRecordVersion = 1;

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
        private readonly Func<string?> _deviceAnchor;
        private readonly Action<string> _log;

        private License? _license;
        private OfflineTokenResponse? _offlineToken;
        private string? _deviceId;
        private readonly Dictionary<string, string> _publicKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        private long _lastSeenTimestamp;

        /// <summary>
        /// Creates a new in-memory license cache.
        /// </summary>
        /// <param name="prefix">Validated namespace for persisted entry names.</param>
        public LicenseCache(string prefix)
            : this(prefix, null, null, null)
        {
        }

        /// <summary>
        /// Creates a license cache, hydrating it from <paramref name="store"/> when one is supplied.
        /// </summary>
        /// <param name="prefix">Validated namespace for persisted entry names.</param>
        /// <param name="store">Optional durable store.</param>
        /// <param name="deviceAnchor">Machine anchor provider; defaults to <see cref="DeviceIdentifier.ComputeAnchor"/>.</param>
        /// <param name="log">Diagnostic sink for storage faults.</param>
        public LicenseCache(string prefix, ILicenseStore? store, Func<string?>? deviceAnchor, Action<string>? log)
        {
            if (!SecurityValidation.IsSafeIdentifier(prefix, 1, 64))
            {
                throw new ArgumentException("Cache prefix is invalid.", nameof(prefix));
            }

            _prefix = prefix;
            _store = store;
            _deviceAnchor = deviceAnchor ?? DeviceIdentifier.ComputeAnchor;
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
                Persist(LicenseEntry, () => JsonSerializer.Serialize(_license, JsonOptions));
            }
        }

        /// <summary>Clears the cached license.</summary>
        public void ClearLicense()
        {
            lock (_lock)
            {
                _license = null;
                Remove(LicenseEntry);
            }
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

        /// <summary>Sets the cached device fingerprint and persists it with this machine's anchor.</summary>
        public void SetDeviceId(string deviceId)
        {
            if (!SecurityValidation.IsSafeText(deviceId, 8, 255))
            {
                throw new ArgumentException("Device fingerprint is invalid.", nameof(deviceId));
            }

            lock (_lock)
            {
                _deviceId = deviceId;
                if (_store == null)
                {
                    return;
                }

                var anchor = SafeAnchor();
                if (anchor == null)
                {
                    // Without an anchor the record could not be trusted on reload.
                    _log("Device identity is unavailable; the device fingerprint was not persisted.");
                    Remove(DeviceEntry);
                    return;
                }

                Persist(DeviceEntry, () => JsonSerializer.Serialize(new DeviceRecord
                {
                    Version = DeviceRecordVersion,
                    Fingerprint = deviceId,
                    Anchor = anchor
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
                Persist(OfflineTokenEntry, () => JsonSerializer.Serialize(_offlineToken, JsonOptions));
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
                if (timestamp > _lastSeenTimestamp)
                {
                    _lastSeenTimestamp = timestamp;
                    Persist(LastSeenEntry, () => JsonSerializer.Serialize(new LastSeenRecord { LastSeen = timestamp }, JsonOptions));
                }
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
            }
        }

        private void Hydrate()
        {
            lock (_lock)
            {
                // The rollback floor is safe to restore unconditionally: it only
                // ever makes verification stricter.
                var lastSeen = Load<LastSeenRecord>(LastSeenEntry);
                if (lastSeen != null && lastSeen.LastSeen > 0)
                {
                    _lastSeenTimestamp = lastSeen.LastSeen;
                }

                var device = Load<DeviceRecord>(DeviceEntry);
                if (device == null)
                {
                    return;
                }

                var anchor = SafeAnchor();
                if (device.Version != DeviceRecordVersion ||
                    !SecurityValidation.IsSafeText(device.Fingerprint, 8, 255) ||
                    anchor == null ||
                    device.Anchor == null ||
                    !Ed25519Verifier.ConstantTimeEquals(device.Anchor, anchor))
                {
                    _log("Persisted device identity does not belong to this machine and user; ignoring persisted license state.");
                    return;
                }

                _deviceId = device.Fingerprint;

                var license = Load<License>(LicenseEntry);
                if (license == null)
                {
                    return;
                }

                if (!SecurityValidation.IsAsciiPrintable(license.Key, 1, 512) ||
                    !string.Equals(license.DeviceId, _deviceId, StringComparison.Ordinal))
                {
                    _log("Persisted license is inconsistent with the persisted device identity; ignoring it.");
                    return;
                }

                // Identity only: nothing that grants access is trusted from disk.
                license.Validation = null;
                license.ActiveEntitlements = null;
                license.LastValidated = null;
                _license = license;

                var offlineToken = Load<OfflineTokenResponse>(OfflineTokenEntry);
                if (offlineToken != null)
                {
                    // Re-verified in full every time it is used.
                    _offlineToken = offlineToken;
                }
            }
        }

        private T? Load<T>(string entry)
            where T : class
        {
            try
            {
                var text = _store!.Read(EntryName(entry));
                if (text == null)
                {
                    return null;
                }

                using (SecurityValidation.ParseStrictJson(text, FileLicenseStoreLimits.MaxEntryBytes))
                {
                    // Structural validation only: bounded size and depth, no duplicate properties.
                }

                return JsonSerializer.Deserialize<T>(text, JsonOptions);
            }
            catch (Exception ex)
            {
                _log($"Ignoring unreadable persisted entry '{entry}' ({ex.GetType().Name}).");
                return null;
            }
        }

        private void Persist(string entry, Func<string> serialize)
        {
            if (_store == null)
            {
                return;
            }

            try
            {
                _store.Write(EntryName(entry), serialize());
            }
            catch (Exception ex)
            {
                _log($"Failed to persist '{entry}' ({ex.GetType().Name}); continuing with the in-memory value.");
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
            }
            catch (Exception ex)
            {
                _log($"Failed to delete persisted '{entry}' ({ex.GetType().Name}).");
            }
        }

        private string? SafeAnchor()
        {
            try
            {
                var anchor = _deviceAnchor();
                return string.IsNullOrEmpty(anchor) ? null : anchor;
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

        private static class FileLicenseStoreLimits
        {
            internal const int MaxEntryBytes = 1024 * 1024;
        }

        private sealed class DeviceRecord
        {
            public int Version { get; set; }

            public string? Fingerprint { get; set; }

            public string? Anchor { get; set; }
        }

        private sealed class LastSeenRecord
        {
            public long LastSeen { get; set; }
        }
    }
}
