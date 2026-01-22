# LicenseSeat C# SDK

[![CI](https://github.com/licenseseat/licenseseat-csharp/actions/workflows/ci.yml/badge.svg)](https://github.com/licenseseat/licenseseat-csharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/LicenseSeat.svg)](https://www.nuget.org/packages/LicenseSeat/)
[![OpenUPM](https://img.shields.io/npm/v/com.licenseseat.sdk?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.licenseseat.sdk/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

Official C# SDK for the [LicenseSeat](https://licenseseat.com) licensing platform. Add license validation to your app in minutes.

> [!TIP]
> **Building a Unity game?** We have a dedicated [Unity SDK](#unity) with full IL2CPP, WebGL, iOS, and Android support. No DLLs. Just install via Unity Package Manager and go!

## Quick Start

**Install:**
```bash
dotnet add package LicenseSeat
```

**Use:**
```csharp
using LicenseSeat;

var client = new LicenseSeatClient(new LicenseSeatClientOptions
{
    ApiKey = "your-api-key",
    ProductSlug = "your-product"
});

// Activate a license
var license = await client.ActivateAsync("XXXX-XXXX-XXXX-XXXX");

// Check entitlements
if (client.HasEntitlement("pro-features"))
{
    // Enable pro features
}
```

That's it. You're licensed.

## Features

| Feature                | Description                                     |
| ---------------------- | ----------------------------------------------- |
| **License Activation** | Activate, validate, and deactivate licenses     |
| **Entitlements**       | Feature gating with usage limits and expiration |
| **Offline Mode**       | Ed25519 signature verification when offline     |
| **Auto-Validation**    | Background validation at configurable intervals |
| **Events**             | React to license changes in real-time           |
| **DI Support**         | First-class ASP.NET Core integration            |

## Platform Support

| Platform                               | Package | Install                          |
| -------------------------------------- | ------- | -------------------------------- |
| **.NET** (Console, ASP.NET, WPF, MAUI) | NuGet   | `dotnet add package LicenseSeat` |
| **Godot 4**                            | NuGet   | `dotnet add package LicenseSeat` |
| **Unity**                              | UPM     | [See Unity section](#unity)      |

## Installation

### NuGet (.NET, Godot)

```bash
dotnet add package LicenseSeat
```

**Requirements:** .NET Standard 2.0+ (.NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)

### Unity

> [!NOTE]
> Unity has a dedicated SDK with WebGL, iOS, and Android support. Don't use NuGet for Unity - use the Package Manager instead.

**Option 1: Git URL (Recommended)**

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL...**
3. Paste:
   ```
   https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity
   ```

**Option 2: manifest.json**

Add to `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.licenseseat.sdk": "https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity"
  }
}
```

**Option 3: OpenUPM**
```bash
openupm add com.licenseseat.sdk
```

**Pin to a version:**
```
https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity#v0.2.0
```

## Usage Examples

### Basic Client

```csharp
using LicenseSeat;

var client = new LicenseSeatClient(new LicenseSeatClientOptions
{
    ApiKey = "your-api-key",
    ProductSlug = "your-product"
});

// Activate a license (binds to this device)
var license = await client.ActivateAsync("LICENSE-KEY");
Console.WriteLine($"Activated: {license.Key}");
Console.WriteLine($"Status: {license.Status}");
Console.WriteLine($"Plan: {license.PlanKey}");

// Validate a license (check if it's valid without activating)
var result = await client.ValidateAsync("LICENSE-KEY");
if (result.Valid)
{
    Console.WriteLine("License is valid!");
    Console.WriteLine($"Active Seats: {result.License?.ActiveSeats}/{result.License?.SeatLimit}");
}
else
{
    Console.WriteLine($"Invalid: {result.Code} - {result.Message}");
}

// Check entitlements
if (client.HasEntitlement("premium"))
{
    // Unlock premium features
}

// Get current status
var status = client.GetStatus();
Console.WriteLine($"Status: {status.StatusType}");

// Deactivate when done (frees up a seat)
await client.DeactivateAsync();
```

### Static API (Singleton)

Perfect for desktop apps where you want global access:

```csharp
using LicenseSeat;

// Configure once at startup
LicenseSeat.LicenseSeat.Configure("your-api-key", "your-product", options =>
{
    options.AutoValidateInterval = TimeSpan.FromHours(1);
});

// Use anywhere in your app
await LicenseSeat.LicenseSeat.Activate("LICENSE-KEY");

if (LicenseSeat.LicenseSeat.HasEntitlement("premium"))
{
    // Premium features
}

var status = LicenseSeat.LicenseSeat.GetStatus();
var license = LicenseSeat.LicenseSeat.GetCurrentLicense();

// Cleanup on exit
LicenseSeat.LicenseSeat.Shutdown();
```

### ASP.NET Core Dependency Injection

```csharp
// Program.cs
builder.Services.AddLicenseSeatClient("your-api-key", "your-product");

// Or with full options:
builder.Services.AddLicenseSeatClient(options =>
{
    options.ApiKey = "your-api-key";
    options.ProductSlug = "your-product";
    options.AutoValidateInterval = TimeSpan.FromMinutes(30);
});
```

```csharp
// Your controller or service
public class LicenseController : ControllerBase
{
    private readonly ILicenseSeatClient _client;

    public LicenseController(ILicenseSeatClient client) => _client = client;

    [HttpPost("activate")]
    public async Task<IActionResult> Activate([FromBody] string licenseKey)
    {
        var license = await _client.ActivateAsync(licenseKey);
        return Ok(new { license.Key, license.Status });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var status = _client.GetStatus();
        return Ok(new { status.StatusType, status.Message });
    }
}
```

### Event Handling

```csharp
// Subscribe to license events
client.Events.On(LicenseSeatEvents.LicenseValidated, _ =>
    Console.WriteLine("License validated!"));

client.Events.On(LicenseSeatEvents.ValidationFailed, _ =>
    Console.WriteLine("Validation failed!"));

client.Events.On(LicenseSeatEvents.EntitlementChanged, _ =>
    Console.WriteLine("Entitlements updated!"));

client.Events.On(LicenseSeatEvents.LicenseActivated, license =>
    Console.WriteLine($"Activated: {((License)license).Key}"));

client.Events.On(LicenseSeatEvents.LicenseDeactivated, _ =>
    Console.WriteLine("License deactivated"));
```

### Offline Validation

```csharp
var client = new LicenseSeatClient(new LicenseSeatClientOptions
{
    ApiKey = "your-api-key",
    ProductSlug = "your-product",
    OfflineFallbackMode = OfflineFallbackMode.NetworkOnly,
    MaxOfflineDays = 7  // Allow 7 days offline
});

// Validate - falls back to cached offline token if network fails
var result = await client.ValidateAsync("LICENSE-KEY");
if (result.Offline)
{
    Console.WriteLine("Validated offline with cached license");
}
```

The SDK automatically fetches and caches Ed25519-signed offline tokens after activation. When offline:
- Validates token signature cryptographically
- Checks token expiration (`exp` timestamp)
- Detects clock tampering
- Returns cached entitlements

### Godot 4

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
            ApiKey = "your-api-key",
            ProductSlug = "your-product"
        });
    }

    public async void ValidateLicense(string licenseKey)
    {
        var result = await _client.ValidateAsync(licenseKey);
        if (result.Valid)
            GD.Print("License is valid!");
        else
            GD.Print($"Invalid: {result.Code}");
    }

    public override void _ExitTree() => _client?.Dispose();
}
```

### Unity

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
        StartCoroutine(_manager.ActivateCoroutine(licenseKey, (license, error) =>
        {
            if (error != null)
            {
                Debug.LogError($"Failed: {error.Message}");
                return;
            }
            Debug.Log($"Activated: {license.Key}");
        }));
    }
}
```

**Unity SDK Features:**
- **Pure C#** - No native DLLs, works everywhere
- **IL2CPP Ready** - Automatic link.xml injection
- **WebGL Support** - Uses UnityWebRequest
- **Editor Tools** - Settings window, inspectors
- **Samples** - Import from Package Manager

Full Unity docs: [src/LicenseSeat.Unity/README.md](src/LicenseSeat.Unity/README.md)

## Configuration

| Option                 | Default                            | Description                                   |
| ---------------------- | ---------------------------------- | --------------------------------------------- |
| `ApiKey`               | —                                  | Your LicenseSeat API key **(required)**       |
| `ProductSlug`          | —                                  | Your product identifier **(required)**        |
| `ApiBaseUrl`           | `https://licenseseat.com/api/v1`   | API endpoint                                  |
| `AutoValidateInterval` | 1 hour                             | Background validation interval (0 = disabled) |
| `MaxRetries`           | 3                                  | Retry attempts for failed requests            |
| `RetryDelay`           | 1 second                           | Base delay between retries                    |
| `OfflineFallbackMode`  | `Disabled`                         | Offline validation mode                       |
| `MaxOfflineDays`       | 0                                  | Offline grace period (0 = disabled)           |
| `MaxClockSkew`         | 5 minutes                          | Clock tamper tolerance                        |
| `HttpTimeout`          | 30 seconds                         | Request timeout                               |
| `Debug`                | `false`                            | Enable debug logging                          |

## API Reference

### LicenseSeatClient Methods

| Method | Description |
|--------|-------------|
| `ActivateAsync(licenseKey)` | Activate a license on this device |
| `ValidateAsync(licenseKey)` | Validate a license (check if valid) |
| `DeactivateAsync()` | Deactivate the current license |
| `HasEntitlement(key)` | Check if an entitlement is active |
| `CheckEntitlement(key)` | Get detailed entitlement status |
| `GetStatus()` | Get current license status |
| `GetCurrentLicense()` | Get the cached license |
| `TestAuthAsync()` | Test API connectivity |

### ValidationResult Properties

| Property | Type | Description |
|----------|------|-------------|
| `Valid` | `bool` | Whether the license is valid |
| `Code` | `string?` | Error code if invalid |
| `Message` | `string?` | Error message if invalid |
| `Offline` | `bool` | True if validated offline |
| `License` | `License?` | License data |
| `ActiveEntitlements` | `List<Entitlement>?` | Active entitlements |
| `Warnings` | `List<ValidationWarning>?` | Any warnings |

### License Properties

| Property | Type | Description |
|----------|------|-------------|
| `Key` | `string` | The license key |
| `Status` | `string?` | License status (active, expired, etc.) |
| `ExpiresAt` | `DateTimeOffset?` | When the license expires |
| `PlanKey` | `string?` | Associated plan |
| `SeatLimit` | `int?` | Maximum allowed seats |
| `ActiveSeats` | `int` | Currently used seats |
| `ActiveEntitlements` | `List<Entitlement>?` | Active entitlements |

## Error Handling

```csharp
try
{
    var license = await client.ActivateAsync("INVALID-KEY");
}
catch (ApiException ex) when (ex.Code == "license_not_found")
{
    Console.WriteLine("License key not found");
}
catch (ApiException ex) when (ex.Code == "seat_limit_exceeded")
{
    Console.WriteLine($"All {ex.Details?["seat_limit"]} seats are in use");
}
catch (ApiException ex)
{
    Console.WriteLine($"API Error: {ex.Code} - {ex.Message}");
    Console.WriteLine($"Status: {ex.StatusCode}");
    Console.WriteLine($"Retryable: {ex.IsRetryable}");
}
```

Common error codes:
- `license_not_found` - Invalid license key
- `license_expired` - License has expired
- `license_suspended` - License is suspended
- `seat_limit_exceeded` - All seats are in use
- `device_not_activated` - Device not activated for this license
- `invalid_api_key` - Invalid API key

## Documentation

Full API documentation: [licenseseat.com/docs](https://licenseseat.com/docs)

---

## Development

### Prerequisites

- .NET SDK 9.0+

### Commands

```bash
# Build
dotnet build

# Test (unit tests)
dotnet test

# Test with coverage
dotnet test --collect:"XPlat Code Coverage"

# Package
dotnet pack --configuration Release --output ./artifacts
```

### Testing

The SDK has two test suites:

#### Unit Tests

Unit tests run offline and test internal SDK logic:

```bash
dotnet test tests/LicenseSeat.Tests
```

#### Integration Tests (Stress Tests)

Integration tests run against the live LicenseSeat API and validate the complete SDK functionality:

```bash
dotnet run --project tests/StressTest
```

**What's tested:**

| Category | Tests |
|----------|-------|
| **Client Operations** | Create, authenticate, validate, activate, deactivate |
| **Static API** | Singleton pattern for desktop apps |
| **Dependency Injection** | ASP.NET Core integration |
| **Error Handling** | Invalid keys, wrong product, invalid API key |
| **Stress Tests** | Concurrent validations, parallel client creation |
| **Offline Cryptography** | Ed25519 signature verification, tamper detection |
| **User Journey** | 15 real-world customer scenarios |

**User Journey Scenarios:**

1. First-time user activation
2. Entitlement/feature gating
3. Auto-validation in background
4. Offline token caching
5. Network outage handling
6. Tampered token detection
7. Clock tampering detection
8. Expired token handling
9. Device switching
10. Seat limit enforcement
11. Invalid license key handling
12. Wrong product slug (security)
13. Invalid API key (security)
14. Event-driven UI updates
15. Graceful shutdown

**Running with your own credentials:**

Set environment variables before running:

```bash
export LICENSESEAT_API_KEY="your-api-key"
export LICENSESEAT_PRODUCT_SLUG="your-product"
export LICENSESEAT_LICENSE_KEY="your-license-key"
dotnet run --project tests/StressTest
```

> **Note:** Integration tests require a valid LicenseSeat account and license. Tests may fail if the license seat limit is reached.

### Releasing

This repo contains two packages:
- **NuGet:** `LicenseSeat` for .NET/Godot
- **Unity:** `com.licenseseat.sdk` for Unity via UPM

#### Release Steps

1. **Update versions:**
   ```bash
   # src/LicenseSeat/LicenseSeat.csproj
   <Version>1.0.0</Version>

   # src/LicenseSeat.Unity/package.json
   "version": "1.0.0"

   # src/LicenseSeat.Unity/CHANGELOG.md
   ## [1.0.0] - YYYY-MM-DD
   ```

2. **Validate:**
   ```bash
   ./scripts/validate-unity-sync.sh
   ./scripts/validate-unity-package.sh
   ```

3. **Tag and release:**
   ```bash
   git add -A && git commit -m "Release v1.0.0"
   git tag v1.0.0 && git push origin main v1.0.0
   gh release create v1.0.0 --title "v1.0.0" --generate-notes
   ```

4. **Automatic:** CI publishes to NuGet. Unity is available via Git tag:
   ```
   https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity#v1.0.0
   ```

#### OpenUPM (One-Time)

1. Go to [openupm.com/packages/add](https://openupm.com/packages/add/)
2. Submit: `https://github.com/licenseseat/licenseseat-csharp`
3. OpenUPM auto-detects `src/LicenseSeat.Unity/package.json`

After approval, new tags are automatically published.

#### NuGet Trusted Publishing

This repo uses [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) (OIDC, no API keys).

Setup:
1. Add `NUGET_USER` variable in repo settings
2. Create `nuget-publish` environment
3. Configure trusted publishing policy on nuget.org

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing`
3. Commit changes: `git commit -m 'Add amazing feature'`
4. Push: `git push origin feature/amazing`
5. Open a Pull Request

## License

MIT - see [LICENSE](LICENSE)
