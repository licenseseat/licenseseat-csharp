#nullable enable
using System;
using UnityEngine;
using UnityEngine.UI;

namespace LicenseSeat.Unity.Samples
{
    /// <summary>
    /// Demonstrates signed offline fallback without making local authorization
    /// decisions from reachability or cached license fields.
    /// </summary>
    public sealed class OfflineLicenseManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LicenseSeatManager? licenseSeatManager;

        [Header("UI Elements")]
        [SerializeField] private Text? connectionStatusText;
        [SerializeField] private Text? licenseStatusText;
        [SerializeField] private Text? lastValidatedText;
        [SerializeField] private Button? validateButton;

        private void Start()
        {
            if (licenseSeatManager == null)
            {
                licenseSeatManager = FindObjectOfType<LicenseSeatManager>();
            }

            if (licenseSeatManager == null ||
                !licenseSeatManager.IsInitialized ||
                licenseSeatManager.Client == null)
            {
                Debug.LogError("[LicenseSeat Sample] LicenseSeatManager is missing or not initialized.");
                return;
            }

            validateButton?.onClick.AddListener(OnValidateClicked);
            licenseSeatManager.Client.Events.On(
                LicenseSeatEvents.ValidationOfflineSuccess,
                OnOfflineFallbackUsed);
            licenseSeatManager.Client.Events.On(
                LicenseSeatEvents.ValidationOfflineFailed,
                OnOfflineFallbackRejected);

            InvokeRepeating(nameof(UpdateConnectionHint), 0f, 5f);
            SetLicenseStatus("Ready to validate", Color.gray);
        }

        private void OnDestroy()
        {
            CancelInvoke(nameof(UpdateConnectionHint));
            validateButton?.onClick.RemoveListener(OnValidateClicked);

            var client = licenseSeatManager?.Client;
            if (client != null)
            {
                client.Events.Off(
                    LicenseSeatEvents.ValidationOfflineSuccess,
                    OnOfflineFallbackUsed);
                client.Events.Off(
                    LicenseSeatEvents.ValidationOfflineFailed,
                    OnOfflineFallbackRejected);
            }
        }

        private void OnValidateClicked()
        {
            var license = licenseSeatManager?.GetCurrentLicense();
            if (licenseSeatManager == null || license == null)
            {
                SetLicenseStatus("No license activated", Color.yellow);
                return;
            }

            validateButton?.onClick.RemoveListener(OnValidateClicked);
            SetLicenseStatus("Validating...", Color.white);
            StartCoroutine(licenseSeatManager.ValidateCoroutine(license.Key, OnValidationComplete));
        }

        private void OnValidationComplete(ValidationResult? result, Exception? error)
        {
            validateButton?.onClick.AddListener(OnValidateClicked);

            if (error != null || result == null)
            {
                // A transport failure with no valid signed fallback remains an error.
                SetLicenseStatus("Validation unavailable; access remains disabled", Color.red);
                return;
            }

            if (!result.Valid)
            {
                SetLicenseStatus(
                    result.Offline
                        ? "Signed offline validation rejected the license"
                        : "The service rejected the license",
                    Color.red);
                return;
            }

            SetLicenseStatus(
                result.Offline ? "VALID (signed offline fallback)" : "VALID (online)",
                result.Offline ? Color.yellow : Color.green);

            if (lastValidatedText != null)
            {
                lastValidatedText.text = $"Last validated: {DateTimeOffset.Now:HH:mm:ss}";
            }
        }

        private void OnOfflineFallbackUsed(object? data)
        {
            Debug.Log("[LicenseSeat Sample] A signed offline token was accepted.");
        }

        private void OnOfflineFallbackRejected(object? data)
        {
            Debug.LogWarning("[LicenseSeat Sample] Signed offline validation failed closed.");
        }

        private void UpdateConnectionHint()
        {
            if (connectionStatusText == null)
            {
                return;
            }

            var reachable = Application.internetReachability != NetworkReachability.NotReachable;
            connectionStatusText.text = reachable
                ? "NETWORK REACHABILITY REPORTED"
                : "NETWORK NOT REACHABLE";
            connectionStatusText.color = reachable ? Color.green : Color.yellow;
        }

        private void SetLicenseStatus(string message, Color color)
        {
            if (licenseStatusText != null)
            {
                licenseStatusText.text = message;
                licenseStatusText.color = color;
            }
        }
    }
}
