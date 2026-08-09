#nullable enable
#if UNITY_5_3_OR_NEWER
using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LicenseSeat
{
    /// <summary>
    /// Unity-native HTTP client adapter using UnityWebRequest.
    /// This adapter works on all Unity platforms including WebGL where System.Net.Http is unavailable.
    /// Calls must be initiated from Unity's main thread.
    /// </summary>
    public sealed class UnityWebRequestAdapter : IHttpClientAdapter
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly LicenseSeatClientOptions _options;
        private readonly Uri _baseUri;

        /// <summary>
        /// Creates a new UnityWebRequest-based HTTP adapter.
        /// </summary>
        /// <param name="options">Client options for configuration.</param>
        public UnityWebRequestAdapter(LicenseSeatClientOptions options)
        {
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _baseUri = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            if (!TryValidateRequestUri(url, out var requestUri))
            {
                return SecurityFailure("Request URL was rejected by the SDK transport policy.");
            }

            using var request = UnityWebRequest.Get(requestUri.AbsoluteUri);
            request.downloadHandler?.Dispose();
            request.downloadHandler = new BoundedDownloadHandler(_options.MaxResponseBodyBytes);
            ConfigureRequest(request, requestUri);

            try
            {
                var operation = request.SendWebRequest();
                await WaitForRequestAsync(operation, cancellationToken);
                return CreateResponse(request, requestUri);
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is ArgumentException ||
                ex is DecoderFallbackException)
            {
                LogTransportFailure("GET");
                return new HttpResponse(0, "Network request failed.");
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> PostAsync(
            string url,
            string jsonBody,
            CancellationToken cancellationToken = default)
        {
            if (jsonBody == null)
            {
                throw new ArgumentNullException(nameof(jsonBody));
            }

            if (!TryValidateRequestUri(url, out var requestUri))
            {
                return SecurityFailure("Request URL was rejected by the SDK transport policy.");
            }

            byte[] bodyBytes;
            try
            {
                bodyBytes = StrictUtf8.GetBytes(jsonBody);
            }
            catch (EncoderFallbackException)
            {
                return SecurityFailure("Request body contains invalid Unicode.");
            }

            if (bodyBytes.Length == 0 || bodyBytes.Length > _options.MaxRequestBodyBytes)
            {
                return SecurityFailure("Request body exceeds the configured size limit.");
            }

            using var request = new UnityWebRequest(requestUri.AbsoluteUri, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(bodyBytes),
                downloadHandler = new BoundedDownloadHandler(_options.MaxResponseBodyBytes)
            };
            request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            ConfigureRequest(request, requestUri);

            try
            {
                var operation = request.SendWebRequest();
                await WaitForRequestAsync(operation, cancellationToken);
                return CreateResponse(request, requestUri);
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
            catch (Exception ex) when (
                ex is InvalidOperationException ||
                ex is ArgumentException ||
                ex is DecoderFallbackException)
            {
                LogTransportFailure("POST");
                return new HttpResponse(0, "Network request failed.");
            }
        }

        private void ConfigureRequest(UnityWebRequest request, Uri requestUri)
        {
            request.timeout = Math.Max(1, (int)Math.Ceiling(_options.HttpTimeout.TotalSeconds));
            request.redirectLimit = 0;
            request.SetRequestHeader("Accept", "application/json");

            // Credentials are applied per request and never sent to deliberately public endpoints.
            if (!IsPublicEndpoint(requestUri))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_options.ApiKey}");
            }
        }

        private HttpResponse CreateResponse(UnityWebRequest request, Uri originalUri)
        {
            var boundedHandler = request.downloadHandler as BoundedDownloadHandler;
            if (boundedHandler == null || boundedHandler.LimitExceeded)
            {
                return SecurityFailure("Response body exceeds the configured size limit.");
            }

            if (!TryValidateRequestUri(request.url, out var finalUri) ||
                Uri.Compare(
                    originalUri,
                    finalUri,
                    UriComponents.AbsoluteUri,
                    UriFormat.SafeUnescaped,
                    StringComparison.Ordinal) != 0)
            {
                return SecurityFailure("HTTP redirects are not allowed.");
            }

            var statusCode = (int)request.responseCode;
            if (statusCode >= 300 && statusCode < 400)
            {
                return SecurityFailure("HTTP redirects are not allowed.");
            }

            var contentLength = request.GetResponseHeader("Content-Length");
            if (long.TryParse(contentLength, NumberStyles.None, CultureInfo.InvariantCulture, out var declaredLength) &&
                declaredLength > _options.MaxResponseBodyBytes)
            {
                return SecurityFailure("Response body exceeds the configured size limit.");
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                return new HttpResponse(0, "Network request failed.");
            }

            if (request.result == UnityWebRequest.Result.DataProcessingError)
            {
                return SecurityFailure("Network response could not be processed safely.");
            }
#else
            if (request.isNetworkError)
            {
                return new HttpResponse(0, "Network request failed.");
            }
#endif

            if (statusCode >= 200 && statusCode < 300 &&
                !IsJsonMediaType(request.GetResponseHeader("Content-Type")))
            {
                return SecurityFailure("Successful API responses must use a JSON media type.");
            }

            try
            {
                return new HttpResponse(statusCode, boundedHandler.GetText());
            }
            catch (DecoderFallbackException)
            {
                return SecurityFailure("Response body is not valid UTF-8.");
            }
        }

        private bool TryValidateRequestUri(string url, out Uri requestUri)
        {
            requestUri = null!;
            if (!SecurityValidation.IsAsciiPrintable(url, 1, 8192) ||
                url.IndexOf('\\') >= 0 ||
                url.IndexOf("%2f", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("%5c", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) >= 0 ||
                url.IndexOf("%25", StringComparison.OrdinalIgnoreCase) >= 0 ||
                !SecurityValidation.HasValidPercentEncoding(url) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
                !string.Equals(parsed.Scheme, _baseUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(parsed.Host, _baseUri.Host, StringComparison.OrdinalIgnoreCase) ||
                parsed.Port != _baseUri.Port ||
                !parsed.AbsolutePath.StartsWith(_baseUri.AbsolutePath, StringComparison.Ordinal) ||
                parsed.Query.Length != 0 ||
                parsed.Fragment.Length != 0 ||
                !string.IsNullOrEmpty(parsed.UserInfo))
            {
                return false;
            }

            requestUri = parsed;
            return true;
        }

        private bool IsPublicEndpoint(Uri requestUri)
        {
            var relativePath = requestUri.AbsolutePath.Substring(_baseUri.AbsolutePath.Length);
            return string.Equals(relativePath, "health", StringComparison.Ordinal) ||
                   relativePath.StartsWith("signing_keys/", StringComparison.Ordinal);
        }

        private static bool IsJsonMediaType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return false;
            }

            var separator = contentType.IndexOf(';');
            var mediaType = (separator >= 0 ? contentType.Substring(0, separator) : contentType).Trim();
            return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }

        private static HttpResponse SecurityFailure(string message)
        {
            return new HttpResponse(-1, message);
        }

        private void LogTransportFailure(string method)
        {
            if (_options.Debug)
            {
                Debug.LogWarning($"[LicenseSeat SDK] HTTP {method} failed.");
            }
        }

        private static async Task WaitForRequestAsync(
            UnityWebRequestAsyncOperation operation,
            CancellationToken cancellationToken)
        {
            // Poll through Unity's synchronization context so Abort and request disposal remain
            // on the main thread, including when cancellation originates on another thread.
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    operation.webRequest?.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private sealed class BoundedDownloadHandler : DownloadHandlerScript
        {
            private readonly int _maxBytes;
            private byte[] _content;
            private int _length;

            internal BoundedDownloadHandler(int maxBytes)
                : base(new byte[8192])
            {
                _maxBytes = maxBytes;
                _content = new byte[Math.Min(8192, maxBytes)];
            }

            internal bool LimitExceeded { get; private set; }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || dataLength <= 0)
                {
                    return true;
                }

                if (dataLength > _maxBytes - _length)
                {
                    LimitExceeded = true;
                    return false;
                }

                var requiredLength = _length + dataLength;
                if (requiredLength > _content.Length)
                {
                    var nextLength = Math.Min(
                        _maxBytes,
                        Math.Max(requiredLength, checked(_content.Length * 2)));
                    Array.Resize(ref _content, nextLength);
                }

                Buffer.BlockCopy(data, 0, _content, _length, dataLength);
                _length = requiredLength;
                return true;
            }

            internal string GetText()
            {
                return StrictUtf8.GetString(_content, 0, _length);
            }
        }
    }
}
#endif
