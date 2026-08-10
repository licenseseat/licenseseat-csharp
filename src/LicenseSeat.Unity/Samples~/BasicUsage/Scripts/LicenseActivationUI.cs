#nullable enable
using System;
using UnityEngine;
using UnityEngine.UI;

namespace LicenseSeat.Unity.Samples
{
    /// <summary>
    /// Sample UI controller demonstrating basic license activation flow.
    /// Attach this to a GameObject with UI elements for license key input.
    /// </summary>
    public class LicenseActivationUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the LicenseSeatManager in the scene")]
        [SerializeField] private LicenseSeatManager? licenseSeatManager;

        [Header("UI Elements")]
        [SerializeField] private InputField? licenseKeyInput;
        [SerializeField] private Button? activateButton;
        [SerializeField] private Button? validateButton;
        [SerializeField] private Text? statusText;
        [SerializeField] private GameObject? activationPanel;
        [SerializeField] private GameObject? licensedPanel;

        private void Start()
        {
            // Find manager if not assigned
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

            // Set up button listeners
            if (activateButton != null)
            {
                activateButton.onClick.AddListener(OnActivateClicked);
            }

            if (validateButton != null)
            {
                validateButton.onClick.AddListener(OnValidateClicked);
            }

            // Subscribe to license events
            licenseSeatManager.Client.Events.On(LicenseSeatEvents.ActivationSuccess, OnLicenseActivated);
            licenseSeatManager.Client.Events.On(LicenseSeatEvents.ValidationSuccess, OnLicenseValidated);
            licenseSeatManager.Client.Events.On(LicenseSeatEvents.ValidationFailed, OnValidationFailed);

            // Check if already licensed
            UpdateUI();
        }

        private void OnDestroy()
        {
            var client = licenseSeatManager?.Client;
            if (client != null)
            {
                client.Events.Off(LicenseSeatEvents.ActivationSuccess, OnLicenseActivated);
                client.Events.Off(LicenseSeatEvents.ValidationSuccess, OnLicenseValidated);
                client.Events.Off(LicenseSeatEvents.ValidationFailed, OnValidationFailed);
            }
        }

        private void OnActivateClicked()
        {
            if (licenseSeatManager == null || licenseKeyInput == null) return;

            var licenseKey = licenseKeyInput.text.Trim();

            if (string.IsNullOrEmpty(licenseKey))
            {
                SetStatus("Please enter a license key", Color.yellow);
                return;
            }

            SetStatus("Activating...", Color.white);
            SetButtonsInteractable(false);

            // Use coroutine for Unity-friendly async
            StartCoroutine(licenseSeatManager.ActivateCoroutine(licenseKey, OnActivationComplete));
        }

        private void OnValidateClicked()
        {
            if (licenseSeatManager == null) return;

            var currentLicense = licenseSeatManager.GetCurrentLicense();
            if (currentLicense == null)
            {
                SetStatus("No license to validate", Color.yellow);
                return;
            }

            SetStatus("Validating...", Color.white);
            SetButtonsInteractable(false);

            StartCoroutine(licenseSeatManager.ValidateCoroutine(currentLicense.Key, OnValidationComplete));
        }

        private void OnActivationComplete(License? license, Exception? error)
        {
            SetButtonsInteractable(true);

            if (error != null)
            {
                SetStatus("Activation failed", Color.red);
                Debug.LogError("[LicenseSeat Sample] Activation failed.");
                return;
            }

            if (license != null)
            {
                SetStatus("License activated", Color.green);
                UpdateUI();
            }
        }

        private void OnValidationComplete(ValidationResult? result, Exception? error)
        {
            SetButtonsInteractable(true);

            if (error != null)
            {
                SetStatus("Validation could not be completed", Color.red);
                return;
            }

            if (result != null && result.Valid)
            {
                SetStatus("License is valid!", Color.green);
            }
            else
            {
                SetStatus("License is invalid", Color.red);
            }
        }

        private void OnLicenseActivated(object? data)
        {
            Debug.Log("[LicenseSeat Sample] License activated event received");
            UpdateUI();
        }

        private void OnLicenseValidated(object? data)
        {
            Debug.Log("[LicenseSeat Sample] License validated event received");
        }

        private void OnValidationFailed(object? data)
        {
            Debug.LogWarning("[LicenseSeat Sample] Validation failed event received");
            SetStatus("License validation failed", Color.red);
        }

        private void UpdateUI()
        {
            var isLicensed = licenseSeatManager?.GetCurrentLicense() != null;

            if (activationPanel != null)
            {
                activationPanel.SetActive(!isLicensed);
            }

            if (licensedPanel != null)
            {
                licensedPanel.SetActive(isLicensed);
            }
        }

        private void SetStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }

            Debug.Log($"[LicenseSeat Sample] Status: {message}");
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (activateButton != null)
            {
                activateButton.interactable = interactable;
            }

            if (validateButton != null)
            {
                validateButton.interactable = interactable;
            }
        }
    }
}
