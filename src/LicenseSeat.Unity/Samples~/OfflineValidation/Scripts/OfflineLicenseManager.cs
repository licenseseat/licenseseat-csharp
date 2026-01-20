using System;
using UnityEngine;
using UnityEngine.UI;

namespace LicenseSeat.Unity.Samples
{
    /// <summary>
    /// Sample demonstrating offline license validation capabilities.
    /// Shows how to handle scenarios where the user may not have internet connectivity.
    /// </summary>
    public class OfflineLicenseManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LicenseSeatManager? licenseSeatManager;

        [Header("UI Elements")]
        [SerializeField] private Text? connectionStatusText;
        [SerializeField] private Text? licenseStatusText;
        [SerializeField] private Text? lastValidatedText;
        [SerializeField] private Button? forceOfflineButton;
        [SerializeField] private Button? validateButton;
        [SerializeField] private Toggle? simulateOfflineToggle;

        private bool _simulateOffline;

        private void Start()
        {
            if (licenseSeatManager == null)
            {
                licenseSeatManager = FindObjectOfType<LicenseSeatManager>();
            }

            if (licenseSeatManager == null)
            {
                Debug.LogError("[OfflineValidation] LicenseSeatManager not found!");
                return;
            }

            // Set up UI
            if (forceOfflineButton != null)
            {
                forceOfflineButton.onClick.AddListener(OnForceOfflineValidation);
            }

            if (validateButton != null)
            {
                validateButton.onClick.AddListener(OnValidateClicked);
            }

            if (simulateOfflineToggle != null)
            {
                simulateOfflineToggle.onValueChanged.AddListener(OnSimulateOfflineChanged);
            }

            // Subscribe to events
            licenseSeatManager.Client.Events.On(LicenseSeatEvents.LicenseValidated, OnLicenseValidated);
            licenseSeatManager.Client.Events.On(LicenseSeatEvents.ValidationFailed, OnValidationFailed);
            licenseSeatManager.Client.Events.On(LicenseSeatEvents.OfflineFallbackUsed, OnOfflineFallbackUsed);

            // Initial status update
            UpdateStatus();

            // Check connectivity periodically
            InvokeRepeating(nameof(UpdateConnectionStatus), 0f, 5f);
        }

        private void OnDestroy()
        {
            if (licenseSeatManager?.Client != null)
            {
                licenseSeatManager.Client.Events.Off(LicenseSeatEvents.LicenseValidated, OnLicenseValidated);
                licenseSeatManager.Client.Events.Off(LicenseSeatEvents.ValidationFailed, OnValidationFailed);
                licenseSeatManager.Client.Events.Off(LicenseSeatEvents.OfflineFallbackUsed, OnOfflineFallbackUsed);
            }
        }

        private void OnValidateClicked()
        {
            if (licenseSeatManager?.Client.CurrentLicense == null)
            {
                SetLicenseStatus("No license activated", Color.yellow);
                return;
            }

            SetLicenseStatus("Validating...", Color.white);

            StartCoroutine(licenseSeatManager.ValidateCoroutine(
                licenseSeatManager.Client.CurrentLicense.LicenseKey,
                OnValidationComplete));
        }

        private void OnForceOfflineValidation()
        {
            if (licenseSeatManager == null) return;

            // Attempt offline validation using cached license data
            var isValid = ValidateOffline();

            if (isValid)
            {
                SetLicenseStatus("Offline validation: VALID", Color.green);
            }
            else
            {
                SetLicenseStatus("Offline validation: INVALID", Color.red);
            }
        }

        private bool ValidateOffline()
        {
            if (licenseSeatManager?.Client.CurrentLicense == null)
            {
                return false;
            }

            var license = licenseSeatManager.Client.CurrentLicense;

            // Check if license has expired
            if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTime.UtcNow)
            {
                Debug.Log("[OfflineValidation] License has expired");
                return false;
            }

            // Check license status
            if (license.Status != LicenseStatus.Active)
            {
                Debug.Log($"[OfflineValidation] License status is {license.Status}");
                return false;
            }

            // Check if we're within the allowed offline period
            // This would typically check against the last online validation timestamp
            // For this sample, we just check if the license data exists

            Debug.Log("[OfflineValidation] Offline validation passed");
            return true;
        }

        private void OnValidationComplete(ValidationResult? result, Exception? error)
        {
            if (error != null)
            {
                // Check if it's a network error (offline)
                if (error.Message.Contains("network") || error.Message.Contains("connection"))
                {
                    SetLicenseStatus("Network error - using offline validation", Color.yellow);
                    OnForceOfflineValidation();
                }
                else
                {
                    SetLicenseStatus($"Error: {error.Message}", Color.red);
                }
                return;
            }

            if (result?.Valid == true)
            {
                SetLicenseStatus("License VALID (online)", Color.green);
                UpdateLastValidated();
            }
            else
            {
                SetLicenseStatus("License INVALID", Color.red);
            }
        }

        private void OnLicenseValidated(object data)
        {
            Debug.Log("[OfflineValidation] License validated online");
            UpdateLastValidated();
        }

        private void OnValidationFailed(object data)
        {
            Debug.LogWarning("[OfflineValidation] Online validation failed");
        }

        private void OnOfflineFallbackUsed(object data)
        {
            Debug.Log("[OfflineValidation] Offline fallback was used");
            SetLicenseStatus("Using cached license (offline)", Color.yellow);
        }

        private void OnSimulateOfflineChanged(bool isOffline)
        {
            _simulateOffline = isOffline;
            UpdateConnectionStatus();

            if (isOffline)
            {
                Debug.Log("[OfflineValidation] Simulating offline mode");
            }
            else
            {
                Debug.Log("[OfflineValidation] Online mode restored");
            }
        }

        private void UpdateConnectionStatus()
        {
            if (connectionStatusText == null) return;

            bool isOnline;

            if (_simulateOffline)
            {
                isOnline = false;
            }
            else
            {
                isOnline = Application.internetReachability != NetworkReachability.NotReachable;
            }

            connectionStatusText.text = isOnline ? "ONLINE" : "OFFLINE";
            connectionStatusText.color = isOnline ? Color.green : Color.red;
        }

        private void UpdateStatus()
        {
            if (licenseSeatManager?.Client.CurrentLicense == null)
            {
                SetLicenseStatus("No license", Color.gray);
            }
            else
            {
                var license = licenseSeatManager.Client.CurrentLicense;
                SetLicenseStatus($"License: {license.Status}", license.Status == LicenseStatus.Active ? Color.green : Color.yellow);
            }
        }

        private void UpdateLastValidated()
        {
            if (lastValidatedText == null) return;
            lastValidatedText.text = $"Last validated: {DateTime.Now:HH:mm:ss}";
        }

        private void SetLicenseStatus(string message, Color color)
        {
            if (licenseStatusText != null)
            {
                licenseStatusText.text = message;
                licenseStatusText.color = color;
            }

            Debug.Log($"[OfflineValidation] {message}");
        }
    }
}
