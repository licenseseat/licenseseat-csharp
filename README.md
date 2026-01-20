# LicenseSeat C# SDK

[![CI](https://github.com/licenseseat/licenseseat-csharp/actions/workflows/ci.yml/badge.svg)](https://github.com/licenseseat/licenseseat-csharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/LicenseSeat.svg)](https://www.nuget.org/packages/LicenseSeat/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/LicenseSeat.svg)](https://www.nuget.org/packages/LicenseSeat/)
[![License](https://img.shields.io/github/license/licenseseat/licenseseat-csharp.svg)](LICENSE)

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

## Documentation

For full API documentation, visit [docs.licenseseat.com](https://docs.licenseseat.com).

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

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

MIT - see [LICENSE](LICENSE) for details.