using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat;

/// <summary>
/// Client for making API requests to the LicenseSeat API with retry logic.
/// </summary>
internal sealed class ApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientAdapter _httpClient;
    private readonly LicenseSeatClientOptions _options;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    /// <summary>
    /// Raised when network status changes.
    /// </summary>
    public event Action<bool>? OnNetworkStatusChange;

    /// <summary>
    /// Gets a value indicating whether the client is currently online.
    /// </summary>
    public bool IsOnline { get; private set; } = true;

    /// <summary>
    /// Creates a new API client.
    /// </summary>
    /// <param name="options">Client options.</param>
    public ApiClient(LicenseSeatClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = new DefaultHttpClientAdapter(options);
        _ownsHttpClient = true;
    }

    /// <summary>
    /// Creates a new API client with a custom HTTP client adapter.
    /// </summary>
    /// <param name="options">Client options.</param>
    /// <param name="httpClient">HTTP client adapter.</param>
    public ApiClient(LicenseSeatClientOptions options, IHttpClientAdapter httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = false;
    }

    /// <summary>
    /// Sends a GET request to the API.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="path">The API endpoint path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<TResponse> GetAsync<TResponse>(string path, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(path);
        return await ExecuteWithRetryAsync<TResponse>(
            () => _httpClient.GetAsync(url, cancellationToken),
            cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request to the API.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="path">The API endpoint path.</param>
    /// <param name="request">The request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(path);
        var jsonBody = JsonSerializer.Serialize(request, JsonOptions);

        return await ExecuteWithRetryAsync<TResponse>(
            () => _httpClient.PostAsync(url, jsonBody, cancellationToken),
            cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a POST request to the API.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="path">The API endpoint path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized response.</returns>
    public async Task<TResponse> PostAsync<TResponse>(string path, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(path);
        return await ExecuteWithRetryAsync<TResponse>(
            () => _httpClient.PostAsync(url, "{}", cancellationToken),
            cancellationToken
        ).ConfigureAwait(false);
    }

    private async Task<TResponse> ExecuteWithRetryAsync<TResponse>(
        Func<Task<HttpResponse>> requestFunc,
        CancellationToken cancellationToken)
    {
        ApiException? lastError = null;

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var response = await requestFunc().ConfigureAwait(false);

                // Handle successful response
                if (response.IsSuccess)
                {
                    // Mark as online if we were offline
                    if (!IsOnline)
                    {
                        IsOnline = true;
                        OnNetworkStatusChange?.Invoke(true);
                    }

                    return DeserializeResponse<TResponse>(response.Body);
                }

                // Parse error response
                lastError = ParseErrorResponse(response);

                // Check if we should retry
                if (attempt < _options.MaxRetries && ShouldRetry(lastError))
                {
                    var delay = CalculateRetryDelay(attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Handle network errors
                if (lastError.IsNetworkError && IsOnline)
                {
                    IsOnline = false;
                    OnNetworkStatusChange?.Invoke(false);
                }

                throw lastError;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Unexpected error during request
                lastError = new ApiException($"Request failed: {ex.Message}", 0, ex);

                if (attempt < _options.MaxRetries)
                {
                    var delay = CalculateRetryDelay(attempt);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (IsOnline)
                {
                    IsOnline = false;
                    OnNetworkStatusChange?.Invoke(false);
                }

                throw lastError;
            }
        }

        // Should not reach here, but just in case
        throw lastError ?? new ApiException("Request failed after retries", 0);
    }

    private static TResponse DeserializeResponse<TResponse>(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            // Return default for empty response (e.g., 204 No Content)
            return default!;
        }

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            return result ?? throw new ApiException("Empty response body", 200);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"Failed to parse response: {ex.Message}", 200, ex);
        }
    }

    private static ApiException ParseErrorResponse(HttpResponse response)
    {
        if (string.IsNullOrEmpty(response.Body))
        {
            return new ApiException($"Request failed with status {response.StatusCode}", response.StatusCode);
        }

        try
        {
            var error = JsonSerializer.Deserialize<ApiErrorResponse>(response.Body, JsonOptions);
            var message = error?.Error ?? error?.Message ?? $"Request failed with status {response.StatusCode}";
            return new ApiException(message, response.StatusCode, error?.ReasonCode, response.Body);
        }
        catch (JsonException)
        {
            return new ApiException($"Request failed with status {response.StatusCode}: {response.Body}",
                response.StatusCode, null, response.Body);
        }
    }

    private static bool ShouldRetry(ApiException error)
    {
        return error.IsRetryable;
    }

    private TimeSpan CalculateRetryDelay(int attempt)
    {
        // Exponential backoff: delay * 2^attempt
        var delayMs = _options.RetryDelay.TotalMilliseconds * Math.Pow(2, attempt);

        // Add some jitter to prevent thundering herd
        var jitter = new Random().Next(0, (int)(delayMs * 0.1));
        delayMs += jitter;

        return TimeSpan.FromMilliseconds(delayMs);
    }

    private string BuildUrl(string path)
    {
        var baseUrl = _options.ApiBaseUrl.TrimEnd('/');
        var trimmedPath = path.TrimStart('/');
        return $"{baseUrl}/{trimmedPath}";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsHttpClient && _httpClient is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
