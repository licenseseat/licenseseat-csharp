using System;
using System.Text.Json.Serialization;

namespace LicenseSeat;

/// <summary>
/// Represents the result of a license activation operation.
/// </summary>
public sealed class ActivationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether activation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the activation ID.
    /// </summary>
    [JsonPropertyName("activation_id")]
    public string? ActivationId { get; set; }

    /// <summary>
    /// Gets or sets the license key that was activated.
    /// </summary>
    [JsonPropertyName("license_key")]
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Gets or sets the device identifier used for activation.
    /// </summary>
    [JsonPropertyName("device_identifier")]
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets when the activation occurred.
    /// </summary>
    [JsonPropertyName("activated_at")]
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets the license data from activation.
    /// </summary>
    [JsonPropertyName("license")]
    public License? License { get; set; }

    /// <summary>
    /// Gets or sets any additional message from the server.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
