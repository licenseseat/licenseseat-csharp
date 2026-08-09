#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace LicenseSeat
{

    /// <summary>
    /// Thread-safe in-memory cache for license and offline-verification data.
    /// Values are copied at the cache boundary so caller-owned mutable models
    /// cannot silently change authorization state after validation.
    /// </summary>
    internal sealed class LicenseCache
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            MaxDepth = SecurityValidation.MaxJsonDepth,
            WriteIndented = false
        };

        private readonly object _lock = new object();

        private License? _license;
        private OfflineTokenResponse? _offlineToken;
        private string? _deviceId;
        private readonly Dictionary<string, string> _publicKeys = new Dictionary<string, string>(StringComparer.Ordinal);
        private long _lastSeenTimestamp;

        /// <summary>
        /// Creates a new in-memory license cache.
        /// </summary>
        /// <param name="prefix">Validated namespace retained for storage-provider compatibility.</param>
        public LicenseCache(string prefix)
        {
            if (!SecurityValidation.IsSafeIdentifier(prefix, 1, 64))
            {
                throw new ArgumentException("Cache prefix is invalid.", nameof(prefix));
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
            }
        }

        /// <summary>Clears the cached license.</summary>
        public void ClearLicense()
        {
            lock (_lock)
            {
                _license = null;
            }
        }

        /// <summary>Updates validation state using defensive copies.</summary>
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

        /// <summary>Sets the cached device fingerprint.</summary>
        public void SetDeviceId(string deviceId)
        {
            if (!SecurityValidation.IsSafeText(deviceId, 8, 255))
            {
                throw new ArgumentException("Device fingerprint is invalid.", nameof(deviceId));
            }

            lock (_lock)
            {
                _deviceId = deviceId;
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
            }
        }

        /// <summary>Clears offline token and signing-key material.</summary>
        public void ClearOfflineToken()
        {
            lock (_lock)
            {
                _offlineToken = null;
                _publicKeys.Clear();
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

        /// <summary>Stores a signing key and prevents same-ID key substitution.</summary>
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
                }
            }
        }

        /// <summary>Clears all cached authorization state.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _license = null;
                _offlineToken = null;
                _deviceId = null;
                _publicKeys.Clear();
                _lastSeenTimestamp = 0;
            }
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
    }
}
