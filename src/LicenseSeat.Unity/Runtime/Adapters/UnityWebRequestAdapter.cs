#if UNITY_5_3_OR_NEWER
using System;
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
    /// </summary>
    public sealed class UnityWebRequestAdapter : IHttpClientAdapter
    {
        private readonly LicenseSeatClientOptions _options;

        /// <summary>
        /// Creates a new UnityWebRequest-based HTTP adapter.
        /// </summary>
        /// <param name="options">Client options for configuration.</param>
        public UnityWebRequestAdapter(LicenseSeatClientOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            using var request = UnityWebRequest.Get(url);
            ConfigureRequest(request);

            try
            {
                var operation = request.SendWebRequest();
                await WaitForRequestAsync(operation, cancellationToken).ConfigureAwait(false);

                return CreateResponse(request);
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LicenseSeat SDK] HTTP GET failed: {ex.Message}");
                return new HttpResponse(0, ex.Message);
            }
        }

        /// <inheritdoc/>
        public async Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            using var request = new UnityWebRequest(url, "POST");
            var bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            ConfigureRequest(request);

            try
            {
                var operation = request.SendWebRequest();
                await WaitForRequestAsync(operation, cancellationToken).ConfigureAwait(false);

                return CreateResponse(request);
            }
            catch (OperationCanceledException)
            {
                request.Abort();
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LicenseSeat SDK] HTTP POST failed: {ex.Message}");
                return new HttpResponse(0, ex.Message);
            }
        }

        private void ConfigureRequest(UnityWebRequest request)
        {
            request.timeout = (int)_options.HttpTimeout.TotalSeconds;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("User-Agent", "LicenseSeat-Unity-SDK/1.0");

            if (!string.IsNullOrEmpty(_options.ApiKey))
            {
                request.SetRequestHeader("Authorization", $"Bearer {_options.ApiKey}");
            }
        }

        private static HttpResponse CreateResponse(UnityWebRequest request)
        {
            var statusCode = (int)request.responseCode;
            var body = request.downloadHandler?.text ?? string.Empty;

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                return new HttpResponse(0, request.error ?? "Network error");
            }
#else
            if (request.isNetworkError)
            {
                return new HttpResponse(0, request.error ?? "Network error");
            }
#endif

            return new HttpResponse(statusCode, body);
        }

        private static async Task WaitForRequestAsync(UnityWebRequestAsyncOperation operation, CancellationToken cancellationToken)
        {
            // Use a TaskCompletionSource for proper async/await integration
            var tcs = new TaskCompletionSource<bool>();

            // Register cancellation
            using var registration = cancellationToken.Register(() =>
            {
                operation.webRequest?.Abort();
                tcs.TrySetCanceled(cancellationToken);
            });

            // Handle completion
            operation.completed += _ =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    tcs.TrySetResult(true);
                }
            };

            // If already done, complete immediately
            if (operation.isDone)
            {
                tcs.TrySetResult(true);
            }

            await tcs.Task.ConfigureAwait(false);
        }
    }
}
#endif
