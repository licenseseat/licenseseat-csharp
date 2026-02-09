using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat;

/// <summary>
/// Client for interacting with the LicenseSeat API.
/// Provides license activation, validation, deactivation, and entitlement checking.
/// </summary>
/// <example>
/// <code>
/// // Create client with API key and product slug
/// var client = new LicenseSeatClient(new LicenseSeatClientOptions("your-api-key", "your-product"));
///
/// // Activate a license
/// var license = await client.ActivateAsync("LICENSE-KEY");
///
/// // Check entitlements
/// if (client.HasEntitlement("pro-features"))
/// {
///     // Enable pro features
/// }
/// </code>
/// </example>
public sealed class LicenseSeatClient : ILicenseSeatClient
{
    /// <summary>
    /// The current SDK version.
    /// </summary>
    public const string SdkVersion = "0.4.0";

    private readonly LicenseSeatClientOptions _options;
    private readonly ApiClient _apiClient;
    private readonly LicenseCache _cache;
    private readonly EventBus _eventBus;
    private readonly object _lock = new object();

    private Timer? _validationTimer;
    private Timer? _heartbeatTimer;
#pragma warning disable CS0414, CA1805 // Field reserved for future connectivity polling
    private Timer? _connectivityTimer = null;
#pragma warning restore CS0414, CA1805
    private Timer? _offlineRefreshTimer;
    private string? _currentAutoLicenseKey;
    private bool _isOnline = true;
    private bool _disposed;

    /// <summary>
    /// Gets the event bus for subscribing to SDK events.
    /// </summary>
    public EventBus Events => _eventBus;

    /// <summary>
    /// Gets a value indicating whether the client is currently online.
    /// </summary>
    public bool IsOnline => _isOnline;

    /// <summary>
    /// Gets the current configuration options.
    /// </summary>
    public LicenseSeatClientOptions Options => _options;

