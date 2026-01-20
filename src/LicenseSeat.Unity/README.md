# LicenseSeat Unity SDK

Pure C# licensing SDK for Unity game developers. Provides seamless license activation, validation, and entitlement checking across **all Unity platforms** including WebGL, iOS, and Android.

## Features

- **Pure C# Implementation** - No native DLLs or platform-specific binaries
- **Works on ALL Platforms** - Windows, macOS, Linux, Android, iOS, WebGL
- **IL2CPP Compatible** - Full AOT compilation support for mobile and WebGL
- **Unity-Native** - ScriptableObject configuration, MonoBehaviour integration
- **Offline Support** - Validate licenses even without internet connectivity
- **Event-Driven** - Subscribe to license events for reactive UI updates

## Installation

### Via Git URL (Recommended)

Add to your `manifest.json`:

```json
{
  "dependencies": {
    "com.licenseseat.sdk": "https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity"
  }
}
```

Or use Unity Package Manager:
1. Open Window > Package Manager
2. Click the + button > Add package from git URL
3. Enter: `https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity`

### Via OpenUPM

```bash
openupm add com.licenseseat.sdk
```

## Quick Start

### 1. Create Settings Asset

Right-click in Project window > Create > LicenseSeat > Settings

Configure your API key in the created asset.

### 2. Add LicenseSeatManager to Scene

Add the `LicenseSeatManager` component to a GameObject in your scene.

### 3. Activate & Validate Licenses

```csharp
using LicenseSeat;
using UnityEngine;

public class LicenseController : MonoBehaviour
{
    private LicenseSeatManager _manager;

    void Start()
    {
        _manager = FindObjectOfType<LicenseSeatManager>();

        // Subscribe to events
        _manager.Client.Events.On(LicenseSeatEvents.LicenseActivated, OnLicenseActivated);
        _manager.Client.Events.On(LicenseSeatEvents.ValidationFailed, OnValidationFailed);
    }

    public void ActivateLicense(string licenseKey)
    {
        // Using coroutine
        StartCoroutine(_manager.ActivateCoroutine(licenseKey, OnActivationComplete));
    }

    private void OnActivationComplete(License license, Exception error)
    {
        if (error != null)
        {
            Debug.LogError($"Activation failed: {error.Message}");
            return;
        }

        Debug.Log($"License activated: {license.LicenseKey}");
    }

    private void OnLicenseActivated(object data) => Debug.Log("License activated!");
    private void OnValidationFailed(object data) => Debug.LogWarning("Validation failed");
}
```

### 4. Check Entitlements

```csharp
// Check if user has a specific feature
if (_manager.Client.HasEntitlement("premium-features"))
{
    // Enable premium features
}

// Get detailed entitlement info
var status = _manager.Client.CheckEntitlement("max-projects");
if (status.Active)
{
    Debug.Log($"Projects limit: {status.Limit}");
}
```

## Platform-Specific Notes

### WebGL

The SDK automatically uses `UnityWebRequest` for HTTP operations on WebGL, as `System.Net.Http` is not supported. Ensure your API server has proper CORS headers configured.

### iOS & Android (IL2CPP)

The included `link.xml` prevents code stripping. If you encounter issues, ensure the file is in your project.

### Offline Validation

Enable offline fallback for games that may run without internet:

```csharp
var options = new LicenseSeatClientOptions
{
    ApiKey = "your-api-key",
    OfflineFallbackMode = OfflineFallbackMode.NetworkOnly,
    MaxOfflineDays = 7
};
```

## Documentation

- [Installation Guide](Documentation~/installation.md)
- [Quick Start](Documentation~/quickstart.md)
- [Troubleshooting](Documentation~/troubleshooting.md)
- [Platform Notes](Documentation~/platform-notes.md)

## Support

- Documentation: https://docs.licenseseat.com/sdk/unity
- Issues: https://github.com/licenseseat/licenseseat-csharp/issues
- Email: support@licenseseat.com

## License

MIT License - see LICENSE file for details.
