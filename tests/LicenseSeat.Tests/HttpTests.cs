using System;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseSeat.Tests;

public class HttpTests
{
    public class HttpResponseTests
    {
        [Theory]
        [InlineData(200, true)]
        [InlineData(201, true)]
        [InlineData(204, true)]
        [InlineData(299, true)]
        [InlineData(199, false)]
        [InlineData(300, false)]
        [InlineData(400, false)]
        [InlineData(500, false)]
        public void IsSuccess_ReturnsCorrectValue(int statusCode, bool expected)
        {
            var response = new HttpResponse(statusCode, "body");

            Assert.Equal(expected, response.IsSuccess);
        }

        [Fact]
        public void Constructor_SetsProperties()
        {
            var response = new HttpResponse(200, "test body");

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("test body", response.Body);
        }
    }

    public class MockHttpClientAdapter : IHttpClientAdapter
    {
        private Func<string, HttpResponse>? _getHandler;
        private Func<string, string, HttpResponse>? _postHandler;

        public int GetCallCount { get; private set; }
        public int PostCallCount { get; private set; }
        public string? LastGetUrl { get; private set; }
        public string? LastPostUrl { get; private set; }
        public string? LastPostBody { get; private set; }

        public void SetupGet(Func<string, HttpResponse> handler)
        {
            _getHandler = handler;
        }

