#nullable enable
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
        [Tooltip("Restricted client SDK key scoped to licenses:validate. Never use an administrator/server key in a player build.")]
        [SerializeField] private string apiKey = "";

        [Tooltip("Your product slug from the LicenseSeat dashboard.")]
        [SerializeField] private string productId = "";

        [Tooltip("Base URL for the LicenseSeat API.")]
        [SerializeField] private string baseUrl = LicenseSeatClientOptions.DefaultApiBaseUrl;

        [Header("Validation Settings")]
        [Tooltip("Legacy serialized setting retained for asset compatibility. The memory-only cache has no license to validate after a restart.")]
        [HideInInspector]
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
        /// Gets or sets the product slug.
        /// </summary>
        [Obsolete("Use ProductSlug. This alias remains for serialized-settings compatibility.")]
        public string ProductId
        {
            get => productId;
            set => productId = value;
        }

        /// <summary>
        /// Gets or sets the product slug used to scope every license request.
        /// </summary>
        public string ProductSlug
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
        /// Gets or sets a legacy serialized value retained for asset compatibility.
        /// The built-in memory-only cache cannot validate a prior license after restart.
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
        public bool IsValid
        {
            get
            {
                try
                {
                    CreateCoreOptions().Validate();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Creates client options from these settings.
        /// The product slug is fixed at client construction so every request is consistently scoped.
        /// </summary>
        /// <returns>Configured client options.</returns>
        public LicenseSeatClientOptions ToClientOptions()
        {
            var options = CreateCoreOptions();
            options.Validate();

            // Use UnityWebRequest adapter for cross-platform compatibility
            options.HttpClientAdapter = new UnityWebRequestAdapter(options);

            return options;
        }

        /// <summary>
        /// Creates validation options for compatibility with earlier SDK versions.
        /// Product scope now comes from <see cref="LicenseSeatClientOptions.ProductSlug"/>.
        /// </summary>
        /// <returns>Default validation options.</returns>
        [Obsolete("Product scope is configured on LicenseSeatClientOptions. Construct ValidationOptions directly.")]
        public ValidationOptions CreateValidationOptions()
        {
            return new ValidationOptions();
        }

        private LicenseSeatClientOptions CreateCoreOptions()
        {
            return new LicenseSeatClientOptions
            {
                ApiKey = apiKey,
                ProductSlug = productId,
                ApiBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? LicenseSeatClientOptions.DefaultApiBaseUrl : baseUrl,
                AutoValidateInterval = autoValidateInterval > 0 ? TimeSpan.FromSeconds(autoValidateInterval) : TimeSpan.Zero,
                OfflineFallbackMode = offlineFallbackMode,
                MaxOfflineDays = maxOfflineDays,
                Debug = enableDebugLogging,
                DeviceId = GetUnityDeviceId(),
                AutoInitialize = false // Let LicenseSeatManager control initialization
            };
        }

        private static string? GetUnityDeviceId()
        {
            try
            {
                var unityIdentifier = SystemInfo.deviceUniqueIdentifier;
                if (string.IsNullOrWhiteSpace(unityIdentifier) ||
                    string.Equals(
                        unityIdentifier,
                        SystemInfo.unsupportedIdentifier,
                        StringComparison.Ordinal) ||
                    !SecurityValidation.IsSafeText(unityIdentifier, 8, 512))
                {
                    return null;
                }

                // Hash the platform identifier before sending it. This reduces
                // unnecessary exposure while retaining deterministic binding.
                return DeviceIdentifier.FromInput("unity:" + unityIdentifier);
            }
            catch (Exception)
            {
                // The core client has an in-process fallback for restricted platforms.
                return null;
            }
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
