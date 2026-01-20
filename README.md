# LicenseSeat C# SDK

[![CI](https://github.com/licenseseat/licenseseat-csharp/actions/workflows/ci.yml/badge.svg)](https://github.com/licenseseat/licenseseat-csharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/LicenseSeat.svg)](https://www.nuget.org/packages/LicenseSeat/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Official C# SDK for the [LicenseSeat](https://licenseseat.com) API. Validate licenses, manage activations, and check entitlements in your .NET applications.

## Features

- **License Management** - Activate, validate, and deactivate licenses
- **Entitlement Checking** - Feature gating with expiration support
- **Offline Validation** - Ed25519 signature verification for offline use
- **Auto-Validation** - Configurable background validation
- **Event System** - React to license state changes
- **Dependency Injection** - First-class ASP.NET Core support
- **Cross-Platform** - .NET Standard 2.0 compatible

## Installation

```bash
dotnet add package LicenseSeat
```

Or via the NuGet Package Manager:

```
Install-Package LicenseSeat
```

## Requirements

- .NET Standard 2.0+ (compatible with .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+, Unity, Godot)

## Quick Start

### Basic Usage

```csharp
using LicenseSeat;

// Create client
var client = new LicenseSeatClient(new LicenseSeatClientOptions
{
    ApiKey = "your-api-key"
});

// Activate a license
var license = await client.ActivateAsync("LICENSE-KEY-HERE");
Console.WriteLine($"License activated: {license.LicenseKey}");

// Check entitlements
if (client.HasEntitlement("pro-features"))
{
    // Enable pro features
}

// Get license status
var status = client.GetStatus();
Console.WriteLine($"Status: {status.StatusType}");
```

### Static API (Singleton Pattern)

```csharp
using LicenseSeat;

// Configure once at app startup
LicenseSeat.Configure("your-api-key", options =>
{
    options.AutoValidateInterval = TimeSpan.FromHours(1);
});

// Use anywhere in your app
await LicenseSeat.Activate("LICENSE-KEY");

if (LicenseSeat.HasEntitlement("premium"))
{
    // Premium features
}

// Cleanup on shutdown
LicenseSeat.Shutdown();
```

### ASP.NET Core Dependency Injection

```csharp
// In Program.cs or Startup.cs
services.AddLicenseSeatClient("your-api-key");

// Or with configuration
services.AddLicenseSeatClient(options =>
{
    options.ApiKey = "your-api-key";
    options.AutoValidateInterval = TimeSpan.FromMinutes(30);
    options.Debug = true;
});
```

```csharp
// In your controller or service
public class MyController : ControllerBase
{
    private readonly ILicenseSeatClient _licenseClient;

    public MyController(ILicenseSeatClient licenseClient)
    {
        _licenseClient = licenseClient;
    }

    public async Task<IActionResult> CheckLicense()
    {
        var status = _licenseClient.GetStatus();
        return Ok(new { status.StatusType, status.Message });
    }
}
```

### Event Handling

```csharp
var client = new LicenseSeatClient(options);

// Subscribe to events
client.Events.On(LicenseSeatEvents.LicenseValidated, data =>
{
    Console.WriteLine("License validated successfully!");
});

client.Events.On(LicenseSeatEvents.ValidationFailed, data =>
{
    Console.WriteLine("Validation failed - check your license");
});

client.Events.On(LicenseSeatEvents.EntitlementChanged, data =>
{
    Console.WriteLine("Entitlements updated");
});
```

### Offline Validation

```csharp
var options = new LicenseSeatClientOptions
{
    ApiKey = "your-api-key",
    OfflineFallbackMode = OfflineFallbackMode.NetworkOnly,
    MaxOfflineDays = 7,  // Allow 7 days offline
    MaxClockSkew = TimeSpan.FromMinutes(5)  // Clock tamper detection
};

var client = new LicenseSeatClient(options);

// Validation will fall back to offline when network is unavailable
var result = await client.ValidateAsync("LICENSE-KEY");

if (result.Offline)
{
    Console.WriteLine("Validated offline with cached license");
}
```

## Configuration Options

| Option                 | Default                       | Description                                      |
| ---------------------- | ----------------------------- | ------------------------------------------------ |
| `ApiKey`               | `null`                        | Your LicenseSeat API key (required)              |
| `ApiBaseUrl`           | `https://licenseseat.com/api` | API base URL                                     |
| `AutoValidateInterval` | 1 hour                        | Interval between auto-validations (0 to disable) |
| `MaxRetries`           | 3                             | Maximum retry attempts for failed requests       |
| `RetryDelay`           | 1 second                      | Base delay between retries (exponential backoff) |
| `OfflineFallbackMode`  | `Disabled`                    | When to use offline validation                   |
| `MaxOfflineDays`       | 0                             | Grace period for offline usage (0 = disabled)    |
| `MaxClockSkew`         | 5 minutes                     | Tolerance for clock tamper detection             |
| `Debug`                | `false`                       | Enable debug logging                             |
| `HttpTimeout`          | 30 seconds                    | HTTP request timeout                             |

## Game Engine Integration

### Godot 4 (C#)

Godot 4.x has native NuGet support - the SDK works out of the box.

**Installation:**

Add the package reference to your `.csproj` file:

```xml
<ItemGroup>
    <PackageReference Include="LicenseSeat" Version="0.1.0" />
</ItemGroup>
```

Or use the dotnet CLI:

```bash
dotnet add package LicenseSeat
```

**Usage in Godot:**

```csharp
using Godot;
using LicenseSeat;

public partial class LicenseManager : Node
{
    private LicenseSeatClient _client;

    public override void _Ready()
    {
        _client = new LicenseSeatClient(new LicenseSeatClientOptions
        {
            ApiKey = "your-api-key"
        });
    }

    public async void ValidateLicense(string licenseKey)
    {
        try
        {
            var result = await _client.ValidateAsync(licenseKey);
            if (result.Valid)
            {
                GD.Print("License is valid!");
                // Unlock features
            }
            else
            {
                GD.Print($"License invalid: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"License check failed: {ex.Message}");
        }
    }

    public override void _ExitTree()
    {
        _client?.Dispose();
    }
}
```

### Unity

We provide a dedicated **Unity SDK** with full cross-platform support, including WebGL, iOS, and Android with IL2CPP.

**Installation (Recommended - Unity Package Manager):**

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL...**
3. Enter:
   ```
   https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity
   ```

Or add directly to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.licenseseat.sdk": "https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity"
  }
}
```

**For a specific version:**
```
https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity#v0.2.0
```

**Installation (OpenUPM):**

```bash
openupm add com.licenseseat.sdk
```

**Usage in Unity:**

```csharp
using UnityEngine;
using LicenseSeat;

