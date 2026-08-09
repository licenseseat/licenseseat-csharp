#nullable enable
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat
{

    /// <summary>
    /// Default implementation of <see cref="IHttpClientAdapter"/> using <see cref="HttpClient"/>.
    /// </summary>
    public sealed class DefaultHttpClientAdapter : IHttpClientAdapter, IDisposable
    {
        private sealed class ResponseLimitExceededException : Exception
        {
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        private readonly HttpClient _httpClient;
        private readonly LicenseSeatClientOptions _options;
        private readonly Uri _baseUri;
        private readonly bool _ownsHttpClient;
        private bool _disposed;

        /// <summary>
        /// Creates a new instance with a new HttpClient.
        /// </summary>
        /// <param name="options">Client options for configuration.</param>
        public DefaultHttpClientAdapter(LicenseSeatClientOptions options)
            : this(CreateHttpClient(options), ownsHttpClient: true, options)
        {
        }

        /// <summary>
        /// Creates a new instance with an existing HttpClient for internal tests.
        /// A caller-provided handler is opaque and could follow a redirect before
        /// this adapter can reject it, leaking a bearer credential.
        /// </summary>
        /// <param name="httpClient">The HttpClient to use.</param>
        /// <param name="options">Client options for configuration.</param>
        internal DefaultHttpClientAdapter(HttpClient httpClient, LicenseSeatClientOptions options)
            : this(httpClient, ownsHttpClient: false, options)
        {
        }

        private DefaultHttpClientAdapter(HttpClient httpClient, bool ownsHttpClient, LicenseSeatClientOptions options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _baseUri = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            _ownsHttpClient = ownsHttpClient;

            if (_httpClient.DefaultRequestHeaders.Authorization != null)
            {
                throw new ArgumentException(
                    "The supplied HttpClient must not use a default Authorization header; LicenseSeat scopes credentials per request.",
                    nameof(httpClient));
            }
        }

        /// <inheritdoc/>
        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            return SendAsync(HttpMethod.Get, url, null, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            if (jsonBody == null)
            {
                throw new ArgumentNullException(nameof(jsonBody));
            }

            return SendAsync(HttpMethod.Post, url, jsonBody, cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }

        private static HttpClient CreateHttpClient(LicenseSeatClientOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false
            };

            return new HttpClient(handler)
            {
                Timeout = options.HttpTimeout
            };
        }

        private async Task<HttpResponse> SendAsync(
            HttpMethod method,
            string url,
            string? jsonBody,
            CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultHttpClientAdapter));
            }

            if (!TryValidateRequestUri(url, out var requestUri))
            {
                return SecurityFailure("Request URL was rejected by the SDK transport policy.");
            }

            try
            {
                using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCancellation.CancelAfter(_options.HttpTimeout);
                var requestCancellationToken = timeoutCancellation.Token;

                using var request = new HttpRequestMessage(method, requestUri);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
                request.Headers.TryAddWithoutValidation("User-Agent", $"LicenseSeat-CSharp-SDK/{LicenseSeatClient.SdkVersion}");

                if (!IsPublicEndpoint(requestUri))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                }

                if (jsonBody != null)
                {
                    byte[] requestBytes;
                    try
                    {
                        requestBytes = StrictUtf8.GetBytes(jsonBody);
                    }
                    catch (EncoderFallbackException)
                    {
                        return SecurityFailure("Request body contains invalid Unicode.");
                    }

                    if (requestBytes.Length == 0 || requestBytes.Length > _options.MaxRequestBodyBytes)
                    {
                        return SecurityFailure("Request body exceeds the configured size limit.");
                    }

                    request.Content = new ByteArrayContent(requestBytes);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
                    {
                        CharSet = "utf-8"
                    };
                }

                using var response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        requestCancellationToken)
                    .ConfigureAwait(false);

                if (response.RequestMessage?.RequestUri == null ||
                    Uri.Compare(
                        requestUri,
                        response.RequestMessage.RequestUri,
                        UriComponents.AbsoluteUri,
                        UriFormat.SafeUnescaped,
                        StringComparison.Ordinal) != 0)
                {
                    return SecurityFailure("HTTP redirects are not allowed.");
                }

                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    return SecurityFailure("HTTP redirects are not allowed.");
                }

                foreach (var encoding in response.Content.Headers.ContentEncoding)
                {
                    if (!string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase))
                    {
                        return SecurityFailure("Encoded HTTP responses are not supported.");
                    }
                }

                if (response.Content.Headers.ContentLength.HasValue &&
                    response.Content.Headers.ContentLength.Value > _options.MaxResponseBodyBytes)
                {
                    return SecurityFailure("Response body exceeds the configured size limit.");
                }

                if (response.IsSuccessStatusCode && !IsJsonMediaType(response.Content.Headers.ContentType?.MediaType))
                {
                    return SecurityFailure("Successful API responses must use a JSON media type.");
                }

                var body = await ReadBoundedBodyAsync(response.Content, requestCancellationToken).ConfigureAwait(false);
                return new HttpResponse((int)response.StatusCode, body);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return new HttpResponse(0, "Network request timed out.");
            }
            catch (HttpRequestException)
            {
                return new HttpResponse(0, "Network request failed.");
            }
            catch (IOException)
            {
                return new HttpResponse(0, "Network response could not be read.");
            }
            catch (DecoderFallbackException)
            {
                return SecurityFailure("Response body is not valid UTF-8.");
            }
            catch (ResponseLimitExceededException)
            {
                return SecurityFailure("Response body exceeds the configured size limit.");
            }
        }

        private async Task<string> ReadBoundedBodyAsync(HttpContent content, CancellationToken cancellationToken)
        {
            using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
            using var output = new MemoryStream();
            var buffer = new byte[8192];

            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (output.Length + read > _options.MaxResponseBodyBytes)
                {
                    throw new ResponseLimitExceededException();
                }

                output.Write(buffer, 0, read);
            }

            return StrictUtf8.GetString(output.ToArray());
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

        private static bool IsJsonMediaType(string? mediaType)
        {
            return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                   (mediaType != null && mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
        }

        private static HttpResponse SecurityFailure(string message)
        {
            return new HttpResponse(-1, message);
        }
    }
}
