using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LicenseSeat;

/// <summary>
/// Represents the result of a license validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the license is valid.
    /// </summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    /// <summary>
    /// Gets or sets the reason for validation failure, if applicable.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets the reason code for validation failure.
    /// </summary>
    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this result is from offline validation.
    /// </summary>
    [JsonPropertyName("offline")]
    public bool Offline { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is an optimistic (cached) result.
    /// </summary>
    [JsonPropertyName("optimistic")]
    public bool Optimistic { get; set; }

    /// <summary>
    /// Gets or sets the license data from validation.
    /// </summary>
    [JsonPropertyName("license")]
    public License? License { get; set; }

    /// <summary>
    /// Gets or sets the active entitlements from validation.
    /// </summary>
    [JsonPropertyName("active_entitlements")]
    public List<Entitlement>? ActiveEntitlements { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="ValidationResult"/>.
    /// </summary>
    public ValidationResult()
    {
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="license">The validated license.</param>
    /// <returns>A valid <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Success(License? license = null)
    {
        return new ValidationResult
        {
            Valid = true,
            License = license,
            ActiveEntitlements = license?.ActiveEntitlements
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="reason">The reason for failure.</param>
    /// <param name="reasonCode">The reason code.</param>
    /// <returns>An invalid <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Failed(string? reason, string? reasonCode = null)
    {
        return new ValidationResult
        {
            Valid = false,
            Reason = reason,
            ReasonCode = reasonCode
        };
    }

    /// <summary>
    /// Creates an offline validation result.
    /// </summary>
    /// <param name="valid">Whether the offline validation succeeded.</param>
    /// <param name="reasonCode">The reason code if validation failed.</param>
    /// <param name="entitlements">The entitlements from offline validation.</param>
    /// <returns>An offline <see cref="ValidationResult"/>.</returns>
    public static ValidationResult OfflineResult(bool valid, string? reasonCode = null, List<Entitlement>? entitlements = null)
    {
        return new ValidationResult
        {
            Valid = valid,
            Offline = true,
            ReasonCode = reasonCode,
            ActiveEntitlements = entitlements
        };
    }
}
