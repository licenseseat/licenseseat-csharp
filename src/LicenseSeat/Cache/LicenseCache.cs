using System;
using System.IO;
using System.Text.Json;

namespace LicenseSeat;

/// <summary>
/// In-memory cache for license data with optional file-based persistence.
/// </summary>
internal sealed class LicenseCache
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly string _prefix;
    private readonly string? _persistPath;
    private readonly object _lock = new object();

    private License? _license;
    private OfflineTokenResponse? _offlineToken;
    private string? _deviceId;
    private string? _publicKey;
    private string? _publicKeyId;
    private double _lastSeenTimestamp;

    /// <summary>
    /// Creates a new license cache.
    /// </summary>
    /// <param name="prefix">Storage prefix for file-based persistence.</param>
    /// <param name="persistPath">Optional path for file-based persistence.</param>
    public LicenseCache(string prefix, string? persistPath = null)
    {
        _prefix = prefix ?? "licenseseat_";
        _persistPath = persistPath;

        if (_persistPath != null)
        {
            LoadFromDisk();
        }
    }

    /// <summary>
    /// Gets the cached license.
    /// </summary>
    /// <returns>The cached license, or null if none.</returns>
    public License? GetLicense()
    {
        lock (_lock)
        {
            return _license;
        }
    }

    /// <summary>
    /// Sets the cached license.
    /// </summary>
    /// <param name="license">The license to cache.</param>
    public void SetLicense(License license)
    {
        lock (_lock)
        {
            _license = license;
            PersistToDisk();
        }
    }

    /// <summary>
    /// Clears the cached license.
    /// </summary>
    public void ClearLicense()
    {
        lock (_lock)
        {
            _license = null;
            PersistToDisk();
        }
    }

    /// <summary>
    /// Updates the validation result on the cached license.
    /// </summary>
    /// <param name="result">The validation result.</param>
    public void UpdateValidation(ValidationResult result)
    {
        lock (_lock)
        {
            if (_license != null)
            {
                _license.Validation = result;
                _license.LastValidated = DateTimeOffset.UtcNow;
                if (result.ActiveEntitlements != null)
                {
                    _license.ActiveEntitlements = result.ActiveEntitlements;
                }
                PersistToDisk();
            }
        }
    }

    /// <summary>
    /// Gets the device ID.
    /// </summary>
    /// <returns>The device ID, or null if not set.</returns>
    public string? GetDeviceId()
    {
        lock (_lock)
        {
            return _deviceId ?? _license?.DeviceId;
        }
    }

    /// <summary>
    /// Sets the device ID.
    /// </summary>
    /// <param name="deviceId">The device ID.</param>
    public void SetDeviceId(string deviceId)
    {
        lock (_lock)
        {
            _deviceId = deviceId;
            PersistToDisk();
        }
    }

    /// <summary>
    /// Gets the cached offline token.
    /// </summary>
    /// <returns>The offline token, or null if none.</returns>
    public OfflineTokenResponse? GetOfflineToken()
    {
        lock (_lock)
        {
            return _offlineToken;
        }
    }

    /// <summary>
    /// Sets the cached offline token.
    /// </summary>
    /// <param name="offlineToken">The offline token to cache.</param>
    public void SetOfflineToken(OfflineTokenResponse offlineToken)
    {
        lock (_lock)
        {
            _offlineToken = offlineToken;
            PersistToDisk();
        }
    }

    /// <summary>
    /// Clears the cached offline token.
    /// </summary>
    public void ClearOfflineToken()
    {
        lock (_lock)
        {
            _offlineToken = null;
            _publicKey = null;
            _publicKeyId = null;
            PersistToDisk();
        }
    }

    /// <summary>
    /// Gets a cached public key by key ID.
    /// </summary>
    /// <param name="keyId">The key ID.</param>
    /// <returns>The public key, or null if not cached.</returns>
    public string? GetPublicKey(string keyId)
    {
        lock (_lock)
        {
            return _publicKeyId == keyId ? _publicKey : null;
        }
    }

    /// <summary>
    /// Sets a public key in the cache.
    /// </summary>
    /// <param name="keyId">The key ID.</param>
    /// <param name="publicKey">The public key (Base64 encoded).</param>
    public void SetPublicKey(string keyId, string publicKey)
    {
        lock (_lock)
        {
            _publicKeyId = keyId;
            _publicKey = publicKey;
            PersistToDisk();
        }
    }

    /// <summary>
    /// Gets the last seen timestamp for clock tampering detection.
    /// </summary>
    /// <returns>The last seen timestamp as Unix time in seconds.</returns>
    public double GetLastSeenTimestamp()
    {
        lock (_lock)
        {
            return _lastSeenTimestamp;
        }
    }

    /// <summary>
    /// Sets the last seen timestamp.
    /// </summary>
    /// <param name="timestamp">The timestamp as Unix time in seconds.</param>
    public void SetLastSeenTimestamp(double timestamp)
    {
        lock (_lock)
        {
            _lastSeenTimestamp = timestamp;
            PersistToDisk();
        }
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _license = null;
            _offlineToken = null;
            _deviceId = null;
            _publicKey = null;
            _publicKeyId = null;
            _lastSeenTimestamp = 0;
            PersistToDisk();
        }
    }

    private void PersistToDisk()
    {
        if (_persistPath == null)
        {
            return;
        }

        try
        {
            var data = new CacheData
            {
                License = _license,
                OfflineToken = _offlineToken,
                DeviceId = _deviceId,
                PublicKey = _publicKey,
                PublicKeyId = _publicKeyId,
                LastSeenTimestamp = _lastSeenTimestamp
            };

            var json = JsonSerializer.Serialize(data, JsonOptions);
            var filePath = Path.Combine(_persistPath, $"{_prefix}cache.json");
            File.WriteAllText(filePath, json);
        }
        catch (Exception)
        {
            // Ignore persistence errors - cache is primarily in-memory
        }
    }

    private void LoadFromDisk()
    {
        if (_persistPath == null)
        {
            return;
        }

        try
        {
            var filePath = Path.Combine(_persistPath, $"{_prefix}cache.json");
            if (!File.Exists(filePath))
            {
                return;
            }

            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<CacheData>(json, JsonOptions);

            if (data != null)
            {
                _license = data.License;
                _offlineToken = data.OfflineToken;
                _deviceId = data.DeviceId;
                _publicKey = data.PublicKey;
                _publicKeyId = data.PublicKeyId;
                _lastSeenTimestamp = data.LastSeenTimestamp;
            }
        }
        catch (Exception)
        {
            // Ignore load errors - start with empty cache
        }
    }

    private sealed class CacheData
    {
        public License? License { get; set; }
        public OfflineTokenResponse? OfflineToken { get; set; }
        public string? DeviceId { get; set; }
        public string? PublicKey { get; set; }
        public string? PublicKeyId { get; set; }
        public double LastSeenTimestamp { get; set; }
    }
}
