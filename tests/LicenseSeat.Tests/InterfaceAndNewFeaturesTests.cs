using System;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CA1859 // Using interface is intentional for testing DI scenarios

namespace LicenseSeat.Tests;

public class InterfaceAndNewFeaturesTests
{
    private sealed class MockHttpClient : IHttpClientAdapter
    {
        private Func<string, HttpResponse>? _getHandler;
        private Func<string, string, HttpResponse>? _postHandler;

        public void SetupGet(Func<string, HttpResponse> handler) => _getHandler = handler;
        public void SetupPost(Func<string, string, HttpResponse> handler) => _postHandler = handler;

        public Task<HttpResponse> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _getHandler?.Invoke(url) ?? new HttpResponse(200, "{}");
            return Task.FromResult(response);
        }

        public Task<HttpResponse> PostAsync(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _postHandler?.Invoke(url, jsonBody) ?? new HttpResponse(200, "{}");
            return Task.FromResult(response);
        }
    }

    private static LicenseSeatClientOptions CreateOptions() => new LicenseSeatClientOptions
    {
        ApiKey = "test-api-key",
        ProductSlug = "test-product",
        ApiBaseUrl = "https://api.test.com",
        AutoInitialize = false,
        AutoValidateInterval = TimeSpan.Zero
    };

    [Fact]
    public void Client_ImplementsILicenseSeatClient()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        Assert.IsAssignableFrom<ILicenseSeatClient>(client);
    }

    [Fact]
    public void ILicenseSeatClient_CanBeUsedForDependencyInjection()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        // Simulate DI scenario
        ILicenseSeatClient client = new LicenseSeatClient(options, mockHttp);

        Assert.NotNull(client.Events);
        Assert.True(client.IsOnline);
        Assert.NotNull(client.Options);
    }

    [Fact]
    public void ILicenseSeatClient_AllMethodsAccessible()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        ILicenseSeatClient client = new LicenseSeatClient(options, mockHttp);

        // Verify all interface methods are accessible
        Assert.NotNull(client.GetStatus());
        Assert.Null(client.GetCurrentLicense());
        Assert.False(client.HasEntitlement("test"));

        var entitlementStatus = client.CheckEntitlement("test");
        Assert.False(entitlementStatus.Active);

        // These methods should not throw
        var initException = Record.Exception(() => client.Initialize());
        Assert.Null(initException);

        var resetException = Record.Exception(() => client.Reset());
        Assert.Null(resetException);

        var purgeException = Record.Exception(() => client.PurgeCachedLicense());
        Assert.Null(purgeException);

        client.Dispose();
    }

    [Fact]
    public async Task PurgeCachedLicense_ClearsAllData()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((_, _) => new HttpResponse(200, """{"valid":true,"license":{"key":"TEST"}}"""));

        using var client = new LicenseSeatClient(options, mockHttp);
        var resetEventReceived = false;

        client.Events.On(LicenseSeatEvents.SdkReset, _ => resetEventReceived = true);

        // Activate first
        await client.ActivateAsync("TEST-KEY");
        Assert.NotNull(client.GetCurrentLicense());

        // Purge (doesn't call server, just clears local state)
        client.PurgeCachedLicense();

        Assert.True(resetEventReceived);
        Assert.Null(client.GetCurrentLicense());
    }

    [Fact]
    public void PurgeCachedLicense_WhenNoLicense_DoesNotThrow()
    {
        var options = CreateOptions();
        var mockHttp = new MockHttpClient();

        using var client = new LicenseSeatClient(options, mockHttp);

        var exception = Record.Exception(() => client.PurgeCachedLicense());

        Assert.Null(exception);
    }

    [Fact]
    public async Task PurgeCachedLicense_StopsAutoValidation()
    {
        var options = CreateOptions();
        options.AutoValidateInterval = TimeSpan.FromMilliseconds(100);
        var mockHttp = new MockHttpClient();
        mockHttp.SetupPost((_, _) => new HttpResponse(200, """{"valid":true,"license":{"key":"TEST"}}"""));

        using var client = new LicenseSeatClient(options, mockHttp);
        var autoValidationStopped = false;

        client.Events.On(LicenseSeatEvents.AutoValidationStopped, _ => autoValidationStopped = true);

        // Activate to start auto-validation
        await client.ActivateAsync("TEST-KEY");

        // Purge should stop auto-validation
        client.PurgeCachedLicense();

        Assert.True(autoValidationStopped);
    }
}
