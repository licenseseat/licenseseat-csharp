using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LicenseSeat;

/// <summary>
/// Represents a feature or capability granted by a license.
/// </summary>
public sealed class Entitlement
{
    /// <summary>
    /// Gets or sets the unique key identifying this entitlement.
    /// </summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the entitlement.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the description of the entitlement.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets when this entitlement expires.
    /// Null means the entitlement never expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets custom metadata attached to this entitlement.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets a value indicating whether this entitlement has expired.
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether this entitlement is currently active.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => !IsExpired;

    /// <summary>
    /// Creates a new instance of <see cref="Entitlement"/>.
    /// </summary>
    public Entitlement()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Entitlement"/> with the specified key.
    /// </summary>
    /// <param name="key">The entitlement key.</param>
    public Entitlement(string key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>
    /// Creates a new instance of <see cref="Entitlement"/> with the specified key and expiration.
    /// </summary>
    /// <param name="key">The entitlement key.</param>
    /// <param name="expiresAt">When the entitlement expires.</param>
    public Entitlement(string key, DateTimeOffset? expiresAt)
        : this(key)
    {
        ExpiresAt = expiresAt;
    }
}
