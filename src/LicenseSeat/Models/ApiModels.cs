#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LicenseSeat
{

    /// <summary>
    /// Request model for activating a device.
    /// POST /products/{slug}/licenses/activate
    /// </summary>
    internal sealed class ActivationRequest
    {
        [JsonPropertyName("license_key")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("fingerprint")]
        public string Fingerprint { get; set; } = string.Empty;

        [JsonPropertyName("device_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeviceName { get; set; }

        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Request model for deactivating a device.
    /// POST /products/{slug}/licenses/deactivate
    /// </summary>
    internal sealed class DeactivationRequest
    {
        [JsonPropertyName("license_key")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("fingerprint")]
        public string Fingerprint { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request model for validating a license.
    /// POST /products/{slug}/licenses/validate
    /// </summary>
    internal sealed class ValidationRequest
    {
        [JsonPropertyName("license_key")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("fingerprint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Fingerprint { get; set; }
    }

    /// <summary>
    /// Request model for generating an offline token.
    /// POST /products/{slug}/licenses/offline-token
    /// </summary>
    internal sealed class OfflineTokenRequest
    {
        [JsonPropertyName("license_key")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("fingerprint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Fingerprint { get; set; }

        [JsonPropertyName("ttl_days")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TtlDays { get; set; }
    }

    /// <summary>
    /// Request model for sending a heartbeat.
    /// POST /products/{slug}/licenses/heartbeat
    /// </summary>
    internal sealed class HeartbeatRequest
    {
        [JsonPropertyName("license_key")]
        public string LicenseKey { get; set; } = string.Empty;

        [JsonPropertyName("fingerprint")]
        public string Fingerprint { get; set; } = string.Empty;
    }

    /// <summary>
    /// Response model for heartbeat endpoint.
    /// POST /products/{slug}/licenses/heartbeat
    /// </summary>
    public sealed class HeartbeatResponse
    {
        /// <summary>Gets or sets the object type.</summary>
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        /// <summary>Gets or sets the server-acknowledged timestamp.</summary>
        [JsonPropertyName("received_at")]
        public DateTimeOffset ReceivedAt { get; set; }

        /// <summary>Gets or sets the server license snapshot.</summary>
        [JsonPropertyName("license")]
        public LicenseData? License { get; set; }
    }

    /// <summary>
    /// Nested error object in API responses.
    /// </summary>
    internal sealed class ApiErrorDetail
    {
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Details { get; set; }
    }

    /// <summary>
    /// API error response model.
    /// </summary>
    internal sealed class ApiErrorResponse
    {
        [JsonPropertyName("error")]
        public ApiErrorDetail? Error { get; set; }

        /// <summary>
        /// Fallback message field for simple error formats.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Warning object in validation responses.
    /// </summary>
    public sealed class ValidationWarning
    {
        /// <summary>
        /// Gets or sets the warning code.
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; }

        /// <summary>
        /// Gets or sets the warning message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Response model for validation endpoint.
    /// </summary>
    internal sealed class ValidationResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("valid")]
        public bool Valid { get; set; }

        [JsonPropertyName("code")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Code { get; set; }

        [JsonPropertyName("message")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Message { get; set; }

        [JsonPropertyName("warnings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<ValidationWarning>? Warnings { get; set; }

        [JsonPropertyName("license")]
        public LicenseData? License { get; set; }

        [JsonPropertyName("activation")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ActivationData? Activation { get; set; }
    }

    /// <summary>
    /// Response model for activation endpoint.
    /// </summary>
    internal sealed class ActivationResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("id")]
        [JsonConverter(typeof(StringOrNumberJsonConverter))]
        public string? Id { get; set; }

        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("fingerprint")]
        public string? Fingerprint { get; set; }

        [JsonPropertyName("device_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeviceName { get; set; }

        [JsonPropertyName("license_key")]
        public string? LicenseKey { get; set; }

        [JsonPropertyName("activated_at")]
        public string? ActivatedAt { get; set; }

        [JsonPropertyName("deactivated_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeactivatedAt { get; set; }

        [JsonPropertyName("ip_address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? IpAddress { get; set; }

        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }

        [JsonPropertyName("license")]
        public LicenseData? License { get; set; }
    }

    /// <summary>
    /// Response model for deactivation endpoint.
    /// </summary>
    internal sealed class DeactivationResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("activation_id")]
        [JsonConverter(typeof(StringOrNumberJsonConverter))]
        public string? ActivationId { get; set; }

        [JsonPropertyName("deactivated_at")]
        public string? DeactivatedAt { get; set; }
    }

    /// <summary>
    /// License data in API responses.
    /// </summary>
    public sealed class LicenseData
    {
        /// <summary>Gets or sets the object type.</summary>
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        /// <summary>Gets or sets the license key.</summary>
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        /// <summary>Gets or sets the license status.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets when the license starts.</summary>
        [JsonPropertyName("starts_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StartsAt { get; set; }

        /// <summary>Gets or sets when the license expires.</summary>
        [JsonPropertyName("expires_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExpiresAt { get; set; }

        /// <summary>Gets or sets the license mode.</summary>
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        /// <summary>Gets or sets the plan key.</summary>
        [JsonPropertyName("plan_key")]
        public string? PlanKey { get; set; }

        /// <summary>Gets or sets the seat limit.</summary>
        [JsonPropertyName("seat_limit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SeatLimit { get; set; }

        /// <summary>Gets or sets the number of active seats.</summary>
        [JsonPropertyName("active_seats")]
        public int ActiveSeats { get; set; }

        /// <summary>Gets or sets the active entitlements.</summary>
        [JsonPropertyName("active_entitlements")]
        public List<EntitlementData>? ActiveEntitlements { get; set; }

        /// <summary>Gets or sets the metadata.</summary>
        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>Gets or sets the product data.</summary>
        [JsonPropertyName("product")]
        public ProductData? Product { get; set; }
    }

    /// <summary>
    /// Activation data in API responses.
    /// </summary>
    public sealed class ActivationData
    {
        /// <summary>Gets or sets the activation ID.</summary>
        [JsonPropertyName("id")]
        [JsonConverter(typeof(StringOrNumberJsonConverter))]
        public string? Id { get; set; }

        /// <summary>Gets or sets the device ID.</summary>
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; set; }

        /// <summary>Gets or sets the canonical device fingerprint.</summary>
        [JsonPropertyName("fingerprint")]
        public string? Fingerprint { get; set; }

        /// <summary>Gets or sets the device name.</summary>
        [JsonPropertyName("device_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeviceName { get; set; }

        /// <summary>Gets or sets the license key.</summary>
        [JsonPropertyName("license_key")]
        public string? LicenseKey { get; set; }

        /// <summary>Gets or sets when the activation occurred.</summary>
        [JsonPropertyName("activated_at")]
        public string? ActivatedAt { get; set; }

        /// <summary>Gets or sets when the activation was deactivated.</summary>
        [JsonPropertyName("deactivated_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeactivatedAt { get; set; }

        /// <summary>Gets or sets the IP address.</summary>
        [JsonPropertyName("ip_address")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? IpAddress { get; set; }

        /// <summary>Gets or sets the metadata.</summary>
        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Entitlement data in API responses.
    /// </summary>
    public sealed class EntitlementData
    {
        /// <summary>Gets or sets the entitlement key.</summary>
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        /// <summary>Gets or sets when the entitlement expires.</summary>
        [JsonPropertyName("expires_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ExpiresAt { get; set; }

        /// <summary>Gets or sets the metadata.</summary>
        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Product data in API responses.
    /// </summary>
    public sealed class ProductData
    {
        /// <summary>Gets or sets the object type.</summary>
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        /// <summary>Gets or sets the product slug.</summary>
        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        /// <summary>Gets or sets the product name.</summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// Response model for signing key endpoint.
    /// GET /signing_keys/{key_id}
    /// </summary>
    internal sealed class SigningKeyResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("key_id")]
        public string? KeyId { get; set; }

        [JsonPropertyName("algorithm")]
        public string? Algorithm { get; set; }

        [JsonPropertyName("public_key")]
        public string? PublicKey { get; set; }

        [JsonPropertyName("created_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    /// <summary>
    /// Offline token response model.
    /// POST /products/{slug}/licenses/offline-token
    /// </summary>
    public sealed class OfflineTokenResponse
    {
        /// <summary>Gets unexpected envelope fields captured for strict verification.</summary>
        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }

        /// <summary>Gets or sets the object type.</summary>
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        /// <summary>Gets or sets the token payload.</summary>
        [JsonPropertyName("token")]
        public OfflineToken? Token { get; set; }

        /// <summary>Gets or sets the signature block.</summary>
        [JsonPropertyName("signature")]
        public OfflineTokenSignature? Signature { get; set; }

        /// <summary>Gets or sets the canonical JSON string used for signature verification.</summary>
        [JsonPropertyName("canonical")]
        public string? Canonical { get; set; }
    }

    /// <summary>
    /// Token payload in offline token response.
    /// </summary>
    public sealed class OfflineToken
    {
        /// <summary>Gets unexpected payload fields captured for strict verification.</summary>
        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }

        /// <summary>Gets or sets the schema version.</summary>
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; set; }

        /// <summary>Gets or sets the license key.</summary>
        [JsonPropertyName("license_key")]
        public string? LicenseKey { get; set; }

        /// <summary>Gets or sets the product slug.</summary>
        [JsonPropertyName("product_slug")]
        public string? ProductSlug { get; set; }

        /// <summary>Gets or sets the plan key.</summary>
        [JsonPropertyName("plan_key")]
        public string? PlanKey { get; set; }

        /// <summary>Gets or sets the license mode.</summary>
        [JsonPropertyName("mode")]
        public string? Mode { get; set; }

        /// <summary>Gets or sets the seat limit.</summary>
        [JsonPropertyName("seat_limit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SeatLimit { get; set; }

        /// <summary>Gets or sets the device ID.</summary>
        [JsonPropertyName("device_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? DeviceId { get; set; }

        /// <summary>Gets or sets the canonical device fingerprint.</summary>
        [JsonPropertyName("fingerprint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Fingerprint { get; set; }

        /// <summary>Gets or sets the issued-at timestamp (Unix seconds).</summary>
        [JsonPropertyName("iat")]
        public long Iat { get; set; }

        /// <summary>Gets or sets the expiration timestamp (Unix seconds).</summary>
        [JsonPropertyName("exp")]
        public long Exp { get; set; }

        /// <summary>Gets or sets the not-before timestamp (Unix seconds).</summary>
        [JsonPropertyName("nbf")]
        public long Nbf { get; set; }

        /// <summary>Gets or sets the license expiration timestamp (Unix seconds).</summary>
        [JsonPropertyName("license_expires_at")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? LicenseExpiresAt { get; set; }

        /// <summary>Gets or sets the signing key ID.</summary>
        [JsonPropertyName("kid")]
        public string? Kid { get; set; }

        /// <summary>Gets or sets the entitlements.</summary>
        [JsonPropertyName("entitlements")]
        public List<OfflineEntitlement>? Entitlements { get; set; }

        /// <summary>Gets or sets the metadata.</summary>
        [JsonPropertyName("metadata")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Entitlement in offline token.
    /// </summary>
    public sealed class OfflineEntitlement
    {
        /// <summary>Gets unexpected entitlement fields captured for strict verification.</summary>
        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }

        /// <summary>Gets or sets the entitlement key.</summary>
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        /// <summary>Gets or sets the expiration timestamp (Unix seconds).</summary>
        [JsonPropertyName("expires_at")]
        // The signed Ruby v1 contract emits this member as explicit JSON null
        // for non-expiring entitlements. It must survive typed reconstruction
        // so visible claims can be compared with the canonical signed payload.
        [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        public long? ExpiresAt { get; set; }
    }

    /// <summary>
    /// Signature block in offline token response.
    /// </summary>
    public sealed class OfflineTokenSignature
    {
        /// <summary>Gets unexpected signature fields captured for strict verification.</summary>
        [JsonExtensionData]
        public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; set; }

        /// <summary>Gets or sets the signature algorithm.</summary>
        [JsonPropertyName("algorithm")]
        public string? Algorithm { get; set; }

        /// <summary>Gets or sets the signing key ID.</summary>
        [JsonPropertyName("key_id")]
        public string? KeyId { get; set; }

        /// <summary>Gets or sets the signature value (Base64URL encoded).</summary>
        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    /// <summary>
    /// Response model for health endpoint.
    /// GET /health
    /// </summary>
    internal sealed class HealthResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("api_version")]
        public string? ApiVersion { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }

    /// <summary>
    /// Response model for the protected authentication test endpoint.
    /// </summary>
    internal sealed class AuthResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("api_version")]
        public string? ApiVersion { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }
    }

    /// <summary>
    /// Release data in API responses.
    /// </summary>
    public sealed class ReleaseData
    {
        /// <summary>Gets or sets the object type.</summary>
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        /// <summary>Gets or sets the version.</summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        /// <summary>Gets or sets the release channel.</summary>
        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        /// <summary>Gets or sets the platform.</summary>
        [JsonPropertyName("platform")]
        public string? Platform { get; set; }

        /// <summary>Gets or sets when the release was published.</summary>
        [JsonPropertyName("published_at")]
        public string? PublishedAt { get; set; }

        /// <summary>Gets or sets the product slug.</summary>
        [JsonPropertyName("product_slug")]
        public string? ProductSlug { get; set; }
    }

    /// <summary>
    /// List response wrapper.
    /// </summary>
    internal sealed class ListResponse<T>
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("data")]
        public List<T>? Data { get; set; }

        [JsonPropertyName("has_more")]
        public bool HasMore { get; set; }

        [JsonPropertyName("next_cursor")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? NextCursor { get; set; }
    }

    /// <summary>
    /// Download token response.
    /// POST /products/{slug}/releases/{version}/download_token
    /// </summary>
    internal sealed class DownloadTokenResponse
    {
        [JsonPropertyName("object")]
        public string? Object { get; set; }

        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("expires_at")]
        public string? ExpiresAt { get; set; }
    }
}
