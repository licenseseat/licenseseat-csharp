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

Unity requires manual setup since it doesn't have native NuGet support.

**Installation (Option 1 - NuGetForUnity):**

1. Install [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) via the Package Manager:
   - Add package from git URL: `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity`

2. Go to **NuGet** → **Manage NuGet Packages** and search for `LicenseSeat`

3. Click **Install**

**Installation (Option 2 - Manual DLL):**

1. Download the `.nupkg` from [NuGet.org](https://www.nuget.org/packages/LicenseSeat/)
2. Rename to `.zip` and extract
3. Copy the DLL from `lib/netstandard2.0/` to your Unity project's `Assets/Plugins/` folder
4. Also copy the dependency DLLs:
   - `System.Text.Json.dll`
   - `BouncyCastle.Crypto.dll`
   - `Microsoft.Extensions.DependencyInjection.Abstractions.dll`
   - `Microsoft.Extensions.Options.dll`

**Required: Add HttpClient Reference**

Create a file named `csc.rsp` in your `Assets/` folder with:

```
-r:System.Net.Http.dll
```

**Usage in Unity:**

```csharp
using UnityEngine;
using LicenseSeat;
using System.Threading.Tasks;

public class LicenseManager : MonoBehaviour
{
    private LicenseSeatClient _client;

    void Start()
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
                Debug.Log("License is valid!");
                // Unlock features
            }
            else
            {
                Debug.LogWarning($"License invalid: {result.Error}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"License check failed: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        _client?.Dispose();
    }
}
```

**Unity Tips:**

- Always dispose the client in `OnDestroy()` to prevent memory leaks
- For WebGL builds, async HTTP may have limitations - consider using offline validation
- If building for iOS/Android with IL2CPP, add a `link.xml` file to preserve SDK types:

```xml
<linker>
    <assembly fullname="LicenseSeat" preserve="all"/>
    <assembly fullname="System.Text.Json" preserve="all"/>
    <assembly fullname="BouncyCastle.Crypto" preserve="all"/>
</linker>
```

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

Releases are automated via GitHub Actions. To publish a new version to NuGet:

1. **Update the version** in `src/LicenseSeat/LicenseSeat.csproj`:
   ```xml
   <Version>1.0.0</Version>
   ```

2. **Create a GitHub Release**:
   ```bash
   # Create and push a tag
   git tag v1.0.0
   git push origin v1.0.0
   ```

   Then go to [GitHub Releases](https://github.com/licenseseat/licenseseat-csharp/releases) and create a new release from the tag.

   **Or use the GitHub CLI:**
   ```bash
   gh release create v1.0.0 --title "v1.0.0" --notes "Release notes here"
   ```

3. **Automatic publishing**: The release workflow will automatically:
   - Build and test the package
   - Create the NuGet package with the release version
   - Publish to [NuGet.org](https://www.nuget.org/packages/LicenseSeat/)
   - Attach the `.nupkg` file to the GitHub release

#### Version Guidelines

- **Major** (`X.0.0`): Breaking API changes
- **Minor** (`0.X.0`): New features, backward compatible
- **Patch** (`0.0.X`): Bug fixes, backward compatible
- **Prerelease** (`1.0.0-beta.1`): Preview versions

#### Manual Release (if needed)

You can also trigger a release manually from the Actions tab:

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