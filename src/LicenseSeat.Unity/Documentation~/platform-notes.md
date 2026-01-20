# Platform Notes

Platform-specific considerations and configuration for the LicenseSeat Unity SDK.

## Supported Platforms

| Platform | Scripting Backend | Status | Notes |
|----------|-------------------|--------|-------|
| Windows | Mono | Supported | Full functionality |
| Windows | IL2CPP | Supported | Full functionality |
| macOS | Mono | Supported | Full functionality |
| macOS | IL2CPP | Supported | Full functionality |
| Linux | Mono | Supported | Full functionality |
| Linux | IL2CPP | Supported | Full functionality |
| Android | IL2CPP | Supported | See Android section |
| iOS | IL2CPP | Supported | See iOS section |
| WebGL | IL2CPP | Supported | See WebGL section |
| Consoles | Varies | Contact us | Platform-specific builds available |

## Desktop Platforms (Windows, macOS, Linux)

### Configuration

No special configuration required. Both Mono and IL2CPP scripting backends are fully supported.

### Device Fingerprinting

The SDK generates a unique device identifier based on:
- Hardware ID (where available)
- Machine name
- OS information

This identifier is consistent across app restarts but may change if hardware changes significantly.

### File Storage

License cache is stored in:
- **Windows**: `%APPDATA%/YourCompany/YourGame/`
- **macOS**: `~/Library/Application Support/YourCompany/YourGame/`
- **Linux**: `~/.config/unity3d/YourCompany/YourGame/`

## Android

### Requirements

- Minimum API Level: 21 (Android 5.0)
- Target API Level: 33+ recommended
- Scripting Backend: IL2CPP (required for Google Play)

### Permissions

The SDK requires internet permission. Add to `AndroidManifest.xml`:

```xml
<uses-permission android:name="android.permission.INTERNET" />
<uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
```

Unity usually adds these automatically.

### Network Security

For Android 9+, if using HTTP (not recommended), add network security config:

```xml
<!-- res/xml/network_security_config.xml -->
<network-security-config>
    <domain-config cleartextTrafficPermitted="true">
        <domain includeSubdomains="true">your-api-domain.com</domain>
    </domain-config>
</network-security-config>
```

**Recommendation**: Always use HTTPS.

### Device Identifier

On Android, the SDK uses a combination of:
- ANDROID_ID (Settings.Secure)
- Device model and manufacturer

Note: ANDROID_ID may change after factory reset.

### ProGuard/R8

If using code obfuscation, ensure LicenseSeat classes are kept. The included `link.xml` should handle this, but if issues occur, add to your ProGuard rules:

```
-keep class com.licenseseat.** { *; }
```

## iOS

### Requirements

- Minimum iOS Version: 12.0
- Xcode: Latest stable version
- Scripting Backend: IL2CPP (required)

### App Transport Security

iOS requires HTTPS by default. If you need to use HTTP (development only):

1. Open `Info.plist`
2. Add exception for your domain

**Recommendation**: Always use HTTPS.

### Device Identifier

On iOS, the SDK uses:
- identifierForVendor (IDFV)
- Device model

Note: IDFV changes if all apps from your developer account are uninstalled.

### Keychain Storage

Consider storing license keys in iOS Keychain for persistence across app reinstalls:

```csharp
// This is a conceptual example - implement using Unity's native plugins
PlayerPrefs.SetString("license_key", key); // Simple approach
// For production, consider KeychainAccess native plugin
```

### TestFlight

Licenses work the same in TestFlight as production. No special configuration needed.

## WebGL

### Critical: Networking Limitations

WebGL runs in a browser sandbox with significant networking restrictions:

- **No System.Net.Sockets** - Raw sockets are not available
- **No HttpClient** - Standard .NET HTTP client doesn't work
- **CORS Required** - Browser enforces cross-origin restrictions

The SDK automatically uses `UnityWebRequest` on WebGL, which handles these limitations.

### CORS Configuration

Your API server MUST include proper CORS headers:

```
Access-Control-Allow-Origin: https://your-game-domain.com
Access-Control-Allow-Methods: GET, POST, PUT, DELETE, OPTIONS
Access-Control-Allow-Headers: Content-Type, Authorization, X-Requested-With
Access-Control-Allow-Credentials: true
```

For development, you can use:
```
Access-Control-Allow-Origin: *
```

### Browser Storage

License data is stored in:
- **IndexedDB** (primary)
- **localStorage** (fallback)

Note: Private/incognito browsing may limit storage availability.

### Device Fingerprinting

WebGL device fingerprinting is limited due to browser privacy features:
- Screen resolution
- User agent
- Canvas fingerprint (if available)

Consider using account-based licensing for WebGL rather than device-based.

### WebGL-Specific Settings

```csharp
var options = new LicenseSeatClientOptions
{
    ApiKey = "your-key",
    // Shorter offline period for WebGL (storage is less reliable)
    MaxOfflineDays = 1,
    // Consider more frequent validation
    AutoValidateInterval = 300
};
```

### Testing WebGL Locally

1. Build WebGL project
2. Use a local server (Python, Node, etc.):
   ```bash
   python -m http.server 8000
   ```
3. Access via `localhost:8000`

Note: `file://` protocol has additional restrictions.

## IL2CPP Considerations

### Code Stripping

The SDK includes `link.xml` to prevent critical code from being stripped. If you encounter issues:

1. Check that `link.xml` is in the package
2. Try reducing stripping level in Player Settings
3. Add custom types to your own `link.xml` if needed

### Generic Type Issues

IL2CPP requires AOT compilation of generic types. The SDK's `link.xml` covers common cases. If you see errors like:

```
ExecutionEngineException: Attempting to call method for which no ahead of time (AOT) code was generated
```

You may need to add type hints. Create a file that references the types:

```csharp
// AOTHints.cs
public class AOTHints
{
    void Hints()
    {
        // Force AOT compilation of generic types
        var list = new List<License>();
        var dict = new Dictionary<string, Entitlement>();
    }
}
```

## Console Platforms

For PlayStation, Xbox, and Nintendo Switch:
- Contact us for platform-specific builds
- Native platform certification requirements may apply
- Special licensing arrangements available

Email: enterprise@licenseseat.com

## Performance Recommendations

| Platform | Recommendation |
|----------|----------------|
| Mobile | Validate on app start, then every 5-10 minutes |
| Desktop | Validate on start, then every 15-30 minutes |
| WebGL | Validate frequently (5 minutes) due to storage limitations |

## Offline Support by Platform

| Platform | Max Recommended Offline Days |
|----------|------------------------------|
| Desktop | 30 days |
| Mobile | 14 days |
| WebGL | 1-3 days |

Adjust based on your security requirements and user needs.
