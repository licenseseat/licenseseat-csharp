# Changelog

All notable changes to the LicenseSeat C# SDK will be documented in this file.

## [0.5.0] - 2026-08-10

### Security

- Moved license keys out of lifecycle request URLs and into bounded JSON request
  bodies on the product-scoped API routes.
- Restricted authenticated traffic to the configured HTTPS origin and API base
  path. Redirects, cookies, unsafe header overrides, ambiguous URLs, and
  oversized or malformed request/response bodies are rejected.
- Made authoritative HTTP responses ineligible for offline fallback, including
  HTTP 408 and all 4xx/5xx responses.
- Added strict bounded JSON and UTF-8 handling, duplicate-property rejection,
  identifier and timestamp validation, and exact response binding to the
  requested license, product, device, activation, and status.
- Hardened signed offline authorization across canonical payload equality,
  Ed25519 signature/key encoding, key identity, license/product/device claims,
  entitlement bounds, time ordering, local grace policy, and clock rollback.
- Added cancellation and generation checks so stale lifecycle or background work
  cannot restore authority after deactivation, reset, disposal, or a newer
  result.
- Added defensive cache copies, bounded telemetry and diagnostic redaction, and
  pinned/verified dependency inputs for the Unity distribution.

### Changed

- Product scope is configured once through
  `LicenseSeatClientOptions.ProductSlug`; the serialized `ProductId` property is
  retained only as an obsolete compatibility alias.
- Removed the public caller-supplied `HttpClient` constructor. Custom transports
  remain available through the explicit adapter boundary, where the caller owns
  equivalent pre-redirect credential protections.
- The built-in license/offline cache is explicitly memory-only and does not grant
  authority across process restarts.
- `MaxOfflineDays = 0` consistently disables offline authority instead of acting
  as an unlimited window.
- Unity now requires the .NET Standard 2.1 profile; its transport applies the
  same origin, redirect, credential, content, and size policy as the core SDK.
- Updated System.Text.Json to 10.0.10 and BouncyCastle.Cryptography to 2.6.2.

### Testing

- Added adversarial coverage for URL/origin confusion, redirects, response
  framing and limits, strict JSON, identity mismatches, cancellation, disposal,
  time bounds, offline signature substitution, cache isolation, and secret
  redaction.
- Added deterministic NuGet validation, Unity/core synchronization checks,
  compatibility builds, enforced coverage floors, vulnerability scanning, and
  a trusted-publishing release pipeline.

## [0.4.0] - 2026-02-09

### Added

- **Heartbeat support**: Periodic liveness pings sent to the server to power "last seen" tracking in the dashboard.
  - `HeartbeatAsync()` / `Heartbeat()` methods for manual heartbeat sends.
  - `HeartbeatInterval` option (default: 5 minutes) for automatic background heartbeats.
  - A heartbeat is also sent after each auto-validation cycle.
  - `HeartbeatSuccess` and `HeartbeatError` events.
- **Enriched telemetry**: Device and environment data sent with API requests now includes:
  - `sdk_name` (always "csharp")
  - `device_type` (desktop, server, mobile)
  - `architecture` (x64, arm64, etc.)
  - `cpu_cores` (processor count)
  - `memory_gb` (total system memory)
  - `language` (two-letter ISO language code)
  - `runtime_version` (.NET runtime description)
- **App version configuration**: `AppVersion` and `AppBuild` options to tag telemetry with your application version. Auto-detected from assembly metadata when not set.
- **Synchronous wrappers**: `Heartbeat()` sync method alongside existing sync wrappers.

### Changed

- Auto-validation now sends a heartbeat after each validation cycle.
- Heartbeat and auto-validation timers are stopped on validation failure, deactivation, or network loss, and restarted when connectivity is restored.

## [0.3.0] - 2025-12-20

### Added

- Initial release with API v1 compatibility.
- License activation, validation, and deactivation.
- Entitlement checking with usage limits and expiration.
- Offline mode with Ed25519 signature verification.
- Auto-validation with configurable intervals.
- Event system for license lifecycle changes.
- ASP.NET Core dependency injection support.
- Unity SDK with IL2CPP, WebGL, iOS, and Android support.
