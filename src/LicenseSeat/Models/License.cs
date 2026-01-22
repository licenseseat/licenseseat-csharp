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
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the device ID associated with this license activation.
    /// </summary>
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

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
    /// Gets or sets when the license expires.
    /// Null means the license never expires.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the license mode (e.g., "hardware_locked", "floating").
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
    /// Gets or sets the current number of active seats.
    /// </summary>
    [JsonPropertyName("active_seats")]
    public int ActiveSeats { get; set; }

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
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether this license is active.
    /// </summary>
    [JsonIgnore]
    public bool IsActive => Status?.Equals("active", StringComparison.OrdinalIgnoreCase) == true && !IsExpired;

    /// <summary>
    /// Creates a License from API response data.
    /// </summary>
    internal static License FromLicenseData(LicenseData data, string? deviceId = null)
    {
        var license = new License
        {
            Key = data.Key ?? string.Empty,
            DeviceId = deviceId ?? string.Empty,
            Status = data.Status,
            Mode = data.Mode,
            PlanKey = data.PlanKey,
            SeatLimit = data.SeatLimit,
            ActiveSeats = data.ActiveSeats,
            Metadata = data.Metadata
        };

        if (!string.IsNullOrEmpty(data.StartsAt) && DateTimeOffset.TryParse(data.StartsAt, out var startsAt))
        {
            license.StartsAt = startsAt;
        }

        if (!string.IsNullOrEmpty(data.ExpiresAt) && DateTimeOffset.TryParse(data.ExpiresAt, out var expiresAt))
        {
            license.ExpiresAt = expiresAt;
        }

        if (data.Product != null)
        {
            license.Product = new Product
            {
                Slug = data.Product.Slug ?? string.Empty,
                Name = data.Product.Name
            };
        }

        if (data.ActiveEntitlements != null)
        {
            license.ActiveEntitlements = new List<Entitlement>();
            foreach (var ent in data.ActiveEntitlements)
            {
                var entitlement = new Entitlement { Key = ent.Key ?? string.Empty };
                if (!string.IsNullOrEmpty(ent.ExpiresAt) && DateTimeOffset.TryParse(ent.ExpiresAt, out var entExpiresAt))
                {
                    entitlement.ExpiresAt = entExpiresAt;
                }
                if (ent.Metadata != null)
                {
                    entitlement.Metadata = ent.Metadata;
                }
                license.ActiveEntitlements.Add(entitlement);
            }
        }

        return license;
    }
}
