# Signed offline fallback sample

This sample demonstrates the SDK's fail-closed offline path. It never treats
network reachability, a cached `License`, an expiry field, or exception text as
proof of authorization. The only authorization result comes from
`ValidateAsync`, which verifies the server-signed offline token when a genuine
transport failure prevents online validation.

## Setup

1. Configure `LicenseSeatSettings` with:
   - a restricted SDK credential scoped to `licenses:validate`;
   - the product slug;
   - `OfflineFallbackMode.NetworkOnly`;
   - an appropriate `MaxOfflineDays` value.
2. Activate and validate once while online so the current process can fetch the
   signed offline token and its public key.
3. Attach `OfflineLicenseManager` and wire its text and validation-button fields.
4. Interrupt networking, then press Validate. A successful result explicitly
   reports `result.Offline == true`.

The built-in cache is memory-only. Restarting the application discards the
license, token, key, and clock state, so this sample does not demonstrate an
air-gapped restart. Do not add a PlayerPrefs fallback or manually accept the
cached license's local fields.

`OfflineFallbackMode.Always` is only a compatibility alias for network-only
fallback. Server responses and local protocol/security failures remain
authoritative and never fall back to cached authorization.
