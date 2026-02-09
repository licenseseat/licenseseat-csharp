using System;

namespace LicenseSeat;

/// <summary>
/// Configuration options for the LicenseSeat SDK.
/// </summary>
public sealed class LicenseSeatClientOptions
{
    /// <summary>
    /// The default API base URL for LicenseSeat.
    /// </summary>
    public const string DefaultApiBaseUrl = "https://licenseseat.com/api/v1";

    /// <summary>
    /// The default auto-validation interval (1 hour).
    /// </summary>
    public static readonly TimeSpan DefaultAutoValidateInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// The default network recheck interval when offline (30 seconds).
    /// </summary>
    public static readonly TimeSpan DefaultNetworkRecheckInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The default retry delay for failed API requests (1 second).
    /// </summary>
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The default offline license refresh interval (72 hours).
    /// </summary>
    public static readonly TimeSpan DefaultOfflineLicenseRefreshInterval = TimeSpan.FromHours(72);

    /// <summary>
    /// The default maximum clock skew tolerance (5 minutes).
    /// </summary>
    public static readonly TimeSpan DefaultMaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the API key for authentication.
    /// Required for all authenticated API requests.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the product slug for API requests.
    /// Required for all license operations.
    /// </summary>
    public string? ProductSlug { get; set; }

    /// <summary>
    /// Gets or sets the base URL for the LicenseSeat API.
    /// Defaults to <see cref="DefaultApiBaseUrl"/>.
    /// </summary>
    public string ApiBaseUrl { get; set; } = DefaultApiBaseUrl;

    /// <summary>
    /// Gets or sets the storage prefix for cached data.
    /// Defaults to "licenseseat_".
    /// </summary>
    public string StoragePrefix { get; set; } = "licenseseat_";

    /// <summary>
    /// Gets or sets the interval between automatic license validations.
    /// Set to <see cref="TimeSpan.Zero"/> to disable auto-validation.
    /// Defaults to 1 hour.
    /// </summary>
    public TimeSpan AutoValidateInterval { get; set; } = DefaultAutoValidateInterval;

    /// <summary>
    /// Gets or sets the interval for checking network connectivity when offline.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan NetworkRecheckInterval { get; set; } = DefaultNetworkRecheckInterval;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed API requests.
    /// Defaults to 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retry attempts.
    /// The actual delay uses exponential backoff: delay * 2^attempt.
    /// Defaults to 1 second.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = DefaultRetryDelay;

    /// <summary>
    /// Gets or sets whether debug logging is enabled.
    /// Defaults to false.
    /// </summary>
    public bool Debug { get; set; }

    /// <summary>
    /// Gets or sets the offline fallback mode.
    /// Defaults to <see cref="OfflineFallbackMode.Disabled"/>.
    /// </summary>
    public OfflineFallbackMode OfflineFallbackMode { get; set; } = OfflineFallbackMode.Disabled;

    /// <summary>
    /// Gets or sets the interval for refreshing offline license data.
    /// Defaults to 72 hours.
    /// </summary>
    public TimeSpan OfflineLicenseRefreshInterval { get; set; } = DefaultOfflineLicenseRefreshInterval;

    /// <summary>
    /// Gets or sets the maximum number of days a license can be used offline
    /// without server validation. Set to 0 to disable this limit.
    /// Only applies when <see cref="OfflineFallbackMode"/> is enabled.
    /// Defaults to 0 (disabled).
    /// </summary>
    public int MaxOfflineDays { get; set; }

    /// <summary>
    /// Gets or sets the maximum clock skew tolerance for offline validation.
    /// Used to detect clock tampering.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan MaxClockSkew { get; set; } = DefaultMaxClockSkew;

