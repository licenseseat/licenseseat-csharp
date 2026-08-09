# Platform notes

The package targets Unity 2021.3+ with the .NET Standard 2.1 API profile. Mono,
IL2CPP, desktop, mobile, and WebGL are release targets—not a substitute for a
per-release build matrix. Qualify every platform/backend combination you claim
to support using the exact Unity LTS patch and stripping settings you ship.

## Managed dependencies and IL2CPP

The package contains no native libraries. It bundles six pinned managed
assemblies for JSON and Ed25519 verification. Their source, license notices, and
expected SHA-256 hashes are in `Runtime/Plugins`.

`link.xml` preserves the SDK, System.Text.Json, its encoder, and Bouncy Castle.
Before release, build a player with IL2CPP and the production stripping level,
then exercise activation, online validation, signed offline fallback,
entitlements, cancellation, and malformed-response rejection on the target.

## Device identity

The Unity settings integration hashes `SystemInfo.deviceUniqueIdentifier` when
the platform supplies a non-placeholder value. It otherwise falls back to the
core process fingerprint, which hashes machine name, user name, and OS version.
Either value can change with platform, privacy, account, or OS state. Applications
with stronger identity requirements should provide a privacy-reviewed,
installation-specific `DeviceId` through `LicenseSeatClientOptions`; do not use
a shared constant across installations.

## Storage and offline behavior

The built-in cache is memory-only on every platform. It does not write to
Application.persistentDataPath, IndexedDB, localStorage, PlayerPrefs, Keychain,
or Keystore. Consequently, signed offline fallback works only after the process
has fetched and verified the necessary license token and public key online; it
does not survive a restart.

Do not add plaintext persistence for license keys, offline tokens, public-key
pins, or last-seen timestamps. A future persistence provider should use
platform-protected storage, integrity protection, atomic writes, strict bounds,
rollback defenses, and versioned data migration.

## Android and iOS

- Use HTTPS. The SDK rejects cleartext endpoints unless a developer explicitly
  opts into insecure HTTP.
- Unity normally adds the Internet permission for networked Android players;
  verify the final merged manifest rather than adding broad permissions blindly.
- Do not weaken Android Network Security Configuration or iOS App Transport
  Security for production.
- Run real device IL2CPP tests; Editor/Mono success does not validate AOT,
  stripping, platform TLS, or lifecycle behavior.

## WebGL

UnityWebRequest runs through the browser networking stack. The service must allow
the exact player origin, `Authorization` and `Content-Type` headers, and the
required methods. Avoid wildcard origins, especially with credentials.

Browser reachability is only a UI hint. Authorization decisions must come from
`ValidateAsync`; never accept a locally cached `License` object merely because
the browser appears offline.

The default fingerprint may not be installation-stable in a browser privacy
environment. Prefer account-oriented licensing or inject an installation ID
from a carefully designed, privacy-reviewed storage strategy.

## Desktop and consoles

Desktop Mono/IL2CPP builds require the same qualification as other targets.
Console platforms additionally require testing with the platform holder's Unity
toolchain and network/security requirements; this package makes no automatic
console certification claim.
