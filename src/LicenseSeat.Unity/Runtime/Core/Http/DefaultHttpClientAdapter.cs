using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat;

/// <summary>
/// Default implementation of <see cref="IHttpClientAdapter"/> using <see cref="HttpClient"/>.
/// </summary>
public sealed class DefaultHttpClientAdapter : IHttpClientAdapter, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Creates a new instance with a new HttpClient.
    /// </summary>
    /// <param name="options">Client options for configuration.</param>
    public DefaultHttpClientAdapter(LicenseSeatClientOptions options)
        : this(CreateHttpClient(options), ownsHttpClient: true, options)
    {
    }

    /// <summary>
    /// Creates a new instance with an existing HttpClient.
    /// </summary>
    /// <param name="httpClient">The HttpClient to use.</param>
    /// <param name="options">Client options for configuration.</param>
    public DefaultHttpClientAdapter(HttpClient httpClient, LicenseSeatClientOptions options)
        : this(httpClient, ownsHttpClient: false, options)
    {
    }

    private DefaultHttpClientAdapter(HttpClient httpClient, bool ownsHttpClient, LicenseSeatClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _ownsHttpClient = ownsHttpClient;

        // Configure default headers
        if (!string.IsNullOrEmpty(options.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
        }

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "LicenseSeat-CSharp-SDK/1.0");
    }

    /// <inheritdoc/>
    public async Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new HttpResponse((int)response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            // Network-level failure - return status 0 to indicate network error
            return new HttpResponse(0, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout
            return new HttpResponse(408, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return new HttpResponse((int)response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            // Network-level failure - return status 0 to indicate network error
            return new HttpResponse(0, ex.Message);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Timeout
            return new HttpResponse(408, ex.Message);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient(LicenseSeatClientOptions options)
    {
        var handler = new HttpClientHandler();

        var client = new HttpClient(handler)
        {
            Timeout = options.HttpTimeout
        };

        return client;
    }
}