    /// <summary>
    /// Gets or sets whether the SDK should automatically initialize on construction.
    /// When true, cached licenses are loaded and auto-validation starts automatically.
    /// Defaults to true.
    /// </summary>
    public bool AutoInitialize { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include device telemetry with API requests.
    /// Set to false to disable telemetry (e.g., for GDPR compliance).
    /// Default: true.
    /// </summary>
    public bool TelemetryEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom device ID.
    /// If not set, a device ID will be automatically generated.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Gets or sets the HTTP request timeout.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a custom HTTP client adapter for making API requests.
    /// When null, a default adapter using <see cref="System.Net.Http.HttpClient"/> is used.
    /// Set this to use custom HTTP implementations (e.g., UnityWebRequest for Unity).
    /// </summary>
    /// <remarks>
    /// This is primarily intended for Unity integration where the standard
    /// HttpClient does not work on WebGL builds. Unity users should provide
    /// a UnityWebRequest-based adapter implementation.
    /// </remarks>
    public IHttpClientAdapter? HttpClientAdapter { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="LicenseSeatClientOptions"/> with default values.
    /// </summary>
    public LicenseSeatClientOptions()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="LicenseSeatClientOptions"/> with the specified API key.
    /// </summary>
    /// <param name="apiKey">The API key for authentication.</param>
    public LicenseSeatClientOptions(string apiKey)
    {
        ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    /// <summary>
    /// Creates a new instance of <see cref="LicenseSeatClientOptions"/> with the specified API key and product slug.
    /// </summary>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="productSlug">The product slug for API requests.</param>
    public LicenseSeatClientOptions(string apiKey, string productSlug)
    {
        ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        ProductSlug = productSlug ?? throw new ArgumentNullException(nameof(productSlug));
    }

    /// <summary>
    /// Creates a copy of this options instance.
    /// </summary>
    /// <returns>A new <see cref="LicenseSeatClientOptions"/> instance with the same values.</returns>
    public LicenseSeatClientOptions Clone()
    {
        return new LicenseSeatClientOptions
        {
            ApiKey = ApiKey,
            ProductSlug = ProductSlug,
            ApiBaseUrl = ApiBaseUrl,
            StoragePrefix = StoragePrefix,
            AutoValidateInterval = AutoValidateInterval,
            NetworkRecheckInterval = NetworkRecheckInterval,
            MaxRetries = MaxRetries,
            RetryDelay = RetryDelay,
            Debug = Debug,
            OfflineFallbackMode = OfflineFallbackMode,
            OfflineLicenseRefreshInterval = OfflineLicenseRefreshInterval,
            MaxOfflineDays = MaxOfflineDays,
            MaxClockSkew = MaxClockSkew,
            AutoInitialize = AutoInitialize,
            TelemetryEnabled = TelemetryEnabled,
            DeviceId = DeviceId,
            HttpTimeout = HttpTimeout,
            HttpClientAdapter = HttpClientAdapter
        };
    }

    /// <summary>
    /// Validates the options and throws if any required values are missing or invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            throw new InvalidOperationException("ApiBaseUrl cannot be empty.");
        }

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            throw new InvalidOperationException("ApiBaseUrl must be a valid HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(ProductSlug))
        {
            throw new InvalidOperationException("ProductSlug is required for all license operations.");
        }

        if (MaxRetries < 0)
        {
            throw new InvalidOperationException("MaxRetries cannot be negative.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("RetryDelay cannot be negative.");
        }

        if (AutoValidateInterval < TimeSpan.Zero)
        {
            throw new InvalidOperationException("AutoValidateInterval cannot be negative.");
        }

        if (MaxOfflineDays < 0)
        {
            throw new InvalidOperationException("MaxOfflineDays cannot be negative.");
        }

        if (HttpTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("HttpTimeout must be positive.");
        }
    }
}

/// <summary>
/// Specifies when offline validation should be used as a fallback.
/// </summary>
public enum OfflineFallbackMode
{
    /// <summary>
    /// Offline fallback is disabled. Network failures will throw exceptions.
    /// This is the default (strict) mode.
    /// </summary>
    Disabled,

    /// <summary>
    /// Use offline validation only for network-related errors
    /// (connection failures, timeouts, DNS errors).
    /// Server errors (4xx, 5xx) will still throw exceptions.
    /// </summary>
    NetworkOnly,

    /// <summary>
    /// Always attempt offline validation when online validation fails,
    /// regardless of the error type.
    /// </summary>
    Always
}