public class LicenseController : MonoBehaviour
{
    private LicenseSeatManager _manager;

    void Start()
    {
        _manager = FindObjectOfType<LicenseSeatManager>();

        // Subscribe to events
        _manager.Client.Events.On(LicenseSeatEvents.LicenseValidated, _ =>
            Debug.Log("License validated!"));
    }

    public void ActivateLicense(string licenseKey)
    {
        // Using coroutine for Unity-friendly async
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
}
```

**Unity SDK Features:**

- **Pure C#** - No native DLLs, works on all platforms
- **IL2CPP Compatible** - Automatic link.xml injection via `IUnityLinkerProcessor`
- **WebGL Support** - Uses `UnityWebRequest` instead of `HttpClient`
- **Unity-Native** - ScriptableObject configuration, MonoBehaviour integration
- **Editor Tools** - Settings window, custom inspectors
- **Samples Included** - Basic usage and offline validation examples

For full Unity documentation, see [src/LicenseSeat.Unity/README.md](src/LicenseSeat.Unity/README.md).

### Windows Desktop Apps (WPF, WinForms, MAUI)

The SDK works natively with all Windows desktop frameworks. Just install via NuGet:

```bash
dotnet add package LicenseSeat
```

## Documentation

For full API documentation, visit [licenseseat.com/docs](https://licenseseat.com/docs).

## Development

### Prerequisites

- .NET SDK 9.0+

### Building

```bash
dotnet build
```

### Testing

```bash
dotnet test
```

### Running Tests with Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Packaging

```bash
dotnet pack --configuration Release --output ./artifacts
```

### Releasing a New Version

This repository contains two distributable packages:
- **NuGet Package** (`LicenseSeat`) - For .NET applications, Godot, etc.
- **Unity Package** (`com.licenseseat.sdk`) - For Unity projects via UPM/OpenUPM

#### Release Process

**1. Update versions in both packages:**

```bash
# NuGet package version
# Edit src/LicenseSeat/LicenseSeat.csproj
<Version>1.0.0</Version>

# Unity package version
# Edit src/LicenseSeat.Unity/package.json
"version": "1.0.0"

# Unity changelog
# Edit src/LicenseSeat.Unity/CHANGELOG.md
## [1.0.0] - YYYY-MM-DD
```

**2. Ensure Unity files are in sync:**

```bash
./scripts/validate-unity-sync.sh
./scripts/validate-unity-package.sh
```

**3. Commit and create a GitHub Release:**

```bash
git add -A
git commit -m "Bump version to 1.0.0"
git push

# Create and push a tag
git tag v1.0.0
git push origin v1.0.0

# Create the release
gh release create v1.0.0 --title "v1.0.0" --notes "Release notes here"
```

**4. Automatic publishing**: The release workflow will:
- Build and test both packages
- Publish NuGet package to [NuGet.org](https://www.nuget.org/packages/LicenseSeat/)
- Attach the `.nupkg` file to the GitHub release

**5. Unity package is automatically available** via Git URL:
```
https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity#v1.0.0
```

#### OpenUPM Submission (One-Time Setup)

To make the Unity package available via `openupm add`:

1. Go to [openupm.com/packages/add](https://openupm.com/packages/add/)
2. Enter repository URL: `https://github.com/licenseseat/licenseseat-csharp`
3. The build pipelines will auto-detect `package.json` at `src/LicenseSeat.Unity/`
4. Submit for review (typically approved within 24 hours)

After approval, OpenUPM automatically tracks new Git tags and publishes updates.

#### Version Guidelines

| Type | Example | When to Use |
|------|---------|-------------|
| Major | `1.0.0` → `2.0.0` | Breaking API changes |
| Minor | `1.0.0` → `1.1.0` | New features, backward compatible |
| Patch | `1.0.0` → `1.0.1` | Bug fixes, backward compatible |
| Prerelease | `1.0.0-beta.1` | Preview/testing versions |

#### Manual Release (if needed)

You can trigger a release manually from the Actions tab:

1. Go to **Actions** → **Release** workflow
2. Click **Run workflow**
3. Enter the version number (e.g., `1.0.0`)
4. Check "Is this a prerelease?" if applicable

#### NuGet Trusted Publishing Setup

This repository uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) for secure, keyless authentication via OIDC. No API keys are stored as secrets.

**Required configuration:**

1. **GitHub Repository Variable:**
   - Go to **Settings** → **Secrets and variables** → **Actions** → **Variables**
   - Add `NUGET_USER` with your NuGet.org profile name (not email)

2. **GitHub Environment:**
   - Create an environment named `nuget-publish` in **Settings** → **Environments**

3. **NuGet.org Trusted Publishing Policy:**
   - Log in to [nuget.org](https://www.nuget.org)
   - Go to your profile → **Trusted Publishing**
   - Add a policy with:
     - Repository Owner: `licenseseat`
     - Repository: `licenseseat-csharp`
     - Workflow: `release.yml`
     - Environment: `nuget-publish`

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

MIT - see [LICENSE](LICENSE) for details.