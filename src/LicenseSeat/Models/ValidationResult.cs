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
    /// Gets or sets the code for validation failure, if applicable.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the message for validation failure, if applicable.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

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
    /// Gets or sets the activation data from validation (if device_id was provided).
    /// </summary>
    [JsonPropertyName("activation")]
    public Activation? Activation { get; set; }

    /// <summary>
    /// Gets or sets the active entitlements from validation.
    /// </summary>
    [JsonPropertyName("active_entitlements")]
    public List<Entitlement>? ActiveEntitlements { get; set; }

    /// <summary>
    /// Gets or sets any warnings from validation.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<ValidationWarning>? Warnings { get; set; }

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
    /// <param name="message">The failure message.</param>
    /// <param name="code">The failure code.</param>
    /// <returns>An invalid <see cref="ValidationResult"/>.</returns>
    public static ValidationResult Failed(string? message, string? code = null)
    {
        return new ValidationResult
        {
            Valid = false,
            Message = message,
            Code = code
        };
    }

    /// <summary>
    /// Creates an offline validation result.
    /// </summary>
    /// <param name="valid">Whether the offline validation succeeded.</param>
    /// <param name="code">The code if validation failed.</param>
    /// <param name="entitlements">The entitlements from offline validation.</param>
    /// <returns>An offline <see cref="ValidationResult"/>.</returns>
    public static ValidationResult OfflineResult(bool valid, string? code = null, List<Entitlement>? entitlements = null)
    {
        return new ValidationResult
        {
            Valid = valid,
            Offline = true,
            Code = code,
            ActiveEntitlements = entitlements
        };
    }
}

/// <summary>
/// Represents an activation in validation results.
/// </summary>
public sealed class Activation
{
    /// <summary>
    /// Gets or sets the activation ID.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the device ID.
    /// </summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    /// <summary>
    /// Gets or sets the license key.
    /// </summary>
    [JsonPropertyName("license_key")]
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Gets or sets when the activation occurred.
    /// </summary>
    [JsonPropertyName("activated_at")]
    public string? ActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets when the activation was deactivated (if applicable).
    /// </summary>
    [JsonPropertyName("deactivated_at")]
    public string? DeactivatedAt { get; set; }

    /// <summary>
    /// Gets or sets the IP address used for activation.
    /// </summary>
    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets metadata associated with the activation.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
