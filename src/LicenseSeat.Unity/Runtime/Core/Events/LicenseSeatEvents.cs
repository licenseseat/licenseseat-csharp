namespace LicenseSeat;

/// <summary>
/// Constants for LicenseSeat SDK event names.
/// </summary>
public static class LicenseSeatEvents
{
    // Activation events
    /// <summary>Fired when activation starts.</summary>
    public const string ActivationStart = "activation:start";

    /// <summary>Fired when activation succeeds.</summary>
    public const string ActivationSuccess = "activation:success";

    /// <summary>Fired when activation fails.</summary>
    public const string ActivationError = "activation:error";

    // Deactivation events
    /// <summary>Fired when deactivation starts.</summary>
    public const string DeactivationStart = "deactivation:start";

    /// <summary>Fired when deactivation succeeds.</summary>
    public const string DeactivationSuccess = "deactivation:success";

    /// <summary>Fired when deactivation fails.</summary>
    public const string DeactivationError = "deactivation:error";

    // Validation events
    /// <summary>Fired when validation starts.</summary>
    public const string ValidationStart = "validation:start";

    /// <summary>Fired when online validation succeeds.</summary>
    public const string ValidationSuccess = "validation:success";

    /// <summary>Fired when validation fails (license invalid).</summary>
    public const string ValidationFailed = "validation:failed";

    /// <summary>Fired when validation encounters an error.</summary>
    public const string ValidationError = "validation:error";

    /// <summary>Fired when auto-validation fails.</summary>
    public const string ValidationAutoFailed = "validation:auto-failed";

    /// <summary>Fired when authentication fails during validation.</summary>
    public const string ValidationAuthFailed = "validation:auth-failed";

    // Offline validation events
    /// <summary>Fired when offline validation succeeds.</summary>
    public const string ValidationOfflineSuccess = "validation:offline-success";

    /// <summary>Fired when offline validation fails.</summary>
    public const string ValidationOfflineFailed = "validation:offline-failed";

    // License events
    /// <summary>Fired when a cached license is loaded.</summary>
    public const string LicenseLoaded = "license:loaded";

    /// <summary>Fired when a license is revoked.</summary>
    public const string LicenseRevoked = "license:revoked";

    // Offline license events
    /// <summary>Fired when fetching offline license.</summary>
    public const string OfflineLicenseFetching = "offlineLicense:fetching";

    /// <summary>Fired when offline license is fetched successfully.</summary>
    public const string OfflineLicenseFetched = "offlineLicense:fetched";

    /// <summary>Fired when offline license fetch fails.</summary>
    public const string OfflineLicenseFetchError = "offlineLicense:fetchError";

    /// <summary>Fired when offline license is ready for use.</summary>
    public const string OfflineLicenseReady = "offlineLicense:ready";

    /// <summary>Fired when offline license signature is verified.</summary>
    public const string OfflineLicenseVerified = "offlineLicense:verified";

    /// <summary>Fired when offline license verification fails.</summary>
    public const string OfflineLicenseVerificationFailed = "offlineLicense:verificationFailed";

    // Auto-validation events
    /// <summary>Fired on each auto-validation cycle.</summary>
    public const string AutoValidationCycle = "autovalidation:cycle";

    /// <summary>Fired when auto-validation stops.</summary>
    public const string AutoValidationStopped = "autovalidation:stopped";

    // Network events
    /// <summary>Fired when network connection is restored.</summary>
    public const string NetworkOnline = "network:online";

    /// <summary>Fired when network connection is lost.</summary>
    public const string NetworkOffline = "network:offline";

    // Auth test events
    /// <summary>Fired when auth test starts.</summary>
    public const string AuthTestStart = "auth_test:start";

    /// <summary>Fired when auth test succeeds.</summary>
    public const string AuthTestSuccess = "auth_test:success";

    /// <summary>Fired when auth test fails.</summary>
    public const string AuthTestError = "auth_test:error";

    // Heartbeat events
    /// <summary>Fired when a heartbeat is sent successfully.</summary>
    public const string HeartbeatSuccess = "heartbeat:success";

    /// <summary>Fired when a heartbeat fails.</summary>
    public const string HeartbeatError = "heartbeat:error";

    // SDK lifecycle events
    /// <summary>Fired when SDK is reset.</summary>
    public const string SdkReset = "sdk:reset";

    /// <summary>Fired when SDK encounters an error.</summary>
    public const string SdkError = "sdk:error";

    /// <summary>Fired when SDK is destroyed.</summary>
    public const string SdkDestroyed = "sdk:destroyed";
}
