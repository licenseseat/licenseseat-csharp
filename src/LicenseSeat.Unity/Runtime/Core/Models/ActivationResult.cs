using System;
using System.Collections.Generic;
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
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the license key that was activated.
    /// </summary>
    [JsonPropertyName("license_key")]
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Gets or sets the device ID used for activation.
    /// </summary>
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the device name.
    /// </summary>
    [JsonPropertyName("device_name")]
    public string? DeviceName { get; set; }

    /// <summary>
    /// Gets or sets when the activation occurred.
    /// </summary>
    [JsonPropertyName("activated_at")]
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>
    /// Gets or sets the IP address used for activation.
    /// </summary>
    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the license data from activation.
    /// </summary>
    [JsonPropertyName("license")]
    public License? License { get; set; }

    /// <summary>
    /// Gets or sets metadata associated with the activation.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Creates an ActivationResult from an API response.
    /// </summary>
    internal static ActivationResult FromResponse(ActivationResponse response, string deviceId)
    {
        var result = new ActivationResult
        {
            Success = true,
            Id = response.Id,
            LicenseKey = response.LicenseKey,
            DeviceId = response.DeviceId ?? deviceId,
            DeviceName = response.DeviceName,
            IpAddress = response.IpAddress,
            Metadata = response.Metadata
        };

        if (!string.IsNullOrEmpty(response.ActivatedAt) && DateTimeOffset.TryParse(response.ActivatedAt, out var activatedAt))
        {
            result.ActivatedAt = activatedAt;
        }

        if (response.License != null)
        {
            result.License = License.FromLicenseData(response.License, deviceId);
            result.License.ActivatedAt = result.ActivatedAt;
        }

        return result;
    }
}
