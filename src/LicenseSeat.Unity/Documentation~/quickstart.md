# Quick Start Guide

Get up and running with the LicenseSeat Unity SDK in minutes.

## Prerequisites

- LicenseSeat account with API credentials
- Unity 2021.3+ with the SDK installed (see [Installation](installation.md))

## Step 1: Create Settings Asset

1. In the Project window, right-click
2. Select **Create > LicenseSeat > Settings**
3. Name it `LicenseSeatSettings` (or your preference)
4. Select the created asset

## Step 2: Configure Settings

In the Inspector, configure:

| Setting | Description |
|---------|-------------|
| **API Key** | Your LicenseSeat API key |
| **Product ID** | Your product identifier |
| **Base URL** | Leave default unless self-hosting |
| **Validate On Start** | Auto-validate when game starts |
| **Enable Debug Logging** | Show SDK logs in console |

## Step 3: Add Manager to Scene

1. Create an empty GameObject (name it "LicenseSeatManager")
2. Add Component: **LicenseSeat > LicenseSeat Manager**
3. Drag your Settings asset to the "Settings" field

## Step 4: Basic License Activation

Create a script to handle license activation:

```csharp
using LicenseSeat;
using LicenseSeat.Unity;
using UnityEngine;

public class MyLicenseController : MonoBehaviour
{
    private LicenseSeatManager _manager;

    void Start()
    {
        _manager = FindObjectOfType<LicenseSeatManager>();

        // Subscribe to events
        _manager.Client.Events.On(LicenseSeatEvents.LicenseActivated, OnActivated);
        _manager.Client.Events.On(LicenseSeatEvents.ValidationFailed, OnFailed);
    }

    public void ActivateLicense(string licenseKey)
    {
        StartCoroutine(_manager.ActivateCoroutine(licenseKey, (license, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"Activation failed: {error.Message}");
                return;
            }

            Debug.Log($"License activated: {license.LicenseKey}");
        }));
    }

    private void OnActivated(object data) => Debug.Log("License activated!");
    private void OnFailed(object data) => Debug.LogWarning("Validation failed!");
}
```

## Step 5: Validate Licenses

```csharp
public void ValidateLicense()
{
    var license = _manager.Client.CurrentLicense;
    if (license == null)
    {
        Debug.Log("No license to validate");
        return;
    }

    StartCoroutine(_manager.ValidateCoroutine(license.LicenseKey, (result, error) =>
    {
        if (error != null)
        {
            Debug.LogError($"Validation error: {error.Message}");
            return;
        }

        if (result.Valid)
        {
            Debug.Log("License is valid!");
        }
        else
        {
            Debug.LogWarning("License is invalid");
        }
    }));
}
```

## Step 6: Check Entitlements

```csharp
// Simple check
if (_manager.Client.HasEntitlement("premium-features"))
{
    EnablePremiumFeatures();
}

// Detailed check
var status = _manager.Client.CheckEntitlement("max-saves");
if (status.Active)
{
    Debug.Log($"Save limit: {status.Limit}");
}
```

## Using Async/Await (Alternative)

If you prefer async/await over coroutines:

```csharp
using System.Threading.Tasks;

public async void ActivateAsync(string licenseKey)
{
    try
    {
        var license = await _manager.Client.ActivateAsync(licenseKey);
        Debug.Log($"Activated: {license.LicenseKey}");
    }
    catch (LicenseSeatException ex)
    {
        Debug.LogError($"Error: {ex.Message}");
    }
}
```

**Warning**: Be careful with async/await in Unity - tasks can outlive GameObjects. Use cancellation tokens or the coroutine API for safety.

## Testing in Editor

1. Open **Window > LicenseSeat > Settings**
2. Enter a test license key
3. Click **Test Activation**
4. Check the console for results

## Next Steps

- [Platform Notes](platform-notes.md) - Platform-specific configuration
- [Troubleshooting](troubleshooting.md) - Common issues and solutions
- Import the **BasicUsage** sample from Package Manager for a complete example

## Sample Code Structure

```
YourGame/
├── Scripts/
│   ├── LicenseManager.cs      # Your license handling code
│   └── MainMenu.cs            # UI that uses licensing
├── Resources/
│   └── LicenseSeatSettings.asset
└── Scenes/
    └── MainMenu.unity         # Contains LicenseSeatManager
```
