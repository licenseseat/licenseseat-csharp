# Changelog

All notable changes to the LicenseSeat C# SDK will be documented in this file.

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
