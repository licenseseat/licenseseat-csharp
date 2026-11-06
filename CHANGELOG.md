# Changelog

All notable changes to the LicenseSeat C# SDK will be documented in this file.

## [0.6.0] - 2026-11-06

### Security

- Offline tokens with a zero-width validity window (`nbf == exp`) are
  rejected; the clock-skew tolerance on `nbf` made such a never-valid grant
  usable for up to five minutes (#39).

### Added

- Persistent offline licensing (#25). `LicenseSeatClientOptions.LicenseStore`
  takes an `ILicenseStore`; `FileLicenseStore.ForApplication("YourApp")` stores
  the activation, device fingerprint, signed offline token and clock-rollback
  floor under the user's local (non-roaming) application data folder with
  atomic writes. A restarted app now resumes its session (`LicenseLoaded`) and,
  offline, validates against the persisted token for up to `MaxOfflineDays`,
  never beyond the token's `exp` or the license's expiry.
- `SigningPublicKey` / `SigningKeyId` options pin the organization's Ed25519
  key in the application. Signing keys are never persisted, so offline
  verification after a restart requires the pin; the pinned key is also used
  instead of fetching one.
- A restored session refreshes its offline token after its first successful
  online validation.

### Fixed

- Clock-rollback detection now works across restarts when a store is
  configured (the last-seen floor is persisted and only moves forward).
- Device fingerprint drift on Windows updates: the fingerprint an install
  activated with is persisted with the OS version it was derived from and
  reused for re-activation, validation and heartbeats, so an OS build change
  no longer orphans the activation or consumes a seat. On load it must
  re-derive from this machine's name and user name, so a copied or edited
  store cannot adopt another machine's fingerprint. The default derivation is
  unchanged (pinned by a golden test), so upgrading from 0.5.x presents the
  same fingerprint. An unreadable device record blocks activation
  (`device_identity_unavailable`) instead of silently creating a new device.
- An authoritative online rejection (`valid: false`) of the cached license now
  discards its offline token, so it cannot authorize offline use after a
  revocation. Rejecting an unrelated key leaves the cached license alone.
- Offline fallback is bound to the requested license key and device, not
  whatever the cache holds.
- `GetStatus()` / `HasEntitlement()` stop reporting offline access once the
  verified token's window (exp, license expiry, `MaxOfflineDays`) closes.
- Successful online validation refreshes a stale offline token (older than
  `OfflineLicenseRefreshInterval`, missing its signing key, or carrying a
  different license expiry), and a shorter server-reported license expiry
  bounds offline use of an older token.
- A background offline-token sync that started before a reset, deactivation,
  rejection or new activation can no longer write its token back.
- A malformed offline token (e.g. a null entitlement) fails closed instead of
  throwing.
- A token retired by an online rejection stays retired: a copy restored into
  the store is not loaded again (the rollback-floor record carries a
  retired-token watermark).
- The clock-rollback floor is merged atomically between cache instances in a
  process and re-read before each offline verification.
- Refresh also triggers when the token is past `exp` or close to the
  `MaxOfflineDays` grace limit; the server-reported license expiry is persisted
  with the license, and only the cached license's own validation updates it.
- A replacement activation's offline sync is no longer lost when an older
  in-flight sync is discarded.
- `FileLicenseStore` no longer treats an inaccessible file or directory as
  absent (which could activate a new device); it reports a read fault.

### Changed

- `Reset()` / `PurgeCachedLicense()` clear the license, offline token and
  signing keys but keep the device fingerprint and clock-rollback floor; they
  are not authority, and discarding them would let a later activation on the
  same machine consume a new seat.
- The memory-only statement from 0.5.0 now applies only when no
  `LicenseStore` is configured (the default).

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
