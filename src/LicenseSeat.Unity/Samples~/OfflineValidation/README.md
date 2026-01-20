# Offline Validation Sample

This sample demonstrates how to handle license validation when the user may not have internet connectivity - a common scenario for games.

## Overview

The LicenseSeat SDK supports offline license validation through:

1. **Cached License Data**: License information is stored locally after successful online validation
2. **Offline Fallback Mode**: Configure how the SDK behaves when offline
3. **Configurable Grace Period**: Set how long a cached license remains valid

## Setup Instructions

1. **Configure Offline Settings**

   In your `LicenseSeatSettings` ScriptableObject:
   - Set **Offline Fallback Mode** to `NetworkOnly` or `Always`
   - Set **Max Offline Days** to your desired grace period (e.g., 7 days)

2. **Create the Scene**
   - Add `LicenseSeatManager` to the scene
   - Create UI with:
     - Text for connection status
     - Text for license status
     - Text for last validation time
     - Button for manual validation
     - Toggle to simulate offline mode (for testing)

3. **Add OfflineLicenseManager**
   - Attach the `OfflineLicenseManager` script
   - Assign all UI references

## Offline Fallback Modes

```csharp
// Configure in LicenseSeatClientOptions
var options = new LicenseSeatClientOptions
{
    ApiKey = "your-api-key",
    OfflineFallbackMode = OfflineFallbackMode.Always,
    MaxOfflineDays = 7
};
```

| Mode | Description |
|------|-------------|
| `Disabled` | No offline fallback; always require network |
| `NetworkOnly` | Try network first, fall back to cache on network failure |
| `Always` | Always use offline validation when cache is available |

## Events

```csharp
// Listen for offline validation success
client.Events.On(LicenseSeatEvents.ValidationOfflineSuccess, data => {
    Debug.Log("Using cached license - device is offline");
});
```

## Best Practices

1. **Initial Online Validation**
   - Ensure at least one successful online validation before going offline
   - The SDK caches license data automatically

2. **Grace Period**
   - Set `MaxOfflineDays` based on your security requirements
   - Shorter periods are more secure but less user-friendly
   - Typical values: 7-30 days

3. **User Communication**
   - Show clear status indicators (Online/Offline)
   - Warn users when approaching the offline limit
   - Explain what happens when the grace period expires

4. **Handling Expiration**
   - When the offline grace period expires, require reconnection
   - Provide clear instructions for users to regain access

## Code Example

```csharp
// Check if we can validate offline
bool canValidateOffline = client.HasCachedLicense &&
    client.CachedLicenseAge < TimeSpan.FromDays(options.MaxOfflineDays);

if (!hasNetwork && canValidateOffline)
{
    // Use cached validation
    var result = client.ValidateOffline();
    if (result.Valid)
    {
        EnableFeatures();
    }
}
else if (!hasNetwork && !canValidateOffline)
{
    // Must go online
    ShowNetworkRequiredMessage();
}
```

## Testing Offline Scenarios

1. Enable "Simulate Offline" toggle in the sample UI
2. Observe how the SDK falls back to cached data
3. Test edge cases:
   - Fresh install with no cache
   - Expired cache
   - Invalid cached license

## Platform Notes

### Mobile (iOS/Android)
- Cache is stored in `Application.persistentDataPath`
- Data persists across app updates

### WebGL
- Browser storage limitations may apply
- Consider shorter offline periods

### Console
- Platform-specific storage APIs are used automatically
