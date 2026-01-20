#if UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using UnityEngine;
using LicenseSeat.Unity;

namespace LicenseSeat
{
    /// <summary>
    /// MonoBehaviour wrapper for the LicenseSeat client.
    /// Provides Unity-native lifecycle management and coroutine-based API.
    /// </summary>
    [AddComponentMenu("LicenseSeat/LicenseSeat Manager")]
    public class LicenseSeatManager : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Settings asset. If not set, will attempt to load from Resources.")]
        [SerializeField] private LicenseSeatSettings _settings;

        [Tooltip("Don't destroy this GameObject when loading new scenes.")]
        [SerializeField] private bool _dontDestroyOnLoad = true;

        [Tooltip("Automatically initialize on Awake.")]
        [SerializeField] private bool _autoInitialize = true;

        private LicenseSeatClient _client;
        private bool _initialized;
        private static LicenseSeatManager _instance;

        /// <summary>
        /// Gets the singleton instance of LicenseSeatManager.
        /// </summary>
        public static LicenseSeatManager Instance => _instance;

        /// <summary>
        /// Gets the underlying LicenseSeat client.
        /// </summary>
        public LicenseSeatClient Client => _client;

        /// <summary>
        /// Gets a value indicating whether the manager is initialized.
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Gets or sets the settings asset.
        /// </summary>
        public LicenseSeatSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        /// <summary>
        /// Unity Awake callback.
        /// </summary>
        protected virtual void Awake()
        {
            // Singleton pattern
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[LicenseSeat SDK] Multiple LicenseSeatManager instances detected. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            _instance = this;

            if (_dontDestroyOnLoad)
            {
                DontDestroyOnLoad(gameObject);
            }

            if (_autoInitialize)
            {
                Initialize();
            }
        }

        /// <summary>
        /// Unity OnDestroy callback.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }

            _client?.Dispose();
            _client = null;
            _initialized = false;
        }

        /// <summary>
        /// Initializes the LicenseSeat client.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                Debug.LogWarning("[LicenseSeat SDK] Already initialized.");
                return;
            }

            // Load settings if not assigned
            if (_settings == null)
            {
                _settings = LicenseSeatSettings.Load();
                if (_settings == null)
                {
                    Debug.LogError("[LicenseSeat SDK] No settings found. Create settings via Create > LicenseSeat > Settings");
                    return;
                }
            }

            if (!_settings.IsValid)
            {
                Debug.LogError("[LicenseSeat SDK] Settings are invalid. Please configure your API key.");
                return;
            }

            try
            {
                var options = _settings.ToClientOptions();
                _client = new LicenseSeatClient(options);
                _client.Initialize();
                _initialized = true;

                Debug.Log("[LicenseSeat SDK] Initialized successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LicenseSeat SDK] Failed to initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// Activates a license using a coroutine.
        /// </summary>
        /// <param name="licenseKey">The license key to activate.</param>
        /// <param name="callback">Callback with result (license, error).</param>
        /// <returns>Coroutine enumerator.</returns>
        public IEnumerator ActivateCoroutine(string licenseKey, Action<License, Exception> callback)
        {
            return ActivateCoroutine(licenseKey, null, callback);
        }

        /// <summary>
        /// Activates a license using a coroutine.
        /// </summary>
        /// <param name="licenseKey">The license key to activate.</param>
        /// <param name="options">Activation options.</param>
        /// <param name="callback">Callback with result (license, error).</param>
        /// <returns>Coroutine enumerator.</returns>
        public IEnumerator ActivateCoroutine(string licenseKey, ActivationOptions options, Action<License, Exception> callback)
        {
            EnsureInitialized();

            var task = _client.ActivateAsync(licenseKey, options);

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                callback?.Invoke(null, task.Exception?.InnerException ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                callback?.Invoke(null, new OperationCanceledException());
            }
            else
            {
                callback?.Invoke(task.Result, null);
            }
        }

        /// <summary>
        /// Validates a license using a coroutine.
        /// </summary>
        /// <param name="licenseKey">The license key to validate.</param>
        /// <param name="callback">Callback with result (validationResult, error).</param>
        /// <returns>Coroutine enumerator.</returns>
        public IEnumerator ValidateCoroutine(string licenseKey, Action<ValidationResult, Exception> callback)
        {
            return ValidateCoroutine(licenseKey, null, callback);
        }

        /// <summary>
        /// Validates a license using a coroutine.
        /// </summary>
        /// <param name="licenseKey">The license key to validate.</param>
        /// <param name="options">Validation options.</param>
        /// <param name="callback">Callback with result (validationResult, error).</param>
        /// <returns>Coroutine enumerator.</returns>
        public IEnumerator ValidateCoroutine(string licenseKey, ValidationOptions options, Action<ValidationResult, Exception> callback)
        {
            EnsureInitialized();

            var task = _client.ValidateAsync(licenseKey, options);

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                callback?.Invoke(null, task.Exception?.InnerException ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                callback?.Invoke(null, new OperationCanceledException());
            }
            else
            {
                callback?.Invoke(task.Result, null);
            }
        }

        /// <summary>
        /// Deactivates the current license using a coroutine.
        /// </summary>
        /// <param name="callback">Callback with error (null if successful).</param>
        /// <returns>Coroutine enumerator.</returns>
        public IEnumerator DeactivateCoroutine(Action<Exception> callback)
        {
            EnsureInitialized();

            var task = _client.DeactivateAsync();

            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                callback?.Invoke(task.Exception?.InnerException ?? task.Exception);
            }
            else if (task.IsCanceled)
            {
                callback?.Invoke(new OperationCanceledException());
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        /// <summary>
        /// Gets the current license status.
        /// </summary>
        /// <returns>The license status.</returns>
        public LicenseStatus GetStatus()
        {
            EnsureInitialized();
            return _client.GetStatus();
        }

        /// <summary>
        /// Gets the current cached license.
        /// </summary>
        /// <returns>The license, or null if none.</returns>
        public License GetCurrentLicense()
        {
            EnsureInitialized();
            return _client.GetCurrentLicense();
        }

        /// <summary>
        /// Checks if a specific entitlement is active.
        /// </summary>
        /// <param name="entitlementKey">The entitlement key.</param>
        /// <returns>True if active.</returns>
        public bool HasEntitlement(string entitlementKey)
        {
            EnsureInitialized();
            return _client.HasEntitlement(entitlementKey);
        }

        /// <summary>
        /// Checks an entitlement with detailed status.
        /// </summary>
        /// <param name="entitlementKey">The entitlement key.</param>
        /// <returns>The entitlement status.</returns>
        public EntitlementStatus CheckEntitlement(string entitlementKey)
        {
            EnsureInitialized();
            return _client.CheckEntitlement(entitlementKey);
        }

        /// <summary>
        /// Resets the SDK state.
        /// </summary>
        public void Reset()
        {
            EnsureInitialized();
            _client.Reset();
        }

        /// <summary>
        /// Purges cached license data.
        /// </summary>
        public void PurgeCachedLicense()
        {
            EnsureInitialized();
            _client.PurgeCachedLicense();
        }

        private void EnsureInitialized()
        {
            if (!_initialized || _client == null)
            {
                throw new InvalidOperationException("LicenseSeatManager is not initialized. Call Initialize() first or enable Auto Initialize.");
            }
        }
    }
}
#endif
