using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat;

/// <summary>
/// Interface for the LicenseSeat client.
/// Enables dependency injection and unit testing.
/// </summary>
public interface ILicenseSeatClient : IDisposable
{
    /// <summary>
    /// Gets the event bus for subscribing to SDK events.
    /// </summary>
    EventBus Events { get; }

    /// <summary>
    /// Gets a value indicating whether the client is currently online.
    /// </summary>
    bool IsOnline { get; }

    /// <summary>
    /// Gets the current configuration options.
    /// </summary>
    LicenseSeatClientOptions Options { get; }

    /// <summary>
    /// Initializes the SDK by loading cached licenses and starting auto-validation.
    /// Called automatically unless <see cref="LicenseSeatClientOptions.AutoInitialize"/> is false.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Activates a license for this device.
    /// </summary>
    /// <param name="licenseKey">The license key to activate.</param>
    /// <param name="options">Optional activation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated license.</returns>
    /// <exception cref="ApiException">When the API request fails.</exception>
    Task<License> ActivateAsync(
        string licenseKey,
        ActivationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a license.
    /// </summary>
    /// <param name="licenseKey">The license key to validate.</param>
    /// <param name="options">Optional validation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ApiException">When the API request fails and offline fallback is not available.</exception>
    Task<ValidationResult> ValidateAsync(
        string licenseKey,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the current license.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="LicenseException">When no active license is found.</exception>
    /// <exception cref="ApiException">When the API request fails.</exception>
    Task DeactivateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current license status.
    /// </summary>
    /// <returns>The current license status.</returns>
    LicenseStatus GetStatus();

    /// <summary>
    /// Gets the current cached license.
    /// </summary>
    /// <returns>The cached license, or null if none.</returns>
    License? GetCurrentLicense();

    /// <summary>
    /// Checks if a specific entitlement is active.
    /// </summary>
    /// <param name="entitlementKey">The entitlement key to check.</param>
    /// <returns>The entitlement status.</returns>
    EntitlementStatus CheckEntitlement(string entitlementKey);

    /// <summary>
    /// Checks if a specific entitlement is active (simple boolean version).
    /// </summary>
    /// <param name="entitlementKey">The entitlement key to check.</param>
    /// <returns>True if the entitlement is active, false otherwise.</returns>
    bool HasEntitlement(string entitlementKey);

    /// <summary>
    /// Resets the SDK state and clears all cached data.
    /// </summary>
    void Reset();

    /// <summary>
    /// Purges any cached license and related offline assets without making a server call.
    /// Useful when responding to logout events or license revocation notifications.
    /// </summary>
    void PurgeCachedLicense();

    /// <summary>
    /// Sends a heartbeat for the current active license.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HeartbeatAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests authentication with the API.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if authentication is successful.</returns>
    Task<bool> TestAuthAsync(CancellationToken cancellationToken = default);

    // Synchronous wrappers for environments that don't support async (e.g., Unity Editor scripts)

    /// <summary>
    /// Activates a license for this device (synchronous version).
    /// </summary>
    /// <param name="licenseKey">The license key to activate.</param>
    /// <param name="options">Optional activation options.</param>
    /// <returns>The activated license.</returns>
    /// <exception cref="ApiException">When the API request fails.</exception>
    /// <remarks>
    /// This is a synchronous wrapper around <see cref="ActivateAsync"/>.
    /// For better performance and responsiveness, prefer the async version when possible.
    /// </remarks>
    License Activate(string licenseKey, ActivationOptions? options = null);

    /// <summary>
    /// Validates a license (synchronous version).
    /// </summary>
    /// <param name="licenseKey">The license key to validate.</param>
    /// <param name="options">Optional validation options.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ApiException">When the API request fails and offline fallback is not available.</exception>
    /// <remarks>
    /// This is a synchronous wrapper around <see cref="ValidateAsync"/>.
    /// For better performance and responsiveness, prefer the async version when possible.
    /// </remarks>
    ValidationResult Validate(string licenseKey, ValidationOptions? options = null);

    /// <summary>
    /// Deactivates the current license (synchronous version).
    /// </summary>
    /// <exception cref="LicenseException">When no active license is found.</exception>
    /// <exception cref="ApiException">When the API request fails.</exception>
    /// <remarks>
    /// This is a synchronous wrapper around <see cref="DeactivateAsync"/>.
    /// For better performance and responsiveness, prefer the async version when possible.
    /// </remarks>
    void Deactivate();

    /// <summary>
    /// Sends a heartbeat for the current active license (synchronous version).
    /// </summary>
    /// <remarks>
    /// This is a synchronous wrapper around <see cref="HeartbeatAsync"/>.
    /// For better performance and responsiveness, prefer the async version when possible.
    /// </remarks>
    void Heartbeat();

    /// <summary>
    /// Tests authentication with the API (synchronous version).
    /// </summary>
    /// <returns>True if authentication is successful.</returns>
    /// <remarks>
    /// This is a synchronous wrapper around <see cref="TestAuthAsync"/>.
    /// For better performance and responsiveness, prefer the async version when possible.
    /// </remarks>
    bool TestAuth();
}
