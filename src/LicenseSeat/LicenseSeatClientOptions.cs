#nullable enable
using System;

namespace LicenseSeat
{

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
        /// The default maximum serialized request size (1 MiB).
        /// </summary>
        public const int DefaultMaxRequestBodyBytes = 1024 * 1024;

        /// <summary>
        /// The default maximum response size (1 MiB).
        /// </summary>
        public const int DefaultMaxResponseBodyBytes = 1024 * 1024;

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
        /// Gets or sets whether plaintext HTTP is explicitly allowed.
        /// Keep this disabled outside isolated local development environments.
        /// </summary>
        public bool AllowInsecureHttp { get; set; }

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
        /// without server validation. Set to 0 to disable offline authority.
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
        /// Gets or sets the interval between heartbeat sends.
        /// Set to <see cref="TimeSpan.Zero"/> or negative to disable the separate heartbeat timer.
        /// Defaults to 5 minutes.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the application version for telemetry.
        /// If not set, the SDK attempts to detect it from the entry assembly.
        /// </summary>
        public string? AppVersion { get; set; }

        /// <summary>
        /// Gets or sets the application build identifier for telemetry.
        /// If not set, the SDK attempts to detect it from the entry assembly's informational version.
        /// </summary>
        public string? AppBuild { get; set; }

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
        /// Gets or sets the maximum serialized JSON request size in bytes.
        /// Defaults to 1 MiB.
        /// </summary>
        public int MaxRequestBodyBytes { get; set; } = DefaultMaxRequestBodyBytes;

        /// <summary>
        /// Gets or sets the maximum HTTP response body size in bytes.
        /// Defaults to 1 MiB.
        /// </summary>
        public int MaxResponseBodyBytes { get; set; } = DefaultMaxResponseBodyBytes;

