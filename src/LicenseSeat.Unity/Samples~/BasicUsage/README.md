# Basic Usage Sample

This sample demonstrates the fundamental license activation flow using the LicenseSeat Unity SDK.

## Setup Instructions

1. **Create a new scene** (or use an existing one)

2. **Add LicenseSeatManager**
   - Create an empty GameObject named "LicenseSeatManager"
   - Add the `LicenseSeatManager` component
   - Assign your `LicenseSeatSettings` ScriptableObject

3. **Create the UI**
   - Create a Canvas with the following elements:
     - **InputField**: For license key entry
     - **Button "Activate"**: To trigger activation
     - **Button "Validate"**: To validate current license
     - **Text "Status"**: To display status messages
     - **Panel "ActivationPanel"**: Container for activation UI
     - **Panel "LicensedPanel"**: Container for licensed state UI

4. **Add LicenseActivationUI**
   - Add the `LicenseActivationUI` component to your Canvas
   - Assign all the UI element references in the Inspector

## Code Overview

### LicenseActivationUI.cs

```csharp
// Activate a license
StartCoroutine(licenseSeatManager.ActivateCoroutine(licenseKey, OnActivationComplete));

// Validate current license
StartCoroutine(licenseSeatManager.ValidateCoroutine(licenseKey, OnValidationComplete));

// Subscribe to events
licenseSeatManager.Client.Events.On(LicenseSeatEvents.ActivationSuccess, OnActivationSuccess);
licenseSeatManager.Client.Events.On(LicenseSeatEvents.ValidationFailed, OnValidationFailed);
```

## Key Concepts

1. **Coroutine-based API**: Unity-friendly async operations using `StartCoroutine`
2. **Event System**: Subscribe to license events for reactive UI updates
3. **Error Handling**: Callbacks include error information for proper error display
4. **Lifecycle Management**: Unsubscribe from events in `OnDestroy`

## Testing

1. Configure your LicenseSeatSettings with valid API credentials
2. Enter a valid license key in the input field
3. Click "Activate" to activate the license
4. Click "Validate" to verify the license is still valid

## Next Steps

- See the **OfflineValidation** sample for handling offline scenarios
- Refer to the [documentation](https://licenseseat.com/docs/sdk/unity) for advanced usage
