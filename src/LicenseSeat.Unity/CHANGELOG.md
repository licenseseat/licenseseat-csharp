# Changelog

All notable changes to the LicenseSeat Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-01-20

### Added

- Initial release of LicenseSeat Unity SDK
- Pure C# implementation with no native dependencies
- Support for all Unity platforms (Windows, macOS, Linux, Android, iOS, WebGL)
- IL2CPP compatibility with comprehensive `link.xml`
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

- Removed non-existent `ProductId` from `ToClientOptions()` (API uses `product_slug` per-call)
- Enhanced `link.xml` with all model types for IL2CPP compatibility
