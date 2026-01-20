using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LicenseSeat;

/// <summary>
/// Represents a license in the LicenseSeat system.
/// </summary>
public sealed class License
{
    /// <summary>
    /// Gets or sets the license key.
    /// </summary>
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device identifier associated with this license.
    /// </summary>
    [JsonPropertyName("device_identifier")]
    public string DeviceIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the license status.
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets when the license starts being valid.
    /// </summary>
    [JsonPropertyName("starts_at")]
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>
    /// Gets or sets when the license ends (expires).
    /// Null means the license never expires.
    /// </summary>
    [JsonPropertyName("ends_at")]
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>
    /// Gets or sets the license mode (e.g., "hardware_locked", "named_user").
    /// </summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>
    /// Gets or sets the plan key associated with this license.
    /// </summary>
    [JsonPropertyName("plan_key")]
    public string? PlanKey { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of seats (devices) allowed.
    /// </summary>
    [JsonPropertyName("seat_limit")]
    public int? SeatLimit { get; set; }

    /// <summary>
    /// Gets or sets the current number of active activations.
    /// </summary>
    [JsonPropertyName("active_activations_count")]
    public int ActiveActivationsCount { get; set; }

    /// <summary>
    /// Gets or sets the list of active entitlements for this license.
    /// </summary>
    [JsonPropertyName("active_entitlements")]
    public List<Entitlement>? ActiveEntitlements { get; set; }

    /// <summary>
    /// Gets or sets the product associated with this license.
    /// </summary>
    [JsonPropertyName("product")]
    public Product? Product { get; set; }

    /// <summary>
    /// Gets or sets custom metadata attached to this license.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets when this license was activated on the current device.
    /// </summary>
    [JsonPropertyName("activated_at")]
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets when this license was last validated.
    /// </summary>
    [JsonPropertyName("last_validated")]
    public DateTimeOffset? LastValidated { get; set; }

    /// <summary>
    /// Gets or sets the last validation result.
    /// </summary>
    [JsonIgnore]
    public ValidationResult? Validation { get; set; }

    /// <summary>
    /// Gets a value indicating whether this license has expired.
    /// </summary>
    [JsonIgnore]
    public bool IsExpired => EndsAt.HasValue && EndsAt.Value < DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether this license is active.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => Status?.Equals("active", StringComparison.OrdinalIgnoreCase) == true && !IsExpired;
}
