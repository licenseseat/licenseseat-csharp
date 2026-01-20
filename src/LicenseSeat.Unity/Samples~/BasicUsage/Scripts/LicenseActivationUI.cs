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

            if (licenseSeatManager == null)
            {
                Debug.LogError("[LicenseSeat Sample] LicenseSeatManager not found in scene!");
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
            if (licenseSeatManager?.Client != null)
            {
                licenseSeatManager.Client.Events.Off(LicenseSeatEvents.ActivationSuccess, OnLicenseActivated);
                licenseSeatManager.Client.Events.Off(LicenseSeatEvents.ValidationSuccess, OnLicenseValidated);
                licenseSeatManager.Client.Events.Off(LicenseSeatEvents.ValidationFailed, OnValidationFailed);
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

            var currentLicense = licenseSeatManager.Client.CurrentLicense;
            if (currentLicense == null)
            {
                SetStatus("No license to validate", Color.yellow);
                return;
            }

            SetStatus("Validating...", Color.white);
            SetButtonsInteractable(false);

            StartCoroutine(licenseSeatManager.ValidateCoroutine(currentLicense.LicenseKey, OnValidationComplete));
        }

        private void OnActivationComplete(License? license, Exception? error)
        {
            SetButtonsInteractable(true);

            if (error != null)
            {
                SetStatus($"Activation failed: {error.Message}", Color.red);
                Debug.LogError($"[LicenseSeat Sample] Activation error: {error}");
                return;
            }

            if (license != null)
            {
                SetStatus($"Activated! License: {license.LicenseKey}", Color.green);
                UpdateUI();
            }
        }

        private void OnValidationComplete(ValidationResult? result, Exception? error)
        {
            SetButtonsInteractable(true);

            if (error != null)
            {
                SetStatus($"Validation failed: {error.Message}", Color.red);
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

        private void OnLicenseActivated(object data)
        {
            Debug.Log("[LicenseSeat Sample] License activated event received");
            UpdateUI();
        }

        private void OnLicenseValidated(object data)
        {
            Debug.Log("[LicenseSeat Sample] License validated event received");
        }

        private void OnValidationFailed(object data)
        {
            Debug.LogWarning("[LicenseSeat Sample] Validation failed event received");
            SetStatus("License validation failed", Color.red);
        }

        private void UpdateUI()
        {
            var isLicensed = licenseSeatManager?.Client?.CurrentLicense != null;

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
