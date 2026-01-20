# Changelog

All notable changes to the LicenseSeat Unity SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-01-20

### Added

- Initial release of LicenseSeat Unity SDK
- Pure C# implementation with no native dependencies
- Support for all Unity platforms (Windows, macOS, Linux, Android, iOS, WebGL)
- IL2CPP compatibility with comprehensive `link.xml`
- `UnityWebRequestAdapter` for WebGL and cross-platform HTTP
- `LicenseSeatManager` MonoBehaviour for easy integration
- `LicenseSeatSettings` ScriptableObject for configuration
- Coroutine extensions for Unity-friendly async operations
- Editor window for testing and configuration
- Synchronous method wrappers for Editor scripts
- Offline license validation support
- Event system for reactive UI updates
- Sample scenes for basic usage and offline validation
