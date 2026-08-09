#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat
{

    /// <summary>
    /// Client for making API requests to the LicenseSeat API with retry logic.
    /// </summary>
    internal sealed class ApiClient : IDisposable
    {
        private static readonly object RandomLock = new object();
        private static readonly Random JitterRandom = new Random();

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            MaxDepth = SecurityValidation.MaxJsonDepth,
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
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _httpClient = new DefaultHttpClientAdapter(_options);
            _ownsHttpClient = true;
        }

        /// <summary>
        /// Creates a new API client with a custom HTTP client adapter.
        /// </summary>
        /// <param name="options">Client options.</param>
        /// <param name="httpClient">HTTP client adapter.</param>
        public ApiClient(LicenseSeatClientOptions options, IHttpClientAdapter httpClient)
        {
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
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
            ThrowIfDisposed();
            var url = BuildUrl(path);
            return await ExecuteWithRetryAsync<TResponse>(
                () => _httpClient.GetAsync(url, cancellationToken),
                cancellationToken,
                retryable: true
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
        /// <param name="retryable">Whether this operation is safe to retry after a transient failure.</param>
        /// <returns>The deserialized response.</returns>
        public async Task<TResponse> PostAsync<TRequest, TResponse>(
            string path,
            TRequest request,
            CancellationToken cancellationToken = default,
            bool retryable = false)
        {
            ThrowIfDisposed();
            var url = BuildUrl(path);
            var jsonBody = SerializeRequest(request);

            if (_options.TelemetryEnabled)
            {
                jsonBody = InjectTelemetry(jsonBody);
            }

            ValidateRequestBody(jsonBody);

            return await ExecuteWithRetryAsync<TResponse>(
                () => _httpClient.PostAsync(url, jsonBody, cancellationToken),
                cancellationToken,
                retryable
            ).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a POST request to the API.
        /// </summary>
        /// <typeparam name="TResponse">The response type.</typeparam>
        /// <param name="path">The API endpoint path.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="retryable">Whether this operation is safe to retry after a transient failure.</param>
        /// <returns>The deserialized response.</returns>
        public async Task<TResponse> PostAsync<TResponse>(
            string path,
            CancellationToken cancellationToken = default,
            bool retryable = false)
        {
            ThrowIfDisposed();
            var url = BuildUrl(path);
            return await ExecuteWithRetryAsync<TResponse>(
                () => _httpClient.PostAsync(url, "{}", cancellationToken),
                cancellationToken,
                retryable
            ).ConfigureAwait(false);
        }

        private async Task<TResponse> ExecuteWithRetryAsync<TResponse>(
            Func<Task<HttpResponse>> requestFunc,
            CancellationToken cancellationToken,
            bool retryable)
        {
            ApiException? lastError = null;
            var maxRetries = retryable ? _options.MaxRetries : 0;

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var response = await requestFunc().ConfigureAwait(false);

                    if (response.StatusCode > 0 && !IsOnline)
                    {
                        IsOnline = true;
                        OnNetworkStatusChange?.Invoke(true);
                    }

                    // Handle successful response
                    if (response.IsSuccess)
                    {
                        return DeserializeResponse<TResponse>(response.Body);
                    }

                    // Parse error response
                    lastError = ParseErrorResponse(response);

                    // Check if we should retry
                    if (attempt < maxRetries && ShouldRetry(lastError))
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
                    cancellationToken.ThrowIfCancellationRequested();

                    // Unexpected error during request
                    lastError = new ApiException("Network request failed.", 0, ex);

                    if (attempt < maxRetries)
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

        private TResponse DeserializeResponse<TResponse>(string body)
        {
            try
            {
                using var document = SecurityValidation.ParseStrictJson(body, _options.MaxResponseBodyBytes);
                var result = JsonSerializer.Deserialize<TResponse>(document.RootElement.GetRawText(), JsonOptions);
                return result ?? throw new ApiException("Empty response body", 200);
            }
            catch (ApiException)
            {
                throw;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is NotSupportedException)
            {
                throw new ApiException("API returned invalid or ambiguous JSON.", 200, ex);
            }
        }

        private ApiException ParseErrorResponse(HttpResponse response)
        {
            if (string.IsNullOrEmpty(response.Body))
            {
                return new ApiException($"Request failed with status {response.StatusCode}", response.StatusCode);
            }

            try
            {
                using var document = SecurityValidation.ParseStrictJson(response.Body, _options.MaxResponseBodyBytes);
                var error = JsonSerializer.Deserialize<ApiErrorResponse>(document.RootElement.GetRawText(), JsonOptions);

                // New API format: { "error": { "code": "...", "message": "...", "details": {...} } }
                if (error?.Error != null)
                {
                    var message = SecurityValidation.IsSafeText(error.Error.Message, 1, 4096)
                        ? error.Error.Message!
                        : $"Request failed with status {response.StatusCode}";
                    var code = SecurityValidation.IsSafeIdentifier(error.Error.Code, 1, 128)
                        ? error.Error.Code
                        : null;
                    return new ApiException(message, response.StatusCode, code);
                }

                // Fallback for simple error format
                var fallbackMessage = SecurityValidation.IsSafeText(error?.Message, 1, 4096)
                    ? error!.Message!
                    : $"Request failed with status {response.StatusCode}";
                return new ApiException(fallbackMessage, response.StatusCode);
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is NotSupportedException)
            {
                return new ApiException(
                    $"Request failed with status {response.StatusCode} and an invalid error response.",
                    response.StatusCode);
            }
        }

        private static bool ShouldRetry(ApiException error)
        {
            return error.IsRetryable;
        }

        private TimeSpan CalculateRetryDelay(int attempt)
        {
            // Exponential backoff: delay * 2^attempt
            var delayMs = Math.Min(
                _options.RetryDelay.TotalMilliseconds * Math.Pow(2, attempt),
                TimeSpan.FromMinutes(5).TotalMilliseconds);

            // Add some jitter to prevent thundering herd
            int jitter;
            lock (RandomLock)
            {
                var jitterLimit = Math.Max(1, (int)Math.Min(int.MaxValue, delayMs * 0.1));
                jitter = JitterRandom.Next(0, jitterLimit);
            }
            delayMs += jitter;

            return TimeSpan.FromMilliseconds(delayMs);
        }

        private string InjectTelemetry(string jsonBody)
        {
            try
            {
                var node = JsonNode.Parse(jsonBody);
                if (node is JsonObject obj)
                {
                    obj["telemetry"] = JsonSerializer.SerializeToNode(
                        TelemetryPayload.Collect(_options.AppVersion, _options.AppBuild).ToDictionary(), JsonOptions);
                    return node.ToJsonString(JsonOptions);
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException || ex is InvalidOperationException)
            {
                // If telemetry injection fails, send the request without it
            }

            return jsonBody;
        }

        private string BuildUrl(string path)
        {
            if (!SecurityValidation.IsAsciiPrintable(path, 1, 2048) ||
                path.IndexOf('\\') >= 0 ||
                path.IndexOf('?') >= 0 ||
                path.IndexOf('#') >= 0 ||
                path.IndexOf("//", StringComparison.Ordinal) >= 0 ||
                path.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("%25", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    !SecurityValidation.HasValidPercentEncoding(path))
            {
                throw new ArgumentException("API path is invalid.", nameof(path));
            }

            var trimmedPath = path.TrimStart('/');
            foreach (var segment in trimmedPath.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    throw new ArgumentException("API path contains an invalid segment.", nameof(path));
                }
            }

            var baseUri = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            var url = new Uri(baseUri, trimmedPath);
            if (!string.Equals(url.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(url.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) ||
                url.Port != baseUri.Port ||
                !url.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.Ordinal) ||
                url.Query.Length != 0 ||
                url.Fragment.Length != 0 ||
                url.AbsoluteUri.Length > 8192)
            {
                throw new InvalidOperationException("API path resolved outside the configured API base URL.");
            }

            return url.AbsoluteUri;
        }

        private string SerializeRequest<TRequest>(TRequest request)
        {
            try
            {
                return JsonSerializer.Serialize(request, JsonOptions);
            }
            catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
            {
                throw new ArgumentException("Request contains data that cannot be represented as bounded JSON.", nameof(request), ex);
            }
        }

        private void ValidateRequestBody(string body)
        {
            try
            {
                using var document = SecurityValidation.ParseStrictJson(body, _options.MaxRequestBodyBytes);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("API request body must be a JSON object.", nameof(body));
                }
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException)
            {
                throw new ArgumentException("Request exceeds the supported JSON limits.", nameof(body), ex);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ApiClient));
            }
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
}