    /// <summary>
    /// Creates a new LicenseSeat client with the specified options.
    /// </summary>
    /// <param name="options">Client configuration options.</param>
    public LicenseSeatClient(LicenseSeatClientOptions options)
    {
        _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        // Pass user-provided telemetry overrides
        TelemetryPayload.UserAppVersion = _options.AppVersion;
        TelemetryPayload.UserAppBuild = _options.AppBuild;

        _eventBus = new EventBus();
        _cache = new LicenseCache(_options.StoragePrefix);

        // Use custom HTTP adapter if provided, otherwise use default
        _apiClient = _options.HttpClientAdapter != null
            ? new ApiClient(_options, _options.HttpClientAdapter)
            : new ApiClient(_options);

        // Forward network status from API client
        _apiClient.OnNetworkStatusChange += HandleNetworkStatusChange;

        if (_options.AutoInitialize)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Creates a new LicenseSeat client with the specified API key and product slug.
    /// </summary>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <param name="productSlug">The product slug for API requests.</param>
    public LicenseSeatClient(string apiKey, string productSlug)
        : this(new LicenseSeatClientOptions(apiKey, productSlug))
    {
    }

    /// <summary>
    /// Creates a new LicenseSeat client with a custom HTTP client adapter (for testing).
    /// </summary>
    /// <param name="options">Client configuration options.</param>
    /// <param name="httpClient">Custom HTTP client adapter.</param>
    internal LicenseSeatClient(LicenseSeatClientOptions options, IHttpClientAdapter httpClient)
    {
        _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();

        // Pass user-provided telemetry overrides
        TelemetryPayload.UserAppVersion = _options.AppVersion;
        TelemetryPayload.UserAppBuild = _options.AppBuild;

        _eventBus = new EventBus();
        _cache = new LicenseCache(_options.StoragePrefix);
        _apiClient = new ApiClient(_options, httpClient);

        _apiClient.OnNetworkStatusChange += HandleNetworkStatusChange;

        if (_options.AutoInitialize)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Initializes the SDK by loading cached licenses and starting auto-validation.
    /// Called automatically unless <see cref="LicenseSeatClientOptions.AutoInitialize"/> is false.
    /// </summary>
    public void Initialize()
    {
        Log("LicenseSeat SDK initialized");

        var cachedLicense = _cache.GetLicense();
        if (cachedLicense != null)
        {
            _eventBus.Emit(LicenseSeatEvents.LicenseLoaded, cachedLicense);

            // Start auto-validation and heartbeat if API key is configured
            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                StartAutoValidation(cachedLicense.Key);
                StartHeartbeat(cachedLicense.Key);

                // Validate in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ValidateAsync(cachedLicense.Key).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log($"Background validation failed: {ex.Message}");

                        if (ex is ApiException apiEx && (apiEx.StatusCode == 401 || apiEx.StatusCode == 501))
                        {
                            _eventBus.Emit(LicenseSeatEvents.ValidationAuthFailed, new
                            {
                                LicenseKey = cachedLicense.Key,
                                Error = ex,
                                Cached = true
                            });
                        }
                    }
                });
            }
        }
    }

    /// <summary>
    /// Activates a license for this device.
    /// </summary>
    /// <param name="licenseKey">The license key to activate.</param>
    /// <param name="options">Optional activation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activated license.</returns>
    /// <exception cref="ApiException">When the API request fails.</exception>
    public async Task<License> ActivateAsync(
        string licenseKey,
        ActivationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new ArgumentException("License key cannot be empty", nameof(licenseKey));
        }

        options ??= new ActivationOptions();
        var deviceId = options.DeviceId ?? _options.DeviceId ?? DeviceIdentifier.Generate();

        var request = new ActivationRequest
        {
            DeviceId = deviceId,
            DeviceName = options.DeviceName,
            Metadata = options.Metadata
        };

        _eventBus.Emit(LicenseSeatEvents.ActivationStart, new { LicenseKey = licenseKey, DeviceId = deviceId });

        try
        {
            var response = await _apiClient.PostAsync<ActivationRequest, ActivationResponse>(
                $"/products/{_options.ProductSlug}/licenses/{licenseKey}/activate",
                request,
                cancellationToken
            ).ConfigureAwait(false);

            // Create license from response
            var result = ActivationResult.FromResponse(response, deviceId);
            var license = result.License ?? new License
            {
                Key = licenseKey,
                DeviceId = deviceId,
                ActivatedAt = DateTimeOffset.UtcNow,
                LastValidated = DateTimeOffset.UtcNow,
                Status = "active"
            };

            license.LastValidated = DateTimeOffset.UtcNow;

            _cache.SetLicense(license);
            _cache.SetDeviceId(deviceId);
            _cache.UpdateValidation(new ValidationResult { Valid = true, Optimistic = true });

            // Start auto-validation and heartbeat
            StartAutoValidation(licenseKey);
            StartHeartbeat(licenseKey);

            // Sync offline assets in background
            _ = Task.Run(() => SyncOfflineAssetsAsync(licenseKey, CancellationToken.None), CancellationToken.None);

            _eventBus.Emit(LicenseSeatEvents.ActivationSuccess, license);
            return license;
        }
        catch (Exception ex)
        {
            _eventBus.Emit(LicenseSeatEvents.ActivationError, new { LicenseKey = licenseKey, Error = ex });
            throw;
        }
    }

    /// <summary>
    /// Validates a license.
    /// </summary>
    /// <param name="licenseKey">The license key to validate.</param>
    /// <param name="options">Optional validation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    /// <exception cref="ApiException">When the API request fails and offline fallback is not available.</exception>
    public async Task<ValidationResult> ValidateAsync(
        string licenseKey,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            throw new ArgumentException("License key cannot be empty", nameof(licenseKey));
        }

        options ??= new ValidationOptions();
        _eventBus.Emit(LicenseSeatEvents.ValidationStart, new { LicenseKey = licenseKey });

        try
        {
            var request = new ValidationRequest
            {
                DeviceId = options.DeviceId ?? _cache.GetDeviceId()
            };

            var response = await _apiClient.PostAsync<ValidationRequest, ValidationResponse>(
                $"/products/{_options.ProductSlug}/licenses/{licenseKey}/validate",
                request,
                cancellationToken
            ).ConfigureAwait(false);

            var result = new ValidationResult
            {
                Valid = response.Valid,
                Code = response.Code,
                Message = response.Message,
                Warnings = response.Warnings
            };

            // Convert LicenseData to License
            if (response.License != null)
            {
                result.License = License.FromLicenseData(response.License, request.DeviceId);
                result.ActiveEntitlements = result.License.ActiveEntitlements;
            }

            // Preserve cached entitlements if server response omits them
            var cachedLicense = _cache.GetLicense();
            if ((result.ActiveEntitlements == null || result.ActiveEntitlements.Count == 0) &&
                cachedLicense?.Validation?.ActiveEntitlements?.Count > 0)
            {
                result.ActiveEntitlements = cachedLicense.Validation.ActiveEntitlements;
            }

            // Update cache
            if (cachedLicense != null && cachedLicense.Key == licenseKey)
            {
                _cache.UpdateValidation(result);
            }

            if (result.Valid)
            {
                _eventBus.Emit(LicenseSeatEvents.ValidationSuccess, result);
                _cache.SetLastSeenTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            else
            {
                _eventBus.Emit(LicenseSeatEvents.ValidationFailed, result);
                StopAutoValidation();
                StopHeartbeat();
                _currentAutoLicenseKey = null;
            }

            return result;
        }
        catch (Exception ex)
        {
            _eventBus.Emit(LicenseSeatEvents.ValidationError, new { LicenseKey = licenseKey, Error = ex });

            // Check for offline fallback
            if (ShouldFallbackToOffline(ex))
            {
                var offlineResult = await VerifyCachedOfflineAsync().ConfigureAwait(false);

                var cachedLicense = _cache.GetLicense();
                if (cachedLicense != null && cachedLicense.Key == licenseKey)
                {
                    _cache.UpdateValidation(offlineResult);
                }

                if (offlineResult.Valid)
                {
                    _eventBus.Emit(LicenseSeatEvents.ValidationOfflineSuccess, offlineResult);
                    return offlineResult;
                }
                else
                {
                    _eventBus.Emit(LicenseSeatEvents.ValidationOfflineFailed, offlineResult);
                    StopAutoValidation();
                    StopHeartbeat();
                    _currentAutoLicenseKey = null;
                    return offlineResult;
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Deactivates the current license.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="LicenseException">When no active license is found.</exception>
    /// <exception cref="ApiException">When the API request fails.</exception>
    public async Task DeactivateAsync(CancellationToken cancellationToken = default)
    {
        var license = _cache.GetLicense();
        if (license == null)
        {
            throw LicenseException.NoActiveLicense();
        }

        _eventBus.Emit(LicenseSeatEvents.DeactivationStart, license);

        try
        {
            var request = new DeactivationRequest
            {
                DeviceId = license.DeviceId
            };

            await _apiClient.PostAsync<DeactivationRequest, DeactivationResponse>(
                $"/products/{_options.ProductSlug}/licenses/{license.Key}/deactivate",
                request,
                cancellationToken
            ).ConfigureAwait(false);

            CompleteDeactivation();
            _eventBus.Emit(LicenseSeatEvents.DeactivationSuccess, null);
        }
        catch (ApiException ex) when (ShouldTreatDeactivationAsSuccess(ex))
        {
            // License no longer exists on server - treat as successful deactivation
            CompleteDeactivation();
            _eventBus.Emit(LicenseSeatEvents.DeactivationSuccess, null);
        }
        catch (Exception ex)
        {
            _eventBus.Emit(LicenseSeatEvents.DeactivationError, new { Error = ex, License = license });
            throw;
        }
    }

    /// <summary>
    /// Gets the current license status.
    /// </summary>
    /// <returns>The current license status.</returns>
    public LicenseStatus GetStatus()
    {
        var license = _cache.GetLicense();
        if (license == null)
        {
            return LicenseStatus.Inactive();
        }

        var validation = license.Validation;
        if (validation == null)
        {
            return LicenseStatus.Pending();
        }

        if (!validation.Valid)
        {
            if (validation.Offline)
            {
                return LicenseStatus.OfflineInvalid(validation.Code ?? "License invalid (offline)");
            }
            return LicenseStatus.Invalid(validation.Message ?? "License invalid");
        }

        var details = new LicenseStatusDetails
        {
            Key = license.Key,
            DeviceId = license.DeviceId,
            ActivatedAt = license.ActivatedAt,
            LastValidated = license.LastValidated,
            Entitlements = validation.ActiveEntitlements ?? new System.Collections.Generic.List<Entitlement>()
        };

        if (validation.Offline)
        {
            return LicenseStatus.OfflineValid(details);
        }

        return LicenseStatus.Active(details);
    }

    /// <summary>
    /// Gets the current cached license.
    /// </summary>
    /// <returns>The cached license, or null if none.</returns>
    public License? GetCurrentLicense()
    {
        return _cache.GetLicense();
    }

    /// <summary>
    /// Checks if a specific entitlement is active.
    /// </summary>
    /// <param name="entitlementKey">The entitlement key to check.</param>
    /// <returns>The entitlement status.</returns>
    public EntitlementStatus CheckEntitlement(string entitlementKey)
    {
        var license = _cache.GetLicense();
        if (license == null || license.Validation == null)
        {
            return EntitlementStatus.NoLicense();
        }

        var entitlements = license.Validation.ActiveEntitlements ?? license.ActiveEntitlements;
        if (entitlements == null)
        {
            return EntitlementStatus.NotFound();
        }

        var entitlement = entitlements.Find(e => e.Key == entitlementKey);
        if (entitlement == null)
        {
            return EntitlementStatus.NotFound();
        }

        if (entitlement.IsExpired)
        {
            return EntitlementStatus.Expired(entitlement);
        }

        return EntitlementStatus.ActiveStatus(entitlement);
    }

    /// <summary>
    /// Checks if a specific entitlement is active (simple boolean version).
    /// </summary>
    /// <param name="entitlementKey">The entitlement key to check.</param>
    /// <returns>True if the entitlement is active, false otherwise.</returns>
    public bool HasEntitlement(string entitlementKey)
    {
        return CheckEntitlement(entitlementKey).Active;
    }

    /// <summary>
    /// Resets the SDK state and clears all cached data.
    /// </summary>
    public void Reset()
    {
        StopAutoValidation();
        StopHeartbeat();
        StopOfflineRefresh();
        _cache.Clear();
        _currentAutoLicenseKey = null;
        _eventBus.Emit(LicenseSeatEvents.SdkReset, null);
    }

    /// <summary>
    /// Purges any cached license and related offline assets without making a server call.
    /// Useful when responding to logout events or license revocation notifications.
    /// </summary>
    public void PurgeCachedLicense()
    {
        _cache.Clear();
        StopAutoValidation();
        StopHeartbeat();
        StopOfflineRefresh();
        _currentAutoLicenseKey = null;
        _eventBus.Emit(LicenseSeatEvents.SdkReset, null);
    }

    /// <summary>
    /// Tests API connectivity by calling the health endpoint.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the API is healthy.</returns>
    public async Task<bool> TestAuthAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw ConfigurationException.ApiKeyRequired();
        }

        _eventBus.Emit(LicenseSeatEvents.AuthTestStart, null);

        try
        {
            await _apiClient.GetAsync<HealthResponse>("/health", cancellationToken).ConfigureAwait(false);
            _eventBus.Emit(LicenseSeatEvents.AuthTestSuccess, null);
            return true;
        }
        catch (Exception ex)
        {
            _eventBus.Emit(LicenseSeatEvents.AuthTestError, new { Error = ex });
            throw;
        }
    }

    /// <summary>
    /// Sends a heartbeat for the current active license.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task HeartbeatAsync(CancellationToken cancellationToken = default)
    {
        var license = _cache.GetLicense();
        if (license == null)
        {
            Log("No active license for heartbeat");
            return;
        }

        var deviceId = _cache.GetDeviceId() ?? _options.DeviceId ?? DeviceIdentifier.Generate();
        var request = new HeartbeatRequest { DeviceId = deviceId };

        try
        {
            await _apiClient.PostAsync<HeartbeatRequest, HeartbeatResponse>(
                $"/products/{_options.ProductSlug}/licenses/{license.Key}/heartbeat",
                request,
                cancellationToken
            ).ConfigureAwait(false);

            _eventBus.Emit(LicenseSeatEvents.HeartbeatSuccess);
            Log("Heartbeat sent successfully");
        }
        catch (Exception ex)
        {
            _eventBus.Emit(LicenseSeatEvents.HeartbeatError, new { Error = ex });
            throw;
        }
    }

    // ============================================================
    // Auto-Validation
    // ============================================================

    private void StartAutoValidation(string licenseKey)
    {
        StopAutoValidation();

        if (_options.AutoValidateInterval <= TimeSpan.Zero)
        {
            return;
        }

        _currentAutoLicenseKey = licenseKey;

        _validationTimer = new Timer(
            _ => PerformAutoValidation(),
            null,
            _options.AutoValidateInterval,
            _options.AutoValidateInterval
        );

        _eventBus.Emit(LicenseSeatEvents.AutoValidationCycle, new
        {
            NextRunAt = DateTimeOffset.UtcNow.Add(_options.AutoValidateInterval)
        });
    }

    private void StopAutoValidation()
    {
        lock (_lock)
        {
            _validationTimer?.Dispose();
            _validationTimer = null;
        }

        _eventBus.Emit(LicenseSeatEvents.AutoValidationStopped, null);
    }

    private async void PerformAutoValidation()
    {
        var licenseKey = _currentAutoLicenseKey;
        if (string.IsNullOrEmpty(licenseKey))
        {
            return;
        }

        try
        {
            await ValidateAsync(licenseKey!).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Auto-validation failed: {ex.Message}");
            _eventBus.Emit(LicenseSeatEvents.ValidationAutoFailed, new { LicenseKey = licenseKey, Error = ex });
        }

        // Fire-and-forget heartbeat after validation
        try
        {
            await HeartbeatAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Heartbeat failed: {ex.Message}");
        }

        _eventBus.Emit(LicenseSeatEvents.AutoValidationCycle, new
        {
            NextRunAt = DateTimeOffset.UtcNow.Add(_options.AutoValidateInterval)
        });
    }

    // ============================================================
    // Heartbeat Timer
    // ============================================================

    private void StartHeartbeat(string licenseKey)
    {
        StopHeartbeat();

        if (_options.HeartbeatInterval <= TimeSpan.Zero)
        {
            return;
        }

        _heartbeatTimer = new Timer(
            _ => PerformHeartbeat(),
            null,
            _options.HeartbeatInterval,
            _options.HeartbeatInterval
        );
    }

    private void StopHeartbeat()
    {
        lock (_lock)
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }
    }

    private async void PerformHeartbeat()
    {
        try
        {
            await HeartbeatAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Heartbeat timer failed: {ex.Message}");
        }
    }

    // ============================================================
    // Offline Support
    // ============================================================

    private async Task SyncOfflineAssetsAsync(string licenseKey, CancellationToken cancellationToken)
    {
        try
        {
            var offlineToken = await GetOfflineTokenAsync(licenseKey, cancellationToken).ConfigureAwait(false);
            _cache.SetOfflineToken(offlineToken);

            var keyId = offlineToken.Signature?.KeyId ?? offlineToken.Token?.Kid;
            if (!string.IsNullOrEmpty(keyId))
            {
                var existingKey = _cache.GetPublicKey(keyId!);
                if (existingKey == null)
                {
                    var publicKey = await GetSigningKeyAsync(keyId!, cancellationToken).ConfigureAwait(false);
                    _cache.SetPublicKey(keyId!, publicKey);
                }
            }

            _eventBus.Emit(LicenseSeatEvents.OfflineLicenseReady, new
            {
                KeyId = keyId,
                ExpiresAt = offlineToken.Token?.Exp
            });
        }
        catch (Exception ex)
        {
            Log($"Failed to sync offline assets: {ex.Message}");
        }
    }

    private async Task<OfflineTokenResponse> GetOfflineTokenAsync(string licenseKey, CancellationToken cancellationToken)
    {
        _eventBus.Emit(LicenseSeatEvents.OfflineLicenseFetching, new { LicenseKey = licenseKey });

        try
        {
            var request = new OfflineTokenRequest
            {
                DeviceId = _cache.GetDeviceId()
            };

            var response = await _apiClient.PostAsync<OfflineTokenRequest, OfflineTokenResponse>(
                $"/products/{_options.ProductSlug}/licenses/{licenseKey}/offline_token",
                request,
                cancellationToken
            ).ConfigureAwait(false);

            _eventBus.Emit(LicenseSeatEvents.OfflineLicenseFetched, new { LicenseKey = licenseKey, Data = response });
            return response;
        }
        catch (Exception ex)
        {
            _eventBus.Emit(LicenseSeatEvents.OfflineLicenseFetchError, new { LicenseKey = licenseKey, Error = ex });
            throw;
        }
    }

    private async Task<string> GetSigningKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        var response = await _apiClient.GetAsync<SigningKeyResponse>(
            $"/signing_keys/{keyId}",
            cancellationToken
        ).ConfigureAwait(false);

        if (string.IsNullOrEmpty(response.PublicKey))
        {
            throw new CryptoException($"Public key not found for key_id: {keyId}", CryptoException.NoPublicKeyCode);
        }

        return response.PublicKey!;
    }

    private Task<ValidationResult> VerifyCachedOfflineAsync()
    {
        var offlineToken = _cache.GetOfflineToken();
        if (offlineToken == null)
        {
            return Task.FromResult(ValidationResult.OfflineResult(false, "no_offline_license"));
        }

        var cachedLicense = _cache.GetLicense();
        if (cachedLicense == null)
        {
            return Task.FromResult(ValidationResult.OfflineResult(false, "no_license"));
        }

        // Check license key match using constant-time comparison to prevent timing attacks
        var tokenLicenseKey = offlineToken.Token?.LicenseKey ?? string.Empty;
        if (!Ed25519Verifier.ConstantTimeEquals(tokenLicenseKey, cachedLicense.Key))
        {
            return Task.FromResult(ValidationResult.OfflineResult(false, "license_mismatch"));
        }

        var now = DateTimeOffset.UtcNow;
        var nowUnix = now.ToUnixTimeSeconds();

        // Check expiry (exp is Unix timestamp)
        if (offlineToken.Token?.Exp > 0)
        {
            if (offlineToken.Token.Exp < nowUnix)
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "expired"));
            }
        }
        else if (_options.MaxOfflineDays > 0)
        {
            // Grace period check when no explicit expiry is set
            var pivot = cachedLicense.LastValidated ?? cachedLicense.ActivatedAt ?? now;
            var ageInDays = (now - pivot).TotalDays;

            if (ageInDays > _options.MaxOfflineDays)
            {
                Log($"Offline grace period expired: {ageInDays:F1} days since last validation (max: {_options.MaxOfflineDays})");
                return Task.FromResult(ValidationResult.OfflineResult(false, "grace_period_expired"));
            }
        }

        // Check not-before (nbf is Unix timestamp)
        if (offlineToken.Token?.Nbf > 0 && offlineToken.Token.Nbf > nowUnix)
        {
            return Task.FromResult(ValidationResult.OfflineResult(false, "not_yet_valid"));
        }

        // Clock tamper detection
        var lastSeenTimestamp = _cache.GetLastSeenTimestamp();
        if (lastSeenTimestamp > 0)
        {
            var maxSkewSeconds = _options.MaxClockSkew.TotalSeconds;

            // If current time is significantly behind the last seen time, clock may have been tampered
            if (nowUnix + maxSkewSeconds < lastSeenTimestamp)
            {
                Log($"Possible clock tampering detected: now={nowUnix}, lastSeen={lastSeenTimestamp}, maxSkew={maxSkewSeconds}");
                return Task.FromResult(ValidationResult.OfflineResult(false, "clock_tamper"));
            }
        }

        // Ed25519 signature verification
        var keyId = offlineToken.Signature?.KeyId ?? offlineToken.Token?.Kid;
        if (!string.IsNullOrEmpty(keyId) && !string.IsNullOrEmpty(offlineToken.Signature?.Value) && !string.IsNullOrEmpty(offlineToken.Canonical))
        {
            var publicKey = _cache.GetPublicKey(keyId!);
            if (!string.IsNullOrEmpty(publicKey))
            {
                try
                {
                    var isValid = Ed25519Verifier.VerifyCanonical(publicKey!, offlineToken.Signature!.Value!, offlineToken.Canonical!);
                    if (!isValid)
                    {
                        Log("Offline token signature verification failed");
                        return Task.FromResult(ValidationResult.OfflineResult(false, "signature_invalid"));
                    }
                    Log("Offline token signature verified successfully");
                }
                catch (CryptoException ex)
                {
                    Log($"Crypto error during offline verification: {ex.Message}");
                    return Task.FromResult(ValidationResult.OfflineResult(false, ex.ErrorCode ?? "crypto_error"));
                }
            }
            else
            {
                // No public key cached - skip signature verification but log it
                Log($"No public key cached for key_id {keyId}, skipping signature verification");
            }
        }

        // Update last seen timestamp for clock tamper detection
        _cache.SetLastSeenTimestamp(nowUnix);

        // Parse entitlements from offline token
        var entitlements = ParseEntitlementsFromOfflineToken(offlineToken.Token);

        // All checks passed
        return Task.FromResult(ValidationResult.OfflineResult(true, entitlements: entitlements));
    }

    private static System.Collections.Generic.List<Entitlement>? ParseEntitlementsFromOfflineToken(OfflineToken? token)
    {
        if (token?.Entitlements == null || token.Entitlements.Count == 0)
        {
            return null;
        }

        var entitlements = new System.Collections.Generic.List<Entitlement>();
        foreach (var oe in token.Entitlements)
        {
            if (string.IsNullOrEmpty(oe.Key))
            {
                continue;
            }

            DateTimeOffset? expiresAt = null;
            if (oe.ExpiresAt.HasValue && oe.ExpiresAt.Value > 0)
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(oe.ExpiresAt.Value);
            }

            entitlements.Add(new Entitlement
            {
                Key = oe.Key!,
                ExpiresAt = expiresAt
            });
        }

        return entitlements.Count > 0 ? entitlements : null;
    }

    private void StopOfflineRefresh()
    {
        lock (_lock)
        {
            _offlineRefreshTimer?.Dispose();
            _offlineRefreshTimer = null;
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private void CompleteDeactivation()
    {
        _cache.ClearLicense();
        _cache.ClearOfflineToken();
        StopAutoValidation();
        StopHeartbeat();
        StopOfflineRefresh();
        _currentAutoLicenseKey = null;
    }

    private bool ShouldFallbackToOffline(Exception error)
    {
        switch (_options.OfflineFallbackMode)
        {
            case OfflineFallbackMode.Always:
                return true;

            case OfflineFallbackMode.NetworkOnly:
                if (error is ApiException apiError)
                {
                    return apiError.IsNetworkError || apiError.IsServerError;
                }
                return true; // Network-level errors

            case OfflineFallbackMode.Disabled:
            default:
                return false;
        }
    }

    private static bool ShouldTreatDeactivationAsSuccess(ApiException error)
    {
        switch (error.StatusCode)
        {
            case 404: // Not found
            case 410: // Gone
                return true;

            case 422: // Unprocessable
                var code = error.Code?.ToLowerInvariant();
                if (code != null)
                {
                    return code == "revoked" ||
                           code == "already_deactivated" ||
                           code == "not_active" ||
                           code == "not_found" ||
                           code == "suspended" ||
                           code == "expired";
                }
                var message = error.Message.ToLowerInvariant();
                return message.Contains("revoked") ||
                       message.Contains("not found") ||
                       message.Contains("already") ||
                       message.Contains("suspended") ||
                       message.Contains("expired");

            default:
                return false;
        }
    }

    private void HandleNetworkStatusChange(bool isOnline)
    {
        var wasOnline = _isOnline;
        _isOnline = isOnline;

        if (!wasOnline && isOnline)
        {
            _eventBus.Emit(LicenseSeatEvents.NetworkOnline, null);

            // Restart auto-validation and heartbeat if we have a license
            if (!string.IsNullOrEmpty(_currentAutoLicenseKey))
            {
                if (_validationTimer == null)
                {
                    StartAutoValidation(_currentAutoLicenseKey!);
                }
                if (_heartbeatTimer == null)
                {
                    StartHeartbeat(_currentAutoLicenseKey!);
                }
            }

            // Sync offline assets
            var license = _cache.GetLicense();
            if (license != null)
            {
                _ = Task.Run(() => SyncOfflineAssetsAsync(license.Key, CancellationToken.None));
            }
        }
        else if (wasOnline && !isOnline)
        {
            _eventBus.Emit(LicenseSeatEvents.NetworkOffline, null);
            StopAutoValidation();
            StopHeartbeat();
        }
    }

    private void Log(string message)
    {
        if (_options.Debug)
        {
#if UNITY_5_3_OR_NEWER
            UnityEngine.Debug.Log($"[LicenseSeat SDK] {message}");
#else
            System.Diagnostics.Debug.WriteLine($"[LicenseSeat SDK] {message}");
#endif
        }
    }

    #region Synchronous Wrappers

    /// <inheritdoc/>
    public License Activate(string licenseKey, ActivationOptions? options = null)
    {
        return RunSync(() => ActivateAsync(licenseKey, options, CancellationToken.None));
    }

    /// <inheritdoc/>
    public ValidationResult Validate(string licenseKey, ValidationOptions? options = null)
    {
        return RunSync(() => ValidateAsync(licenseKey, options, CancellationToken.None));
    }

    /// <inheritdoc/>
    public void Deactivate()
    {
        RunSync(() => DeactivateAsync(CancellationToken.None));
    }

    /// <inheritdoc/>
    public void Heartbeat()
    {
        RunSync(() => HeartbeatAsync(CancellationToken.None));
    }

    /// <inheritdoc/>
    public bool TestAuth()
    {
        return RunSync(() => TestAuthAsync(CancellationToken.None));
    }

    /// <summary>
    /// Runs an async operation synchronously without risking deadlocks.
    /// Uses Task.Run to schedule on thread pool, avoiding SynchronizationContext issues.
    /// </summary>
    private static T RunSync<T>(Func<Task<T>> asyncFunc)
    {
        return Task.Run(asyncFunc).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Runs an async operation synchronously without risking deadlocks.
    /// Uses Task.Run to schedule on thread pool, avoiding SynchronizationContext issues.
    /// </summary>
    private static void RunSync(Func<Task> asyncFunc)
    {
        Task.Run(asyncFunc).GetAwaiter().GetResult();
    }

    #endregion

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopAutoValidation();
        StopHeartbeat();
        StopOfflineRefresh();
        _connectivityTimer?.Dispose();
        _apiClient.Dispose();

        _eventBus.Emit(LicenseSeatEvents.SdkDestroyed, null);
        _eventBus.Clear();
    }
}
