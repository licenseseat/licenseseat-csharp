#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace LicenseSeat.Unity
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
        [SerializeField] private string apiKey = "";

        [Tooltip("Your product identifier from the LicenseSeat dashboard.")]
        [SerializeField] private string productId = "";

        [Tooltip("Base URL for the LicenseSeat API.")]
        [SerializeField] private string baseUrl = LicenseSeatClientOptions.DefaultApiBaseUrl;

        [Header("Validation Settings")]
        [Tooltip("Automatically validate license when the game starts.")]
        [SerializeField] private bool validateOnStart = true;

        [Tooltip("Interval between automatic license validations (in seconds). Set to 0 to disable.")]
        [Min(0)]
        [SerializeField] private float autoValidateInterval = 0;

        [Header("Offline Settings")]
        [Tooltip("When to use offline validation as fallback.")]
        [SerializeField] private OfflineFallbackMode offlineFallbackMode = OfflineFallbackMode.Disabled;

        [Tooltip("Maximum days a license can be used offline.")]
        [Min(0)]
        [SerializeField] private int maxOfflineDays = 7;

        [Header("Debug Settings")]
        [Tooltip("Enable debug logging to console.")]
        [SerializeField] private bool enableDebugLogging = false;

        /// <summary>
        /// Gets or sets the API key.
        /// </summary>
        public string ApiKey
        {
            get => apiKey;
            set => apiKey = value;
        }

        /// <summary>
        /// Gets or sets the product ID.
        /// </summary>
        public string ProductId
        {
            get => productId;
            set => productId = value;
        }

        /// <summary>
        /// Gets or sets the API base URL.
        /// </summary>
        public string BaseUrl
        {
            get => baseUrl;
            set => baseUrl = value;
        }

        /// <summary>
        /// Gets or sets whether to validate on start.
        /// </summary>
        public bool ValidateOnStart
        {
            get => validateOnStart;
            set => validateOnStart = value;
        }

        /// <summary>
        /// Gets or sets the auto-validation interval in seconds.
        /// </summary>
        public float AutoValidateInterval
        {
            get => autoValidateInterval;
            set => autoValidateInterval = value;
        }

        /// <summary>
        /// Gets or sets the offline fallback mode.
        /// </summary>
        public OfflineFallbackMode OfflineFallbackMode
        {
            get => offlineFallbackMode;
            set => offlineFallbackMode = value;
        }

        /// <summary>
        /// Gets or sets the maximum offline days.
        /// </summary>
        public int MaxOfflineDays
        {
            get => maxOfflineDays;
            set => maxOfflineDays = value;
        }

        /// <summary>
        /// Gets or sets whether debug logging is enabled.
        /// </summary>
        public bool EnableDebugLogging
        {
            get => enableDebugLogging;
            set => enableDebugLogging = value;
        }

        /// <summary>
        /// Gets whether the settings are valid (has required configuration).
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(productId);

        /// <summary>
        /// Creates client options from these settings.
        /// Note: ProductId is not part of client options. Use <see cref="CreateValidationOptions"/>
        /// to get ValidationOptions with ProductSlug set for validation calls.
        /// </summary>
        /// <returns>Configured client options.</returns>
        public LicenseSeatClientOptions ToClientOptions()
        {
            var options = new LicenseSeatClientOptions
            {
                ApiKey = apiKey,
                ApiBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? LicenseSeatClientOptions.DefaultApiBaseUrl : baseUrl,
                AutoValidateInterval = autoValidateInterval > 0 ? TimeSpan.FromSeconds(autoValidateInterval) : TimeSpan.Zero,
                OfflineFallbackMode = offlineFallbackMode,
                MaxOfflineDays = maxOfflineDays,
                Debug = enableDebugLogging,
                AutoInitialize = false // Let LicenseSeatManager control initialization
            };

            // Use UnityWebRequest adapter for cross-platform compatibility
            options.HttpClientAdapter = new UnityWebRequestAdapter(options);

            return options;
        }

        /// <summary>
        /// Creates validation options with the configured product slug.
        /// Use this when calling ValidateAsync to include the product identifier.
        /// </summary>
        /// <returns>ValidationOptions with ProductSlug set from ProductId.</returns>
        public ValidationOptions CreateValidationOptions()
        {
            return new ValidationOptions
            {
                ProductSlug = productId
            };
        }

        private void OnValidate()
        {
            // Ensure minimum values
            if (autoValidateInterval < 0) autoValidateInterval = 0;
            if (maxOfflineDays < 0) maxOfflineDays = 0;

            // Ensure API base URL has trailing slash removed
            if (!string.IsNullOrEmpty(baseUrl) && baseUrl.EndsWith("/"))
            {
                baseUrl = baseUrl.TrimEnd('/');
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