        /// <summary>
        /// Gets or sets a custom HTTP client adapter for making API requests.
        /// When null, a default adapter using <see cref="System.Net.Http.HttpClient"/> is used.
        /// Set this to use custom HTTP implementations (e.g., UnityWebRequest for Unity).
        /// </summary>
        /// <remarks>
        /// This is an explicit trust boundary. A custom adapter must enforce TLS,
        /// disable redirects and cookies, scope credentials to the configured
        /// origin and base path, honor cancellation/timeouts, and bound request
        /// and response bodies. The Unity package supplies its audited adapter.
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
                AllowInsecureHttp = AllowInsecureHttp,
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
                HeartbeatInterval = HeartbeatInterval,
                AppVersion = AppVersion,
                AppBuild = AppBuild,
                DeviceId = DeviceId,
                HttpTimeout = HttpTimeout,
                MaxRequestBodyBytes = MaxRequestBodyBytes,
                MaxResponseBodyBytes = MaxResponseBodyBytes,
                HttpClientAdapter = HttpClientAdapter
            };
        }

        /// <summary>
        /// Validates the options and throws if any required values are missing or invalid.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
        public void Validate()
        {
            if (!SecurityValidation.IsAsciiPrintable(ApiKey, 1, 4096))
            {
                throw new InvalidOperationException("ApiKey is required and must contain 1 to 4096 printable ASCII characters without spaces.");
            }

            if (!SecurityValidation.IsSafeIdentifier(ProductSlug, 1, 128))
            {
                throw new InvalidOperationException("ProductSlug is required and must be a safe identifier no longer than 128 bytes.");
            }

            if (!SecurityValidation.IsAsciiPrintable(ApiBaseUrl, 1, 2048))
            {
                throw new InvalidOperationException("ApiBaseUrl must contain 1 to 2048 printable ASCII characters without spaces.");
            }

            if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                throw new InvalidOperationException("ApiBaseUrl must be a valid HTTP or HTTPS URL.");
            }

            if (!string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) ||
                string.IsNullOrEmpty(uri.Host) ||
                ApiBaseUrl.IndexOf('\\') >= 0 ||
                ApiBaseUrl.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ApiBaseUrl.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ApiBaseUrl.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) >= 0 ||
                ApiBaseUrl.IndexOf("%25", StringComparison.OrdinalIgnoreCase) >= 0 ||
                !SecurityValidation.HasValidPercentEncoding(ApiBaseUrl))
            {
                throw new InvalidOperationException("ApiBaseUrl cannot contain credentials, a query, a fragment, encoded path separators, or backslashes.");
            }

            foreach (var segment in uri.AbsolutePath.Split('/'))
            {
                if (segment == "." || segment == "..")
                {
                    throw new InvalidOperationException("ApiBaseUrl cannot contain path traversal segments.");
                }
            }

            if (uri.Scheme == "http" && !AllowInsecureHttp)
            {
                throw new InvalidOperationException("ApiBaseUrl must use HTTPS unless AllowInsecureHttp is explicitly enabled.");
            }

            if (!SecurityValidation.IsSafeIdentifier(StoragePrefix, 1, 64))
            {
                throw new InvalidOperationException("StoragePrefix must be a safe identifier no longer than 64 bytes.");
            }

            if (MaxRetries < 0 || MaxRetries > 8)
            {
                throw new InvalidOperationException("MaxRetries must be between 0 and 8.");
            }

            if (RetryDelay < TimeSpan.Zero || RetryDelay > TimeSpan.FromMinutes(1))
            {
                throw new InvalidOperationException("RetryDelay must be between zero and one minute.");
            }

            if (AutoValidateInterval < TimeSpan.Zero || AutoValidateInterval > TimeSpan.FromDays(24))
            {
                throw new InvalidOperationException("AutoValidateInterval must be between zero and 24 days.");
            }

            if (NetworkRecheckInterval <= TimeSpan.Zero || NetworkRecheckInterval > TimeSpan.FromDays(24))
            {
                throw new InvalidOperationException("NetworkRecheckInterval must be positive and no longer than 24 days.");
            }

            if (OfflineLicenseRefreshInterval <= TimeSpan.Zero || OfflineLicenseRefreshInterval > TimeSpan.FromDays(24))
            {
                throw new InvalidOperationException("OfflineLicenseRefreshInterval must be positive and no longer than 24 days.");
            }

            if (HeartbeatInterval > TimeSpan.FromDays(24))
            {
                throw new InvalidOperationException("HeartbeatInterval cannot exceed 24 days.");
            }

            if (MaxOfflineDays < 0 || MaxOfflineDays > 36_600)
            {
                throw new InvalidOperationException("MaxOfflineDays must be between 0 and 36600.");
            }

            if (MaxClockSkew < TimeSpan.Zero || MaxClockSkew > TimeSpan.FromHours(1))
            {
                throw new InvalidOperationException("MaxClockSkew must be between zero and one hour.");
            }

            if (HttpTimeout <= TimeSpan.Zero || HttpTimeout > TimeSpan.FromMinutes(5))
            {
                throw new InvalidOperationException("HttpTimeout must be positive and no longer than five minutes.");
            }

            if (MaxRequestBodyBytes < 1024 || MaxRequestBodyBytes > 16 * 1024 * 1024)
            {
                throw new InvalidOperationException("MaxRequestBodyBytes must be between 1 KiB and 16 MiB.");
            }

            if (MaxResponseBodyBytes < 1024 || MaxResponseBodyBytes > 16 * 1024 * 1024)
            {
                throw new InvalidOperationException("MaxResponseBodyBytes must be between 1 KiB and 16 MiB.");
            }

            if (AppVersion != null && !SecurityValidation.IsSafeText(AppVersion, 1, 256))
            {
                throw new InvalidOperationException("AppVersion must be valid text no longer than 256 bytes.");
            }

            if (AppBuild != null && !SecurityValidation.IsSafeText(AppBuild, 1, 256))
            {
                throw new InvalidOperationException("AppBuild must be valid text no longer than 256 bytes.");
            }

            if (DeviceId != null && !SecurityValidation.IsSafeText(DeviceId, 8, 255))
            {
                throw new InvalidOperationException("DeviceId must be valid text between 8 and 255 bytes.");
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
        /// Compatibility alias for network-only fallback. Authoritative HTTP
        /// responses and local security failures never fall back to cached authorization.
        /// </summary>
        Always
    }
}
