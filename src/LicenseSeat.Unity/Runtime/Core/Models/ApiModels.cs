using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LicenseSeat;

/// <summary>
/// Request model for activating a license.
/// </summary>
internal sealed class ActivationRequest
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("device_identifier")]
    public string DeviceIdentifier { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("software_release_date")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SoftwareReleaseDate { get; set; }
}

/// <summary>
/// Request model for deactivating a license.
/// </summary>
internal sealed class DeactivationRequest
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("device_identifier")]
    public string DeviceIdentifier { get; set; } = string.Empty;
}

/// <summary>
/// Request model for validating a license.
/// </summary>
internal sealed class ValidationRequest
{
    [JsonPropertyName("license_key")]
    public string LicenseKey { get; set; } = string.Empty;

    [JsonPropertyName("device_identifier")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceIdentifier { get; set; }

    [JsonPropertyName("product_slug")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProductSlug { get; set; }
}

/// <summary>
/// API error response model.
/// </summary>
internal sealed class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Response model for validation endpoint.
/// </summary>
internal sealed class ValidationResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("license")]
    public License? License { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("reason_code")]
    public string? ReasonCode { get; set; }
}

/// <summary>
/// Response model for public key endpoint.
/// </summary>
internal sealed class PublicKeyResponse
{
    [JsonPropertyName("public_key_b64")]
    public string? PublicKeyB64 { get; set; }

    [JsonPropertyName("kid")]
    public string? KeyId { get; set; }
}

/// <summary>
/// Signed offline license data.
/// </summary>
public sealed class SignedOfflineLicense
{
    /// <summary>
    /// Gets or sets the key ID used to sign this license.
    /// </summary>
    [JsonPropertyName("kid")]
    public string? KeyId { get; set; }

    /// <summary>
    /// Gets or sets the license payload.
    /// </summary>
    [JsonPropertyName("payload")]
    public OfflineLicensePayload? Payload { get; set; }

    /// <summary>
    /// Gets or sets the Base64URL-encoded signature.
    /// </summary>
    [JsonPropertyName("signature_b64u")]
    public string? SignatureB64U { get; set; }
}

/// <summary>
/// Payload of a signed offline license.
/// </summary>
public sealed class OfflineLicensePayload
{
    /// <summary>
    /// Gets or sets the license key.
    /// </summary>
    [JsonPropertyName("lic_k")]
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Gets or sets the key ID.
    /// </summary>
    [JsonPropertyName("kid")]
    public string? KeyId { get; set; }

    /// <summary>
    /// Gets or sets when the offline license expires.
    /// </summary>
    [JsonPropertyName("exp_at")]
    public string? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets when the offline license was issued.
    /// </summary>
    [JsonPropertyName("iss_at")]
    public string? IssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the entitlements (short form key).
    /// </summary>
    [JsonPropertyName("ents")]
    public List<OfflineEntitlement>? Entitlements { get; set; }

    /// <summary>
    /// Gets or sets the active entitlements (alternate key for Swift SDK compatibility).
    /// </summary>
    [JsonPropertyName("active_ents")]
    public List<OfflineEntitlement>? ActiveEntitlements { get; set; }

    /// <summary>
    /// Gets or sets the active entitlements (long form key for Swift SDK compatibility).
    /// </summary>
    [JsonPropertyName("active_entitlements")]
    public List<OfflineEntitlement>? ActiveEntitlementsLong { get; set; }

    /// <summary>
    /// Gets or sets custom data.
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object>? Data { get; set; }

    /// <summary>
    /// Gets all entitlements from any supported property.
    /// </summary>
    /// <returns>The list of entitlements, or an empty list if none.</returns>
    public List<OfflineEntitlement> GetAllEntitlements()
    {
        return ActiveEntitlements ?? ActiveEntitlementsLong ?? Entitlements ?? new List<OfflineEntitlement>();
    }
}

/// <summary>
/// Entitlement in offline license payload.
/// </summary>
public sealed class OfflineEntitlement
{
    /// <summary>
    /// Gets or sets the entitlement key.
    /// </summary>
    [JsonPropertyName("k")]
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets when the entitlement expires.
    /// </summary>
    [JsonPropertyName("exp")]
    public string? ExpiresAt { get; set; }
}
