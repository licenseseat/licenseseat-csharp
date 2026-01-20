#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace LicenseSeat
{
    /// <summary>
    /// ScriptableObject for storing LicenseSeat SDK configuration.
    /// Create via: Right-click in Project > Create > LicenseSeat > Settings
    /// </summary>
    [CreateAssetMenu(fileName = "LicenseSeatSettings", menuName = "LicenseSeat/Settings", order = 1)]
    public class LicenseSeatSettings : ScriptableObject
    {
        [Header("API Configuration")]
        [Tooltip("Your LicenseSeat API key. Required for authenticated requests.")]
        [SerializeField] private string _apiKey = "";

        [Tooltip("Base URL for the LicenseSeat API.")]
        [SerializeField] private string _apiBaseUrl = LicenseSeatClientOptions.DefaultApiBaseUrl;

        [Header("Validation")]
        [Tooltip("Interval between automatic license validations (in minutes). Set to 0 to disable.")]
        [Min(0)]
        [SerializeField] private int _autoValidateIntervalMinutes = 60;

        [Tooltip("HTTP request timeout (in seconds).")]
        [Min(1)]
        [SerializeField] private int _httpTimeoutSeconds = 30;

        [Header("Offline Support")]
        [Tooltip("When to use offline validation as fallback.")]
        [SerializeField] private OfflineFallbackMode _offlineFallbackMode = OfflineFallbackMode.Disabled;

        [Tooltip("Maximum days a license can be used offline (0 = unlimited).")]
        [Min(0)]
        [SerializeField] private int _maxOfflineDays = 0;

        [Header("Debug")]
        [Tooltip("Enable debug logging to console.")]
        [SerializeField] private bool _debugLogging = false;

        [Tooltip("Custom storage prefix for cached data.")]
        [SerializeField] private string _storagePrefix = "licenseseat_";

        /// <summary>
        /// Gets the API key.
        /// </summary>
        public string ApiKey => _apiKey;

        /// <summary>
        /// Gets the API base URL.
        /// </summary>
        public string ApiBaseUrl => _apiBaseUrl;

        /// <summary>
        /// Gets the auto-validation interval.
        /// </summary>
        public TimeSpan AutoValidateInterval => TimeSpan.FromMinutes(_autoValidateIntervalMinutes);

        /// <summary>
        /// Gets the HTTP timeout.
        /// </summary>
        public TimeSpan HttpTimeout => TimeSpan.FromSeconds(_httpTimeoutSeconds);

        /// <summary>
        /// Gets the offline fallback mode.
        /// </summary>
        public OfflineFallbackMode OfflineFallbackMode => _offlineFallbackMode;

        /// <summary>
        /// Gets the maximum offline days.
        /// </summary>
        public int MaxOfflineDays => _maxOfflineDays;

        /// <summary>
        /// Gets whether debug logging is enabled.
        /// </summary>
        public bool DebugLogging => _debugLogging;

        /// <summary>
        /// Gets the storage prefix.
        /// </summary>
        public string StoragePrefix => _storagePrefix;

        /// <summary>
        /// Creates client options from these settings.
        /// </summary>
        /// <returns>Configured client options.</returns>
        public LicenseSeatClientOptions ToClientOptions()
        {
            var options = new LicenseSeatClientOptions
            {
                ApiKey = _apiKey,
                ApiBaseUrl = string.IsNullOrWhiteSpace(_apiBaseUrl) ? LicenseSeatClientOptions.DefaultApiBaseUrl : _apiBaseUrl,
                AutoValidateInterval = TimeSpan.FromMinutes(_autoValidateIntervalMinutes),
                HttpTimeout = TimeSpan.FromSeconds(_httpTimeoutSeconds),
                OfflineFallbackMode = _offlineFallbackMode,
                MaxOfflineDays = _maxOfflineDays,
                Debug = _debugLogging,
                StoragePrefix = _storagePrefix,
                AutoInitialize = false // Let LicenseSeatManager control initialization
            };

            // Use UnityWebRequest adapter for cross-platform compatibility
            options.HttpClientAdapter = new UnityWebRequestAdapter(options);

            return options;
        }

        /// <summary>
        /// Validates the settings configuration.
        /// </summary>
        /// <returns>True if settings are valid.</returns>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_apiBaseUrl);
        }

        private void OnValidate()
        {
            // Ensure minimum values
            if (_autoValidateIntervalMinutes < 0) _autoValidateIntervalMinutes = 0;
            if (_httpTimeoutSeconds < 1) _httpTimeoutSeconds = 1;
            if (_maxOfflineDays < 0) _maxOfflineDays = 0;

            // Ensure API base URL has trailing slash removed
            if (!string.IsNullOrEmpty(_apiBaseUrl) && _apiBaseUrl.EndsWith("/"))
            {
                _apiBaseUrl = _apiBaseUrl.TrimEnd('/');
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Loads or creates the default settings asset in Resources folder.
        /// Editor only.
        /// </summary>
        public static LicenseSeatSettings GetOrCreateSettings()
        {
            const string resourcePath = "LicenseSeatSettings";
            const string assetPath = "Assets/Resources/LicenseSeatSettings.asset";

            // Try to load existing
            var settings = Resources.Load<LicenseSeatSettings>(resourcePath);
            if (settings != null)
            {
                return settings;
            }

            // Create new
            settings = CreateInstance<LicenseSeatSettings>();

            // Ensure Resources folder exists
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            }

            UnityEditor.AssetDatabase.CreateAsset(settings, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log($"[LicenseSeat SDK] Created settings asset at {assetPath}");
            return settings;
        }
#endif

        /// <summary>
        /// Loads settings from Resources.
        /// </summary>
        /// <returns>The settings, or null if not found.</returns>
        public static LicenseSeatSettings Load()
        {
            return Resources.Load<LicenseSeatSettings>("LicenseSeatSettings");
        }
    }
}
#endif
