# Installation Guide

This guide covers all methods for installing the LicenseSeat Unity SDK.

## Requirements

- **Unity Version**: 2021.3 LTS or newer (recommended: 2022.3 LTS or Unity 6)
- **Scripting Backend**: Mono or IL2CPP
- **API Compatibility Level**: .NET Standard 2.0 or .NET 4.x

## Installation Methods

### Method 1: Git URL (Recommended)

The simplest way to install the SDK is via Git URL in the Unity Package Manager.

1. Open **Window > Package Manager**
2. Click the **+** button in the top-left corner
3. Select **Add package from git URL...**
4. Enter the following URL:

```
https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity
```

5. Click **Add**

#### Specifying a Version

To lock to a specific version, append the version tag:

```
https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity#v1.0.0
```

### Method 2: manifest.json

Add the package directly to your `Packages/manifest.json` file:

```json
{
  "dependencies": {
    "com.licenseseat.sdk": "https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity"
  }
}
```

### Method 3: OpenUPM

If you prefer using OpenUPM:

```bash
openupm add com.licenseseat.sdk
```

Or add the scoped registry to your `manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.licenseseat"]
    }
  ],
  "dependencies": {
    "com.licenseseat.sdk": "1.0.0"
  }
}
```

### Method 4: Local Development

For local development or testing:

1. Clone the repository:
   ```bash
   git clone https://github.com/licenseseat/licenseseat-csharp.git
   ```

2. In Unity Package Manager, click **+** > **Add package from disk...**

3. Navigate to `licenseseat-csharp/src/LicenseSeat.Unity/package.json`

## Verifying Installation

After installation:

1. Check **Window > Package Manager** - you should see "LicenseSeat SDK"
2. Check **Edit > Project Settings > LicenseSeat** - settings panel should appear
3. Check **Window > LicenseSeat > Settings** - editor window should open

## Post-Installation Setup

1. **Create Settings Asset**
   - Right-click in Project window
   - Select **Create > LicenseSeat > Settings**
   - Configure your API key and Product ID

2. **Add to Scene**
   - Create an empty GameObject
   - Add the `LicenseSeatManager` component
   - Assign your Settings asset

See the [Quick Start Guide](quickstart.md) for next steps.

## Updating the SDK

### Via Package Manager

1. Open **Window > Package Manager**
2. Select "LicenseSeat SDK"
3. Click **Update** (if available)

### Via manifest.json

Change the version tag in the URL:

```json
"com.licenseseat.sdk": "https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity#v1.1.0"
```

## Uninstalling

1. Open **Window > Package Manager**
2. Select "LicenseSeat SDK"
3. Click **Remove**

Or remove the entry from `Packages/manifest.json`.

## Troubleshooting Installation

### "Could not resolve package"

- Ensure you have Git installed and accessible from command line
- Check your internet connection
- Verify the repository URL is correct

### "Compilation errors after install"

- Check Unity version compatibility (2021.3+)
- Ensure API Compatibility Level is set correctly
- Try **Assets > Reimport All**

### Package doesn't appear in Package Manager

- Close and reopen Unity
- Delete the `Library/PackageCache` folder and let Unity reimport

See [Troubleshooting](troubleshooting.md) for more solutions.
