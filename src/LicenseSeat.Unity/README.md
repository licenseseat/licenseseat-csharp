# LicenseSeat Unity SDK

Managed C# licensing SDK for Unity. It provides license activation, validation,
entitlement checks, and signed offline fallback through UnityWebRequest.

## Compatibility and security notes

- Targets Unity 2021.3 or newer with the .NET Standard 2.1 API profile.
- Contains no native libraries. Its pinned managed JSON and cryptography
  dependencies are bundled in `Runtime/Plugins`; their versions, source,
  notices, and SHA-256 hashes are recorded alongside the binaries.
- Includes linker preservation metadata for Mono and IL2CPP. Every release must
  still be qualified in real Unity Editor and player builds for each supported
  platform and scripting backend.
- A settings asset embedded in a player can be inspected by an attacker. Put
  only a restricted LicenseSeat SDK credential with the `licenses:validate`
  scope in a client build—never an administrator or server credential.
- License keys are secrets. Do not write them to logs, analytics, crash reports,
  PlayerPrefs, or other plaintext storage.
- The built-in cache is memory-only. Signed offline fallback can cover a network
  outage after an online operation in the same process, but it does not survive
  an application restart. The SDK intentionally does not persist sensitive
  licensing state in PlayerPrefs.

## Installation

Pin a reviewed release or commit in production. To install from a Git tag, add
the package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.licenseseat.sdk": "https://github.com/licenseseat/licenseseat-csharp.git?path=src/LicenseSeat.Unity#v0.4.0"
  }
}
```

Or select **Window > Package Manager > + > Add package from git URL** and enter
the same URL. If the package is published in your OpenUPM registry, it can also
be installed with `openupm add com.licenseseat.sdk`.

## Quick start

1. Select **Create > LicenseSeat > Settings** in the Project window.
2. Configure the restricted SDK credential and product slug.
3. Add `LicenseSeatManager` to a GameObject and assign the settings asset.
4. Activate and validate through the manager:

```csharp
using System;
using LicenseSeat;
using UnityEngine;

public sealed class LicenseController : MonoBehaviour
{
    [SerializeField] private LicenseSeatManager manager;

    public void Activate(string licenseKey)
    {
        StartCoroutine(manager.ActivateCoroutine(licenseKey, OnActivated));
    }

    private void OnActivated(License license, Exception error)
    {
        if (error != null)
        {
            Debug.LogError("License activation failed.");
            return;
        }

        // Never log license.Key.
        Debug.Log("License activated.");
    }
}
```

Check entitlements only after successful validation:

```csharp
if (manager.Client.HasEntitlement("premium-features"))
{
    EnablePremiumFeatures();
}
```

## Offline fallback

Offline authorization is fail-closed and accepts only a signed token bound to
the requested license, product, and device. It is attempted only for a genuine
transport failure; an HTTP response from the service—including 4xx, 5xx, and
408—remains authoritative.

Configure it in the settings asset, or directly:

```csharp
var options = new LicenseSeatClientOptions("restricted-sdk-key", "product-slug")
{
    OfflineFallbackMode = OfflineFallbackMode.NetworkOnly,
    MaxOfflineDays = 7
};
```

`OfflineFallbackMode.Always` is retained as a compatibility alias for
`NetworkOnly`; it does not override server denials or local security failures.

## WebGL and CORS

The adapter uses UnityWebRequest and enforces HTTPS by default, bounded request
and response bodies, scoped authorization, strict media types, and no redirects.
WebGL additionally depends on the browser's CORS policy. Configure only the
origins and headers your application requires; do not enable credentialed
wildcard CORS.

## Core synchronization

The package contains copies of the core C# source under `Runtime/Core`. After a
core change, run:

```bash
./scripts/sync-unity-core.sh --replace-symlinks
./scripts/validate-unity-sync.sh
./scripts/validate-unity-package.sh
```

## Documentation

- [Installation guide](Documentation~/installation.md)
- [Quick start](Documentation~/quickstart.md)
- [Troubleshooting](Documentation~/troubleshooting.md)
- [Platform notes](Documentation~/platform-notes.md)

## License

LicenseSeat is distributed under the MIT License. Bundled dependency notices are
in `Runtime/Plugins/ThirdPartyNotices.md` and
`Runtime/Plugins/DOTNET-THIRD-PARTY-NOTICES.txt`.
