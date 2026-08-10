# Changelog

All notable changes to the LicenseSeat Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2026-08-10

### Security

- Moved license keys out of request URLs and into bounded JSON request bodies.
- Restricted credentials to the configured HTTPS origin and API base path;
  redirects, cookies, and oversized or malformed responses are rejected.
- Removed the public caller-supplied `HttpClient` constructor because an opaque
  auto-redirect handler can forward a bearer credential before post-response
  redirect checks run; custom transports remain an explicit adapter trust boundary.
- Made authoritative HTTP responses ineligible for offline fallback, including
  HTTP 408 and all 4xx/5xx responses.
- Made signed offline validation fail closed and bound signed claims to the
  license key, product slug, device fingerprint, key identifier, and timestamps.
- Added strict JSON, UTF-8, identifier, URL, and response-schema validation.
- Added lifecycle cancellation, non-overlapping background work, defensive cache
  copies, bounded telemetry, and secret-safe diagnostic logging.
- Replaced the Unity transport with a bounded UnityWebRequest implementation that
  applies the same origin, path, credential, redirect, content, and size policy.
- Bundled pinned managed dependencies from signature-verified NuGet packages with
  explicit assembly references, SHA-256 hashes, and redistribution notices.

### Changed

- Product scope is now configured once through
  `LicenseSeatClientOptions.ProductSlug`; the serialized `ProductId` field remains
  only as an obsolete compatibility alias.
- The built-in license and offline-token cache is explicitly memory-only.
- Unity device binding now hashes `SystemInfo.deviceUniqueIdentifier` when the
  platform exposes a usable value, with the core process fingerprint as fallback.
- The legacy serialized `ValidateOnStart` flag is retained for asset compatibility
  but hidden because there is no persisted license to validate after a restart.
- Updated the package and documentation to require Unity's .NET Standard 2.1
  profile and to distinguish target compatibility from tested build coverage.
- Updated System.Text.Json to 10.0.10 and BouncyCastle.Cryptography to 2.6.2.

### Testing

- Added adversarial tests for URL confusion, redirects, response limits,
  malformed payloads, identity mismatches, cancellation, disposal, time bounds,
  offline signature verification, and secret redaction.

## [0.3.0] - 2026-01-22

### Breaking Changes

- **API v1 Compatibility**: Updated SDK to use new LicenseSeat API v1 endpoint structure
- **ProductSlug Required**: `ProductSlug` is now a required configuration option
- **Field Renames**:
  - `device_identifier` → `device_id`
  - `license_key` → `key`
  - `ends_at` → `expires_at`
  - `active_activations_count` → `active_seats`
  - `reason_code` → `code`
  - `reason` → `message`
- **URL Structure**: Product-scoped endpoints were introduced. License keys are
  sent in request bodies as of 0.5.0.
- **Underscore URLs**: API endpoints use underscores (`/offline_token`, `/signing_keys/`) instead of hyphens

### Added

- `Activation` class in `ValidationResult` for device-specific validation info
- `ValidationWarning` model for API warnings
- Public `Ed25519Verifier` for external offline token verification
- Comprehensive integration test suite with 48 tests

### Fixed

- Unix timestamp units mismatch in clock tamper detection (was milliseconds, now seconds)
- Offline token response model alignment with new API structure

## [0.2.0] - 2026-01-20

### Added

- Initial release of LicenseSeat Unity SDK
- Managed C# implementation with no native libraries
- Target support for Windows, macOS, Linux, Android, iOS, and WebGL
- IL2CPP preservation metadata in `link.xml`
- `LicenseSeatLinkerProcessor` - automatic link.xml injection for UPM packages via `IUnityLinkerProcessor`
- `UnityWebRequestAdapter` for WebGL and cross-platform HTTP
- `LicenseSeatManager` MonoBehaviour for easy integration
- `LicenseSeatSettings` ScriptableObject for configuration
- `CreateValidationOptions()` method for API-compliant product slug handling
- Coroutine extensions for Unity-friendly async operations
- Editor window for testing and configuration
- Synchronous method wrappers for Editor scripts
- Offline license validation support
- Event system for reactive UI updates
- Sample scenes for basic usage and offline validation
- Comprehensive `UnityCompatibilityTests` for PRD compliance

### Fixed

- Corrected API base URL from `api.licenseseat.com` to `licenseseat.com/api`
- Fixed `IsValid` property access (was incorrectly called as method)
- Fixed `OfflineFallbackMode` enum usage in tests
- Added missing namespace imports across editor and runtime files
- Added `#if UNITY_5_3_OR_NEWER` guard to `TaskExtensions.cs`

### Changed

- Began migration away from the legacy `ProductId` naming
- Enhanced `link.xml` with all model types for IL2CPP compatibility
