using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat;

/// <summary>
/// Static convenience API for LicenseSeat.
/// Provides a singleton pattern for simple usage scenarios.
/// </summary>
/// <example>
/// <code>
/// // Configure at startup
/// LicenseSeat.Configure("your-api-key", "your-product-slug");
///
/// // Use anywhere in your app
/// var license = await LicenseSeat.Activate("LICENSE-KEY");
/// var status = LicenseSeat.GetStatus();
/// </code>
/// </example>
public static class LicenseSeat
{
    private static readonly object _sharedLock = new object();
    private static LicenseSeatClient? _shared;

    /// <summary>
    /// Gets the shared singleton instance.
    /// Returns null if <see cref="Configure(string, string, Action{LicenseSeatClientOptions}?)"/> has not been called.
    /// </summary>
    public static LicenseSeatClient? Shared
    {
        get
        {
            lock (_sharedLock)
            {
                return _shared;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the shared instance has been configured.
    /// </summary>
    public static bool IsConfigured
    {
        get
        {
            lock (_sharedLock)
            {
                return _shared != null;
            }
        }
    }

    /// <summary>
    /// Configures the shared singleton instance with the specified API key and product slug.
    /// </summary>
    /// <param name="apiKey">Your LicenseSeat API key.</param>
    /// <param name="productSlug">The product slug for API requests.</param>
    /// <param name="configure">Optional action to customize configuration options.</param>
    /// <returns>The configured client instance.</returns>
    /// <example>
    /// <code>
    /// // Simple configuration
    /// LicenseSeat.Configure("your-api-key", "your-product");
    ///
    /// // With custom options
    /// LicenseSeat.Configure("your-api-key", "your-product", options => {
    ///     options.ApiBaseUrl = "https://custom.api.com";
    ///     options.Debug = true;
    /// });
    /// </code>
    /// </example>
    public static LicenseSeatClient Configure(string apiKey, string productSlug, Action<LicenseSeatClientOptions>? configure = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be empty", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(productSlug))
        {
            throw new ArgumentException("Product slug cannot be empty", nameof(productSlug));
        }

        var options = new LicenseSeatClientOptions(apiKey, productSlug);
        configure?.Invoke(options);

        lock (_sharedLock)
        {
            _shared?.Dispose();
            _shared = new LicenseSeatClient(options);
            return _shared;
        }
    }

    /// <summary>
    /// Configures the shared singleton instance with the specified options.
    /// </summary>
    /// <param name="options">Configuration options.</param>
    /// <param name="force">If true, replaces any existing instance. If false, returns existing instance.</param>
    /// <returns>The configured client instance.</returns>
    public static LicenseSeatClient Configure(LicenseSeatClientOptions options, bool force = false)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        lock (_sharedLock)
        {
            if (_shared != null && !force)
            {
                return _shared;
            }

            _shared?.Dispose();
            _shared = new LicenseSeatClient(options);
            return _shared;
        }
    }

    /// <summary>
    /// Activates a license using the shared instance.
    /// </summary>
    /// <param name="licenseKey">The license key to activate.</param>
    /// <param name="options">Optional activation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated license.</returns>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static Task<License> Activate(
        string licenseKey,
        ActivationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetRequiredShared().ActivateAsync(licenseKey, options, cancellationToken);
    }

    /// <summary>
    /// Validates a license using the shared instance.
    /// </summary>
    /// <param name="licenseKey">The license key to validate.</param>
    /// <param name="options">Optional validation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static Task<ValidationResult> Validate(
        string licenseKey,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GetRequiredShared().ValidateAsync(licenseKey, options, cancellationToken);
    }

    /// <summary>
    /// Deactivates the current license using the shared instance.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static Task Deactivate(CancellationToken cancellationToken = default)
    {
        return GetRequiredShared().DeactivateAsync(cancellationToken);
    }

    /// <summary>
    /// Checks an entitlement using the shared instance.
    /// </summary>
    /// <param name="entitlementKey">The entitlement key to check.</param>
    /// <returns>The entitlement status.</returns>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static EntitlementStatus Entitlement(string entitlementKey)
    {
        return GetRequiredShared().CheckEntitlement(entitlementKey);
    }

    /// <summary>
    /// Checks if an entitlement is active using the shared instance.
    /// </summary>
    /// <param name="entitlementKey">The entitlement key to check.</param>
    /// <returns>True if the entitlement is active.</returns>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static bool HasEntitlement(string entitlementKey)
    {
        return GetRequiredShared().HasEntitlement(entitlementKey);
    }

    /// <summary>
    /// Gets the current status using the shared instance.
    /// </summary>
    /// <returns>The current license status.</returns>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static LicenseStatus GetStatus()
    {
        return GetRequiredShared().GetStatus();
    }

    /// <summary>
    /// Gets the current license using the shared instance.
    /// </summary>
    /// <returns>The current license, or null if none.</returns>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static License? GetCurrentLicense()
    {
        return GetRequiredShared().GetCurrentLicense();
    }

    /// <summary>
    /// Resets the shared instance state.
    /// </summary>
    /// <exception cref="InvalidOperationException">When Configure has not been called.</exception>
    public static void Reset()
    {
        GetRequiredShared().Reset();
    }

    /// <summary>
    /// Disposes the shared instance and clears the singleton.
    /// </summary>
    public static void Shutdown()
    {
        lock (_sharedLock)
        {
            _shared?.Dispose();
            _shared = null;
        }
    }

    private static LicenseSeatClient GetRequiredShared()
    {
        var client = Shared;
        if (client == null)
        {
            throw new InvalidOperationException(
                "LicenseSeat.Configure() must be called before using static methods. " +
                "Call Configure() at application startup.");
        }
        return client;
    }
}
