#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat
{

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
        private static readonly JsonSerializerOptions OfflineJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            MaxDepth = SecurityValidation.MaxJsonDepth,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// The current SDK version.
        /// </summary>
        public const string SdkVersion = "0.4.0";

        private readonly LicenseSeatClientOptions _options;
        private readonly ApiClient _apiClient;
        private readonly LicenseCache _cache;
        private readonly EventBus _eventBus;
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _lifetimeCancellation = new CancellationTokenSource();
        private readonly CancellationToken _lifetimeToken;

        private Timer? _validationTimer;
        private Timer? _heartbeatTimer;
#pragma warning disable CS0414, CA1805 // Field reserved for future connectivity polling
        private Timer? _connectivityTimer = null;
#pragma warning restore CS0414, CA1805
        private Timer? _offlineRefreshTimer;
        private string? _currentAutoLicenseKey;
        private int _autoValidationRunning;
        private int _heartbeatRunning;
        private int _offlineSyncRunning;
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
        public LicenseSeatClientOptions Options => _options.Clone();

        /// <summary>
        /// Creates a new LicenseSeat client with the specified options.
        /// </summary>
        /// <param name="options">Client configuration options.</param>
        public LicenseSeatClient(LicenseSeatClientOptions options)
        {
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _lifetimeToken = _lifetimeCancellation.Token;

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
            _lifetimeToken = _lifetimeCancellation.Token;

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
            ThrowIfDisposed();
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
                    StartOfflineRefresh(cachedLicense.Key);

                    // Validate in background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ValidateAsync(cachedLicense.Key, cancellationToken: _lifetimeToken)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
                        {
                            // Client shutdown cancels background work.
                        }
                        catch (Exception)
                        {
                            Log("Background validation failed.");

                        }
                    }, _lifetimeToken);
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
            using var operationCancellation = CreateOperationCancellation(cancellationToken);
            var operationToken = operationCancellation.Token;

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                throw new ArgumentException("License key cannot be empty", nameof(licenseKey));
            }

            ValidateLicenseKey(licenseKey, nameof(licenseKey));

            options ??= new ActivationOptions();
            var deviceId = options.DeviceId ?? _options.DeviceId ?? DeviceIdentifier.Generate();
            ValidateFingerprint(deviceId, nameof(options));

            if (options.DeviceName != null && !SecurityValidation.IsSafeText(options.DeviceName, 1, 255))
            {
                throw new ArgumentException("Device name must be valid text no longer than 255 bytes.", nameof(options));
            }

            var request = new ActivationRequest
            {
                LicenseKey = licenseKey,
                Fingerprint = deviceId,
                DeviceName = options.DeviceName,
                Metadata = options.Metadata
            };

            _eventBus.Emit(LicenseSeatEvents.ActivationStart, new { LicenseKey = licenseKey, DeviceId = deviceId });

            try
            {
                var response = await _apiClient.PostAsync<ActivationRequest, ActivationResponse>(
                    $"/products/{EncodePathSegment(_options.ProductSlug!)}/licenses/activate",
                    request,
                    operationToken
                ).ConfigureAwait(false);

                operationToken.ThrowIfCancellationRequested();
                ValidateActivationResponse(response, licenseKey, deviceId);

                // Create license from response
                var result = ActivationResult.FromResponse(response, deviceId);
                var license = result.License!;

                license.LastValidated = DateTimeOffset.UtcNow;

                _cache.SetLicense(license);
                _cache.SetDeviceId(deviceId);
                _cache.UpdateValidation(new ValidationResult { Valid = true, Optimistic = true });

                // Start auto-validation and heartbeat
                StartAutoValidation(licenseKey);
                StartHeartbeat(licenseKey);
                StartOfflineRefresh(licenseKey);

                // Sync offline assets in background only when local policy
                // explicitly grants bounded offline authority.
                if (OfflinePolicyIsEnabled())
                {
                    _ = SyncOfflineAssetsAsync(licenseKey, _lifetimeToken);
                }

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
            using var operationCancellation = CreateOperationCancellation(cancellationToken);
            var operationToken = operationCancellation.Token;

            if (string.IsNullOrWhiteSpace(licenseKey))
            {
                throw new ArgumentException("License key cannot be empty", nameof(licenseKey));
            }

            ValidateLicenseKey(licenseKey, nameof(licenseKey));

            options ??= new ValidationOptions();
            _eventBus.Emit(LicenseSeatEvents.ValidationStart, new { LicenseKey = licenseKey });

            try
            {
                var fingerprint = options.DeviceId ?? _cache.GetDeviceId() ?? _options.DeviceId;
                if (fingerprint != null)
                {
                    ValidateFingerprint(fingerprint, nameof(options));
                }

                var request = new ValidationRequest
                {
                    LicenseKey = licenseKey,
                    Fingerprint = fingerprint
                };

                var response = await _apiClient.PostAsync<ValidationRequest, ValidationResponse>(
                    $"/products/{EncodePathSegment(_options.ProductSlug!)}/licenses/validate",
                    request,
                    operationToken,
                    retryable: true
                ).ConfigureAwait(false);

                operationToken.ThrowIfCancellationRequested();
                ValidateValidationResponse(response, licenseKey, fingerprint);

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
                    result.License = License.FromLicenseData(response.License, request.Fingerprint);
                    result.ActiveEntitlements = result.License.ActiveEntitlements;
                }

                var cachedLicense = _cache.GetLicense();

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
                    StopOfflineRefresh();
                    _currentAutoLicenseKey = null;
                }

                return result;
            }
            catch (Exception ex)
            {
                _eventBus.Emit(LicenseSeatEvents.ValidationError, new { LicenseKey = licenseKey, Error = ex });
                if (ex is ApiException apiError && (apiError.StatusCode == 401 || apiError.StatusCode == 403))
                {
                    _eventBus.Emit(LicenseSeatEvents.ValidationAuthFailed, new
                    {
                        LicenseKey = licenseKey,
                        Error = ex,
                        Cached = _cache.GetLicense() != null
                    });
                }

                // Check for offline fallback
                if (ShouldFallbackToOffline(ex))
                {
                    operationToken.ThrowIfCancellationRequested();
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
                        StopOfflineRefresh();
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
            using var operationCancellation = CreateOperationCancellation(cancellationToken);
            var operationToken = operationCancellation.Token;

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
                    LicenseKey = license.Key,
                    Fingerprint = license.DeviceId
                };

                ValidateLicenseKey(license.Key, "cached license key");
                ValidateFingerprint(license.DeviceId, "cached device fingerprint");

                var response = await _apiClient.PostAsync<DeactivationRequest, DeactivationResponse>(
                    $"/products/{EncodePathSegment(_options.ProductSlug!)}/licenses/deactivate",
                    request,
                    operationToken
                ).ConfigureAwait(false);

                operationToken.ThrowIfCancellationRequested();
                ValidateDeactivationResponse(response);

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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            return _cache.GetLicense();
        }

        /// <summary>
        /// Checks if a specific entitlement is active.
        /// </summary>
        /// <param name="entitlementKey">The entitlement key to check.</param>
        /// <returns>The entitlement status.</returns>
        public EntitlementStatus CheckEntitlement(string entitlementKey)
        {
            ThrowIfDisposed();
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
            ThrowIfDisposed();
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
            ThrowIfDisposed();
            _cache.Clear();
            StopAutoValidation();
            StopHeartbeat();
            StopOfflineRefresh();
            _currentAutoLicenseKey = null;
            _eventBus.Emit(LicenseSeatEvents.SdkReset, null);
        }

        /// <summary>
        /// Tests the configured API credential using a protected, side-effect-free endpoint.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the API is healthy.</returns>
        public async Task<bool> TestAuthAsync(CancellationToken cancellationToken = default)
        {
            using var operationCancellation = CreateOperationCancellation(cancellationToken);
            var operationToken = operationCancellation.Token;

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                throw ConfigurationException.ApiKeyRequired();
            }

            _eventBus.Emit(LicenseSeatEvents.AuthTestStart, null);

            try
            {
                var response = await _apiClient.GetAsync<AuthResponse>("/auth", operationToken).ConfigureAwait(false);
                operationToken.ThrowIfCancellationRequested();
                if (response.Object != "authentication" || !response.Authenticated || response.Scope != "licenses:validate")
                {
                    throw InvalidResponse("Authentication endpoint returned inconsistent data.");
                }

                RequireTimestamp(response.Timestamp, "authentication timestamp");
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
            using var operationCancellation = CreateOperationCancellation(cancellationToken);
            var operationToken = operationCancellation.Token;

            var license = _cache.GetLicense();
            if (license == null)
            {
                Log("No active license for heartbeat");
                return;
            }

            var deviceId = _cache.GetDeviceId() ?? _options.DeviceId ?? DeviceIdentifier.Generate();
            ValidateLicenseKey(license.Key, "cached license key");
            ValidateFingerprint(deviceId, "cached device fingerprint");
            var request = new HeartbeatRequest { LicenseKey = license.Key, Fingerprint = deviceId };

            try
            {
                var response = await _apiClient.PostAsync<HeartbeatRequest, HeartbeatResponse>(
                    $"/products/{EncodePathSegment(_options.ProductSlug!)}/licenses/heartbeat",
                    request,
                    operationToken
                ).ConfigureAwait(false);

                operationToken.ThrowIfCancellationRequested();
                ValidateHeartbeatResponse(response, license.Key);

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
            ThrowIfDisposed();
            StopAutoValidation();

            if (_options.AutoValidateInterval <= TimeSpan.Zero)
            {
                return;
            }

            _currentAutoLicenseKey = licenseKey;

            _validationTimer = new Timer(
                _ => _ = PerformAutoValidationAsync(),
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

        private async Task PerformAutoValidationAsync()
        {
            if (_lifetimeToken.IsCancellationRequested || Interlocked.Exchange(ref _autoValidationRunning, 1) != 0)
            {
                return;
            }

            var licenseKey = _currentAutoLicenseKey;
            if (string.IsNullOrEmpty(licenseKey))
            {
                Volatile.Write(ref _autoValidationRunning, 0);
                return;
            }

            try
            {
                await ValidateAsync(licenseKey!, cancellationToken: _lifetimeToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Log("Auto-validation failed.");
                _eventBus.Emit(LicenseSeatEvents.ValidationAutoFailed, new { LicenseKey = licenseKey, Error = ex });
            }

            finally
            {
                Volatile.Write(ref _autoValidationRunning, 0);
            }

            if (!_lifetimeToken.IsCancellationRequested)
            {
                _eventBus.Emit(LicenseSeatEvents.AutoValidationCycle, new
                {
                    NextRunAt = DateTimeOffset.UtcNow.Add(_options.AutoValidateInterval)
                });
            }
        }

        // ============================================================
        // Heartbeat Timer
        // ============================================================

        private void StartHeartbeat(string licenseKey)
        {
            ThrowIfDisposed();
            StopHeartbeat();

            if (_options.HeartbeatInterval <= TimeSpan.Zero)
            {
                return;
            }

            _heartbeatTimer = new Timer(
                _ => _ = PerformHeartbeatAsync(),
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

        private async Task PerformHeartbeatAsync()
        {
            if (_lifetimeToken.IsCancellationRequested || Interlocked.Exchange(ref _heartbeatRunning, 1) != 0)
            {
                return;
            }

            try
            {
                await HeartbeatAsync(_lifetimeToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
            {
                // Client shutdown cancels background work.
            }
            catch (Exception)
            {
                Log("Heartbeat timer failed.");
            }
            finally
            {
                Volatile.Write(ref _heartbeatRunning, 0);
            }
        }

        // ============================================================
        // Offline Support
        // ============================================================

        private async Task SyncOfflineAssetsAsync(string licenseKey, CancellationToken cancellationToken)
        {
            if (!OfflinePolicyIsEnabled() || cancellationToken.IsCancellationRequested ||
                Interlocked.Exchange(ref _offlineSyncRunning, 1) != 0)
            {
                return;
            }

            try
            {
                var offlineToken = await GetOfflineTokenAsync(licenseKey, cancellationToken).ConfigureAwait(false);
                var keyId = offlineToken.Signature!.KeyId!;
                var publicKey = _cache.GetPublicKey(keyId);
                var fetchedKey = false;
                if (publicKey == null)
                {
                    publicKey = await GetSigningKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
                    fetchedKey = true;
                }

                if (!Ed25519Verifier.VerifyCanonical(publicKey, offlineToken.Signature.Value!, offlineToken.Canonical!))
                {
                    throw CryptoException.SignatureInvalid();
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (fetchedKey)
                {
                    _cache.SetPublicKey(keyId, publicKey);
                }

                _cache.SetOfflineToken(offlineToken);

                _eventBus.Emit(LicenseSeatEvents.OfflineLicenseReady, new
                {
                    KeyId = keyId,
                    ExpiresAt = offlineToken.Token?.Exp
                });
            }
            catch (OperationCanceledException) when (_lifetimeToken.IsCancellationRequested)
            {
                // Client shutdown cancels background synchronization.
            }
            catch (Exception)
            {
                Log("Failed to sync offline assets.");
            }
            finally
            {
                Volatile.Write(ref _offlineSyncRunning, 0);
            }
        }

        private async Task<OfflineTokenResponse> GetOfflineTokenAsync(string licenseKey, CancellationToken cancellationToken)
        {
            _eventBus.Emit(LicenseSeatEvents.OfflineLicenseFetching, new { LicenseKey = licenseKey });

            try
            {
                var request = new OfflineTokenRequest
                {
                    LicenseKey = licenseKey,
                    Fingerprint = _cache.GetDeviceId()
                };

                ValidateLicenseKey(licenseKey, nameof(licenseKey));
                if (request.Fingerprint == null)
                {
                    throw InvalidResponse("Cannot request an offline token without an activated device fingerprint.");
                }

                ValidateFingerprint(request.Fingerprint, "cached device fingerprint");

                var response = await _apiClient.PostAsync<OfflineTokenRequest, OfflineTokenResponse>(
                    $"/products/{EncodePathSegment(_options.ProductSlug!)}/licenses/offline-token",
                    request,
                    cancellationToken
                ).ConfigureAwait(false);

                ValidateOfflineTokenEnvelope(response, licenseKey, request.Fingerprint);

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
            if (!SecurityValidation.IsSafeIdentifier(keyId, 1, 255, allowColon: true))
            {
                throw new CryptoException("Signing key ID is invalid.", CryptoException.InvalidKeyCode);
            }

            var response = await _apiClient.GetAsync<SigningKeyResponse>(
                $"/signing_keys/{EncodePathSegment(keyId)}",
                cancellationToken
            ).ConfigureAwait(false);

            if (response.Object != "signing_key" ||
                response.Algorithm != "Ed25519" ||
                response.Status != "active" ||
                !Ed25519Verifier.ConstantTimeEquals(response.KeyId, keyId) ||
                string.IsNullOrEmpty(response.PublicKey))
            {
                throw new CryptoException("Signing key response is invalid or does not match the requested key.", CryptoException.NoPublicKeyCode);
            }

            ValidatePublicKey(response.PublicKey!);

            return response.PublicKey!;
        }

        private Task<ValidationResult> VerifyCachedOfflineAsync()
        {
            if (!OfflinePolicyIsEnabled())
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "offline_disabled"));
            }

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

            var fingerprint = _cache.GetDeviceId();
            if (string.IsNullOrEmpty(fingerprint))
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "device_mismatch"));
            }

            try
            {
                ValidateOfflineTokenEnvelope(offlineToken, cachedLicense.Key, fingerprint!);
            }
            catch (CryptoException ex)
            {
                Log("Offline token structure validation failed.");
                return Task.FromResult(ValidationResult.OfflineResult(false, ex.ErrorCode ?? "token_invalid"));
            }

            var now = DateTimeOffset.UtcNow;
            var nowUnix = now.ToUnixTimeSeconds();
            var token = offlineToken.Token!;

            if (token.Exp <= nowUnix)
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "expired"));
            }

            var maxClockSkewSeconds = checked((long)_options.MaxClockSkew.TotalSeconds);
            if (token.Iat > nowUnix + maxClockSkewSeconds || token.Nbf > nowUnix + maxClockSkewSeconds)
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "not_yet_valid"));
            }

            if (token.LicenseExpiresAt.HasValue && token.LicenseExpiresAt.Value <= nowUnix)
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "license_expired"));
            }

            var maxOfflineSeconds = checked((long)_options.MaxOfflineDays * 24L * 60L * 60L);
            if (nowUnix - token.Iat >= maxOfflineSeconds)
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "grace_period_expired"));
            }

            var keyId = offlineToken.Signature!.KeyId!;
            var publicKey = _cache.GetPublicKey(keyId);
            if (string.IsNullOrEmpty(publicKey))
            {
                return Task.FromResult(ValidationResult.OfflineResult(false, "no_public_key"));
            }

            try
            {
                ValidatePublicKey(publicKey!);
                if (!Ed25519Verifier.VerifyCanonical(
                        publicKey!,
                        offlineToken.Signature.Value!,
                        offlineToken.Canonical!))
                {
                    return Task.FromResult(ValidationResult.OfflineResult(false, "signature_invalid"));
                }
            }
            catch (CryptoException ex)
            {
                Log("Cryptographic offline verification failed.");
                return Task.FromResult(ValidationResult.OfflineResult(false, ex.ErrorCode ?? "crypto_error"));
            }

            // Clock tamper detection
            var lastSeenTimestamp = _cache.GetLastSeenTimestamp();
            if (lastSeenTimestamp > 0)
            {
                // If current time is significantly behind the last seen time, clock may have been tampered
                if (nowUnix + maxClockSkewSeconds < lastSeenTimestamp)
                {
                    Log($"Possible clock tampering detected: now={nowUnix}, lastSeen={lastSeenTimestamp}, maxSkew={maxClockSkewSeconds}");
                    return Task.FromResult(ValidationResult.OfflineResult(false, "clock_tamper"));
                }
            }

            // Update last seen timestamp for clock tamper detection
            _cache.SetLastSeenTimestamp(nowUnix);

            // Parse entitlements from offline token
            var entitlements = ParseEntitlementsFromOfflineToken(token, nowUnix);

            // All checks passed
            return Task.FromResult(ValidationResult.OfflineResult(true, entitlements: entitlements));
        }

        private static List<Entitlement>? ParseEntitlementsFromOfflineToken(OfflineToken token, long nowUnix)
        {
            if (token.Entitlements == null || token.Entitlements.Count == 0)
            {
                return null;
            }

            var entitlements = new System.Collections.Generic.List<Entitlement>();
            foreach (var oe in token.Entitlements)
            {
                if (oe.ExpiresAt.HasValue && oe.ExpiresAt.Value <= nowUnix)
                {
                    continue;
                }

                DateTimeOffset? expiresAt = null;
                if (oe.ExpiresAt.HasValue)
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

        private void StartOfflineRefresh(string licenseKey)
        {
            lock (_lock)
            {
                _offlineRefreshTimer?.Dispose();
                _offlineRefreshTimer = null;

                if (_disposed || _lifetimeToken.IsCancellationRequested || !OfflinePolicyIsEnabled())
                {
                    return;
                }

                _offlineRefreshTimer = new Timer(
                    _ => _ = PerformOfflineRefreshAsync(licenseKey),
                    null,
                    _options.OfflineLicenseRefreshInterval,
                    _options.OfflineLicenseRefreshInterval
                );
            }
        }

        private async Task PerformOfflineRefreshAsync(string licenseKey)
        {
            await SyncOfflineAssetsAsync(licenseKey, _lifetimeToken).ConfigureAwait(false);
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void ValidateActivationResponse(ActivationResponse response, string licenseKey, string fingerprint)
        {
            if (response.Object != "activation" || response.License == null)
            {
                throw InvalidResponse("Activation response is missing its required object or license.");
            }

            if (!Ed25519Verifier.ConstantTimeEquals(response.LicenseKey, licenseKey) ||
                !Ed25519Verifier.ConstantTimeEquals(response.Fingerprint ?? response.DeviceId, fingerprint))
            {
                throw InvalidResponse("Activation response does not match the requested license and device.");
            }

            RequireTimestamp(response.ActivatedAt, "activation timestamp");
            ValidateLicenseData(response.License, licenseKey, requireActive: true);
        }

        private void ValidateValidationResponse(ValidationResponse response, string licenseKey, string? fingerprint)
        {
            if (response.Object != "validation_result" || response.License == null)
            {
                throw InvalidResponse("Validation response is missing its required object or license.");
            }

            ValidateLicenseData(response.License, licenseKey, requireActive: response.Valid);

            if (!response.Valid && !SecurityValidation.IsSafeIdentifier(response.Code, 1, 128))
            {
                throw InvalidResponse("Invalid validation responses must include a bounded error code.");
            }

            if (response.Message != null && !SecurityValidation.IsSafeText(response.Message, 1, 4096))
            {
                throw InvalidResponse("Validation response contains an invalid message.");
            }

            if (response.Warnings != null)
            {
                if (response.Warnings.Count > 100)
                {
                    throw InvalidResponse("Validation response contains too many warnings.");
                }

                foreach (var warning in response.Warnings)
                {
                    if (!SecurityValidation.IsSafeIdentifier(warning.Code, 1, 128) ||
                        !SecurityValidation.IsSafeText(warning.Message, 1, 4096))
                    {
                        throw InvalidResponse("Validation response contains an invalid warning.");
                    }
                }
            }

            if (response.Activation != null)
            {
                if (fingerprint == null ||
                    !Ed25519Verifier.ConstantTimeEquals(
                        response.Activation.Fingerprint ?? response.Activation.DeviceId,
                        fingerprint) ||
                    !Ed25519Verifier.ConstantTimeEquals(response.Activation.LicenseKey, licenseKey))
                {
                    throw InvalidResponse("Validation activation does not match the requested license and device.");
                }

                RequireTimestamp(response.Activation.ActivatedAt, "validation activation timestamp");
            }
        }

        private void ValidateDeactivationResponse(DeactivationResponse response)
        {
            if (response.Object != "deactivation")
            {
                throw InvalidResponse("Deactivation response has an invalid object type.");
            }

            RequireTimestamp(response.DeactivatedAt, "deactivation timestamp");
        }

        private void ValidateHeartbeatResponse(HeartbeatResponse response, string licenseKey)
        {
            if (response.Object != "heartbeat" || response.License == null || response.ReceivedAt == default)
            {
                throw InvalidResponse("Heartbeat response is incomplete.");
            }

            if (response.ReceivedAt > DateTimeOffset.UtcNow.Add(_options.MaxClockSkew))
            {
                throw InvalidResponse("Heartbeat response timestamp is unreasonably far in the future.");
            }

            ValidateLicenseData(response.License, licenseKey, requireActive: true);
        }

        private void ValidateLicenseData(LicenseData data, string expectedLicenseKey, bool requireActive)
        {
            if (data.Object != "license" ||
                !Ed25519Verifier.ConstantTimeEquals(data.Key, expectedLicenseKey) ||
                !SecurityValidation.IsSafeIdentifier(data.Status, 1, 32) ||
                !SecurityValidation.IsSafeIdentifier(data.Mode, 1, 64) ||
                !SecurityValidation.IsSafeIdentifier(data.PlanKey, 1, 255) ||
                data.Product?.Object != "product" ||
                !string.Equals(data.Product.Slug, _options.ProductSlug, StringComparison.Ordinal))
            {
                throw InvalidResponse("License response contains missing or inconsistent identity claims.");
            }

            switch (data.Status)
            {
                case "active":
                case "revoked":
                case "canceled":
                case "expired":
                case "pending":
                case "suspended":
                    break;
                default:
                    throw InvalidResponse("License response contains an unsupported status.");
            }

            switch (data.Mode)
            {
                case "hardware_locked":
                case "floating":
                case "named_user":
                    break;
                default:
                    throw InvalidResponse("License response contains an unsupported mode.");
            }

            var startsAt = RequireTimestamp(data.StartsAt, "license start timestamp");
            DateTimeOffset? expiresAt = null;
            if (data.ExpiresAt != null)
            {
                expiresAt = RequireTimestamp(data.ExpiresAt, "license expiry timestamp");
                if (expiresAt <= startsAt)
                {
                    throw InvalidResponse("License response contains an inconsistent validity window.");
                }
            }

            if (data.SeatLimit.HasValue && (data.SeatLimit.Value < 1 || data.SeatLimit.Value > 1_000_000))
            {
                throw InvalidResponse("License response contains an invalid seat limit.");
            }

            if (data.ActiveSeats < 0 ||
                (data.SeatLimit.HasValue && data.ActiveSeats > data.SeatLimit.Value))
            {
                throw InvalidResponse("License response contains an invalid active seat count.");
            }

            if (requireActive)
            {
                var now = DateTimeOffset.UtcNow;
                if (data.Status != "active" || startsAt > now || (expiresAt.HasValue && expiresAt.Value <= now))
                {
                    throw InvalidResponse("Server marked a license valid without an active validity window.");
                }
            }

            ValidateEntitlements(data.ActiveEntitlements);
        }

        private static void ValidateEntitlements(List<EntitlementData>? entitlements)
        {
            if (entitlements == null)
            {
                return;
            }

            if (entitlements.Count > 500)
            {
                throw InvalidResponse("License response contains too many entitlements.");
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entitlement in entitlements)
            {
                if (!SecurityValidation.IsSafeIdentifier(entitlement.Key, 1, 100) ||
                    !keys.Add(entitlement.Key!))
                {
                    throw InvalidResponse("License response contains an invalid or duplicate entitlement.");
                }

                if (entitlement.ExpiresAt != null)
                {
                    RequireTimestamp(entitlement.ExpiresAt, "entitlement expiry timestamp");
                }
            }
        }

        private void ValidateOfflineTokenEnvelope(
            OfflineTokenResponse response,
            string expectedLicenseKey,
            string expectedFingerprint)
        {
            var token = response.Token;
            var signature = response.Signature;
            if (response.Object != "offline_token" ||
                token == null ||
                signature == null ||
                response.AdditionalData?.Count > 0 ||
                token.AdditionalData?.Count > 0 ||
                signature.AdditionalData?.Count > 0 ||
                !SecurityValidation.IsSafeText(response.Canonical, 2, SecurityValidation.MaxCanonicalJsonBytes))
            {
                throw new CryptoException("Offline token envelope is incomplete or contains unsupported fields.");
            }

            if (signature.Algorithm != "Ed25519" ||
                !SecurityValidation.IsSafeIdentifier(signature.KeyId, 1, 255, allowColon: true) ||
                !SecurityValidation.IsAsciiPrintable(signature.Value, 1, 128) ||
                !Ed25519Verifier.ConstantTimeEquals(signature.KeyId, token.Kid))
            {
                throw new CryptoException("Offline token signature block is invalid.", CryptoException.InvalidSignatureCode);
            }

            ValidateSignatureEncoding(signature.Value!);

            if (token.SchemaVersion != 1 ||
                !SecurityValidation.IsAsciiPrintable(token.LicenseKey, 1, 512) ||
                !SecurityValidation.IsSafeIdentifier(token.ProductSlug, 1, 255) ||
                !SecurityValidation.IsSafeIdentifier(token.PlanKey, 1, 255) ||
                !SecurityValidation.IsSafeIdentifier(token.Mode, 1, 255) ||
                !SecurityValidation.IsSafeIdentifier(token.Kid, 1, 255, allowColon: true) ||
                !SecurityValidation.IsSafeText(token.Fingerprint, 8, 255) ||
                !Ed25519Verifier.ConstantTimeEquals(token.LicenseKey, expectedLicenseKey) ||
                !string.Equals(token.ProductSlug, _options.ProductSlug, StringComparison.Ordinal) ||
                !Ed25519Verifier.ConstantTimeEquals(token.Fingerprint, expectedFingerprint))
            {
                throw new CryptoException("Offline token identity claims are invalid or do not match this client.");
            }

            if (token.Mode != "hardware_locked" && token.Mode != "floating" && token.Mode != "named_user")
            {
                throw new CryptoException("Offline token contains an unsupported license mode.");
            }

            const long maxLifetimeSeconds = 100L * 366L * 24L * 60L * 60L;
            if (token.Iat <= 0 || token.Nbf <= 0 || token.Exp <= 0 ||
                token.Iat > token.Nbf || token.Nbf > token.Exp ||
                token.Exp - token.Iat > maxLifetimeSeconds ||
                (token.LicenseExpiresAt.HasValue && token.LicenseExpiresAt.Value < token.Iat) ||
                (token.SeatLimit.HasValue && (token.SeatLimit.Value < 1 || token.SeatLimit.Value > 1_000_000)))
            {
                throw new CryptoException("Offline token contains invalid time or seat claims.");
            }

            if (token.Entitlements == null || token.Entitlements.Count > 500)
            {
                throw new CryptoException("Offline token contains invalid entitlements.");
            }

            var entitlementKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entitlement in token.Entitlements)
            {
                if (entitlement.AdditionalData?.Count > 0 ||
                    !SecurityValidation.IsSafeIdentifier(entitlement.Key, 1, 100) ||
                    !entitlementKeys.Add(entitlement.Key!) ||
                    (entitlement.ExpiresAt.HasValue && entitlement.ExpiresAt.Value <= 0))
                {
                    throw new CryptoException("Offline token contains an invalid or duplicate entitlement.");
                }
            }

            try
            {
                using var canonicalDocument = SecurityValidation.ParseStrictJson(
                    response.Canonical!,
                    SecurityValidation.MaxCanonicalJsonBytes);
                if (canonicalDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new CryptoException("Offline token canonical payload must be a JSON object.");
                }

                var tokenJson = JsonSerializer.Serialize(token, OfflineJsonOptions);
                using var tokenDocument = SecurityValidation.ParseStrictJson(
                    tokenJson,
                    SecurityValidation.MaxCanonicalJsonBytes);
                if (!SecurityValidation.JsonElementsEqual(canonicalDocument.RootElement, tokenDocument.RootElement))
                {
                    throw new CryptoException("Offline token payload does not match the signed canonical payload.");
                }
            }
            catch (CryptoException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is NotSupportedException)
            {
                throw new CryptoException("Offline token canonical payload is invalid.", ex);
            }
        }

        private static void ValidateSignatureEncoding(string signature)
        {
            foreach (var character in signature)
            {
                var valid = (character >= 'A' && character <= 'Z') ||
                            (character >= 'a' && character <= 'z') ||
                            (character >= '0' && character <= '9') ||
                            character == '+' || character == '/' || character == '=' ||
                            character == '-' || character == '_';
                if (!valid)
                {
                    throw new CryptoException("Offline token signature is not valid Base64.", CryptoException.InvalidSignatureCode);
                }
            }

            try
            {
                var normalized = signature.Replace('-', '+').Replace('_', '/');
                switch (normalized.Length % 4)
                {
                    case 2:
                        normalized += "==";
                        break;
                    case 3:
                        normalized += "=";
                        break;
                    case 1:
                        throw new FormatException("Invalid Base64 length.");
                }

                if (Convert.FromBase64String(normalized).Length != 64)
                {
                    throw new CryptoException("Offline token signature must decode to 64 bytes.", CryptoException.InvalidSignatureCode);
                }
            }
            catch (CryptoException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new CryptoException("Offline token signature is not valid Base64.", CryptoException.InvalidSignatureCode, ex);
            }
        }

        private static void ValidatePublicKey(string publicKey)
        {
            if (!SecurityValidation.IsAsciiPrintable(publicKey, 1, 64))
            {
                throw new CryptoException("Public signing key is invalid.", CryptoException.InvalidKeyCode);
            }

            try
            {
                var bytes = Convert.FromBase64String(publicKey);
                if (bytes.Length != 32 || !string.Equals(Convert.ToBase64String(bytes), publicKey, StringComparison.Ordinal))
                {
                    throw new CryptoException("Public signing key must be canonical Base64 for exactly 32 bytes.", CryptoException.InvalidKeyCode);
                }
            }
            catch (CryptoException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new CryptoException("Public signing key is not valid Base64.", CryptoException.InvalidKeyCode, ex);
            }
        }

        private static DateTimeOffset RequireTimestamp(string? value, string fieldName)
        {
            if (!SecurityValidation.TryParseRfc3339(value, out var timestamp))
            {
                throw InvalidResponse($"API response contains an invalid {fieldName}.");
            }

            return timestamp;
        }

        private static void ValidateLicenseKey(string value, string parameterName)
        {
            if (!SecurityValidation.IsAsciiPrintable(value, 1, 512))
            {
                throw new ArgumentException("License key must contain 1 to 512 printable ASCII characters without spaces.", parameterName);
            }
        }

        private static void ValidateFingerprint(string value, string parameterName)
        {
            if (!SecurityValidation.IsSafeText(value, 8, 255))
            {
                throw new ArgumentException("Device fingerprint must be valid text between 8 and 255 bytes.", parameterName);
            }
        }

        private static string EncodePathSegment(string value)
        {
            return Uri.EscapeDataString(value);
        }

        private static ApiException InvalidResponse(string message)
        {
            return new ApiException(message, 200, "invalid_response");
        }

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
            if (!OfflinePolicyIsEnabled())
            {
                return false;
            }

            // Never replace an authoritative HTTP response (including 4xx/5xx) or
            // a local protocol/security rejection with cached authorization.
            return error is ApiException apiError && apiError.IsNetworkError;
        }

        private bool OfflinePolicyIsEnabled()
        {
            return _options.OfflineFallbackMode != OfflineFallbackMode.Disabled &&
                _options.MaxOfflineDays > 0;
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
                    return code == "revoked" ||
                           code == "already_deactivated" ||
                           code == "not_active" ||
                           code == "not_found" ||
                           code == "suspended" ||
                           code == "expired";

                default:
                    return false;
            }
        }

        private void HandleNetworkStatusChange(bool isOnline)
        {
            if (_lifetimeToken.IsCancellationRequested)
            {
                return;
            }

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
                if (license != null && OfflinePolicyIsEnabled())
                {
                    _ = SyncOfflineAssetsAsync(license.Key, _lifetimeToken);
                    StartOfflineRefresh(license.Key);
                }
            }
            else if (wasOnline && !isOnline)
            {
                _eventBus.Emit(LicenseSeatEvents.NetworkOffline, null);
                StopAutoValidation();
                StopHeartbeat();
                StopOfflineRefresh();
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

        private CancellationTokenSource CreateOperationCancellation(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeToken);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed))
            {
                throw new ObjectDisposedException(nameof(LicenseSeatClient));
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
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                Volatile.Write(ref _disposed, true);
            }

            _lifetimeCancellation.Cancel();

            StopAutoValidation();
            StopHeartbeat();
            StopOfflineRefresh();
            _connectivityTimer?.Dispose();
            _apiClient.Dispose();

            _eventBus.Emit(LicenseSeatEvents.SdkDestroyed, null);
            _eventBus.Clear();
            _lifetimeCancellation.Dispose();
        }
    }
}
