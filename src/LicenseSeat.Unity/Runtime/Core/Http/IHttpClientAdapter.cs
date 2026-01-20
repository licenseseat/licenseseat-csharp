using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat;

/// <summary>
/// Abstraction over HTTP client for testability.
/// </summary>
public interface IHttpClientAdapter
{
    /// <summary>
    /// Sends a GET request.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a POST request with JSON body.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="jsonBody">The JSON request body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an HTTP response.
/// </summary>
public sealed class HttpResponse
{
    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the response body.
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// Gets a value indicating whether the request was successful (2xx status code).
    /// </summary>
    public bool IsSuccess => StatusCode >= 200 && StatusCode < 300;

    /// <summary>
    /// Creates a new HTTP response.
    /// </summary>
    /// <param name="statusCode">The status code.</param>
    /// <param name="body">The response body.</param>
    public HttpResponse(int statusCode, string body)
    {
        StatusCode = statusCode;
        Body = body;
    }
}