        public void SetupPost(Func<string, string, HttpResponse> handler)
        {
            _postHandler = handler;
        }

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            GetCallCount++;
            LastGetUrl = url;
            cancellationToken.ThrowIfCancellationRequested();
            var response = _getHandler?.Invoke(url) ?? new HttpResponse(200, "{}");
            return Task.FromResult(response);
        }

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            PostCallCount++;
            LastPostUrl = url;
            LastPostBody = jsonBody;
            cancellationToken.ThrowIfCancellationRequested();
            var response = _postHandler?.Invoke(url, jsonBody) ?? new HttpResponse(200, "{}");
            return Task.FromResult(response);
        }
    }

    public class ApiClientTests
    {
        private static LicenseSeatClientOptions CreateOptions() => new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ProductSlug = "test-product",
            ApiBaseUrl = "https://api.test.com",
            MaxRetries = 2,
            RetryDelay = TimeSpan.FromMilliseconds(10)
        };

        [Fact]
        public async Task GetAsync_Success_ReturnsDeserializedResponse()
        {
            var options = CreateOptions();
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupGet(_ => new HttpResponse(200, "{\"valid\":true}"));

            using var client = new ApiClient(options, mockHttp);

            var result = await client.GetAsync<ValidationResult>("/test");

            Assert.True(result.Valid);
            Assert.Equal("https://api.test.com/test", mockHttp.LastGetUrl);
        }

        [Fact]
        public async Task PostAsync_Success_ReturnsDeserializedResponse()
        {
            var options = CreateOptions();
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupPost((_, _) => new HttpResponse(200, "{\"valid\":true,\"license\":{\"key\":\"test\"}}"));

            using var client = new ApiClient(options, mockHttp);

            var request = new ValidationRequest { DeviceId = "device-123" };
            var result = await client.PostAsync<ValidationRequest, ValidationResult>("/licenses/validate", request);

            Assert.True(result.Valid);
            Assert.Contains("device-123", mockHttp.LastPostBody);
        }

        [Fact]
        public async Task GetAsync_NetworkError_ThrowsApiException()
        {
            var options = CreateOptions();
            options.MaxRetries = 0;
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupGet(_ => new HttpResponse(0, "Network error"));

            using var client = new ApiClient(options, mockHttp);

            var ex = await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<ValidationResult>("/test"));
            Assert.Equal(0, ex.StatusCode);
            Assert.True(ex.IsNetworkError);
        }

        [Fact]
        public async Task PostAsync_ClientError_ThrowsApiException()
        {
            var options = CreateOptions();
            options.MaxRetries = 0;
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupPost((_, _) => new HttpResponse(404, "{\"error\":{\"code\":\"license_not_found\",\"message\":\"License not found\"}}"));

            using var client = new ApiClient(options, mockHttp);

            var ex = await Assert.ThrowsAsync<ApiException>(
                () => client.PostAsync<ValidationRequest, ValidationResult>("/test", new ValidationRequest()));

            Assert.Equal(404, ex.StatusCode);
            Assert.Equal("license_not_found", ex.Code);
            Assert.Contains("License not found", ex.Message);
        }

        [Fact]
        public async Task PostAsync_ServerError_RetriesAndFails()
        {
            var options = CreateOptions();
            options.MaxRetries = 2;
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupPost((_, _) => new HttpResponse(503, "{\"error\":{\"code\":\"service_unavailable\",\"message\":\"Service unavailable\"}}"));

            using var client = new ApiClient(options, mockHttp);

            var ex = await Assert.ThrowsAsync<ApiException>(
                () => client.PostAsync<ValidationRequest, ValidationResult>("/test", new ValidationRequest()));

            // Should have retried: 1 initial + 2 retries = 3 total
            Assert.Equal(3, mockHttp.PostCallCount);
            Assert.Equal(503, ex.StatusCode);
        }

        [Fact]
        public async Task PostAsync_RetrySucceeds_ReturnsResult()
        {
            var options = CreateOptions();
            options.MaxRetries = 2;
            var mockHttp = new MockHttpClientAdapter();
            var callCount = 0;
            mockHttp.SetupPost((_, _) =>
            {
                callCount++;
                // Fail first two attempts, succeed on third
                return callCount < 3
                    ? new HttpResponse(503, "{\"error\":{\"message\":\"Service unavailable\"}}")
                    : new HttpResponse(200, "{\"valid\":true}");
            });

            using var client = new ApiClient(options, mockHttp);

            var result = await client.PostAsync<ValidationRequest, ValidationResult>("/test", new ValidationRequest());

            Assert.True(result.Valid);
            Assert.Equal(3, mockHttp.PostCallCount);
        }

        [Fact]
        public async Task Cancellation_ThrowsOperationCanceledException()
        {
            var options = CreateOptions();
            var mockHttp = new MockHttpClientAdapter();

            using var client = new ApiClient(options, mockHttp);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => client.GetAsync<ValidationResult>("/test", cts.Token));
        }

        [Fact]
        public void BuildUrl_CombinesBaseUrlAndPath()
        {
            var options = CreateOptions();
            options.ApiBaseUrl = "https://api.test.com/";
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupGet(_ => new HttpResponse(200, "{}"));

            using var client = new ApiClient(options, mockHttp);

            // Access via GET call to verify URL building
            _ = client.GetAsync<object>("/endpoint");

            Assert.Equal("https://api.test.com/endpoint", mockHttp.LastGetUrl);
        }

        [Fact]
        public void BuildUrl_HandlesLeadingSlashInPath()
        {
            var options = CreateOptions();
            options.ApiBaseUrl = "https://api.test.com/";
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupGet(_ => new HttpResponse(200, "{}"));

            using var client = new ApiClient(options, mockHttp);

            _ = client.GetAsync<object>("/endpoint");

            // Should strip trailing slash from base URL and leading slash from path
            Assert.Equal("https://api.test.com/endpoint", mockHttp.LastGetUrl);
        }

        [Fact]
        public async Task OnNetworkStatusChange_FiresWhenGoingOffline()
        {
            var options = CreateOptions();
            options.MaxRetries = 0;
            var mockHttp = new MockHttpClientAdapter();
            mockHttp.SetupGet(_ => new HttpResponse(0, "Network error"));

            using var client = new ApiClient(options, mockHttp);
            bool? statusReceived = null;
            client.OnNetworkStatusChange += status => statusReceived = status;

            Assert.True(client.IsOnline);

            await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<object>("/test"));

            Assert.False(client.IsOnline);
            Assert.False(statusReceived);
        }

        [Fact]
        public async Task OnNetworkStatusChange_FiresWhenComingBackOnline()
        {
            var options = CreateOptions();
            options.MaxRetries = 0;
            var mockHttp = new MockHttpClientAdapter();

            using var client = new ApiClient(options, mockHttp);
            var statusChanges = new System.Collections.Generic.List<bool>();
            client.OnNetworkStatusChange += status => statusChanges.Add(status);

            // First, go offline
            mockHttp.SetupGet(_ => new HttpResponse(0, "Network error"));
            await Assert.ThrowsAsync<ApiException>(() => client.GetAsync<object>("/test"));

            Assert.False(client.IsOnline);
            Assert.Contains(false, statusChanges);

            // Then come back online
            mockHttp.SetupGet(_ => new HttpResponse(200, "{}"));
            await client.GetAsync<object>("/test");

            Assert.True(client.IsOnline);
            Assert.Contains(true, statusChanges);
        }
    }
}
