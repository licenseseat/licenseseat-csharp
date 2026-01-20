using System;
using System.Threading.Tasks;

namespace LicenseSeat.Tests;

public class StaticApiTests : IDisposable
{
    public StaticApiTests()
    {
        // Ensure clean state before each test
        LicenseSeat.Shutdown();
    }

    public void Dispose()
    {
        // Clean up after each test
        LicenseSeat.Shutdown();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Configure_WithApiKey_CreatesSharedInstance()
    {
        var client = LicenseSeat.Configure("test-api-key", options =>
        {
            options.AutoInitialize = false;
        });

        Assert.NotNull(client);
        Assert.NotNull(LicenseSeat.Shared);
        Assert.True(LicenseSeat.IsConfigured);
    }

    [Fact]
    public void Configure_WithEmptyApiKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => LicenseSeat.Configure(""));
    }

    [Fact]
    public void Configure_WithNullApiKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => LicenseSeat.Configure((string)null!));
    }

    [Fact]
    public void Configure_WithOptions_CreatesSharedInstance()
    {
        var options = new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            AutoInitialize = false
        };

        var client = LicenseSeat.Configure(options);

        Assert.NotNull(client);
        Assert.NotNull(LicenseSeat.Shared);
    }

    [Fact]
    public void Configure_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => LicenseSeat.Configure((LicenseSeatClientOptions)null!));
    }

    [Fact]
    public void Configure_CalledTwiceWithoutForce_ReturnsSameInstance()
    {
        var options1 = new LicenseSeatClientOptions { ApiKey = "key1", AutoInitialize = false };
        var options2 = new LicenseSeatClientOptions { ApiKey = "key2", AutoInitialize = false };

        var client1 = LicenseSeat.Configure(options1);
        var client2 = LicenseSeat.Configure(options2, force: false);

        Assert.Same(client1, client2);
    }

    [Fact]
    public void Configure_CalledTwiceWithForce_ReplacesInstance()
    {
        var options1 = new LicenseSeatClientOptions { ApiKey = "key1", AutoInitialize = false };
        var options2 = new LicenseSeatClientOptions { ApiKey = "key2", AutoInitialize = false };

        var client1 = LicenseSeat.Configure(options1);
        var client2 = LicenseSeat.Configure(options2, force: true);

        Assert.NotSame(client1, client2);
    }

    [Fact]
    public void Shared_BeforeConfigure_ReturnsNull()
    {
        Assert.Null(LicenseSeat.Shared);
        Assert.False(LicenseSeat.IsConfigured);
    }

    [Fact]
    public async Task Activate_BeforeConfigure_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LicenseSeat.Activate("test-key"));
        Assert.Contains("Configure", ex.Message);
    }

    [Fact]
    public async Task Validate_BeforeConfigure_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LicenseSeat.Validate("test-key"));
        Assert.Contains("Configure", ex.Message);
    }

    [Fact]
    public async Task Deactivate_BeforeConfigure_ThrowsInvalidOperationException()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => LicenseSeat.Deactivate());
        Assert.Contains("Configure", ex.Message);
    }

    [Fact]
    public void GetStatus_BeforeConfigure_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => LicenseSeat.GetStatus());
    }

    [Fact]
    public void Entitlement_BeforeConfigure_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => LicenseSeat.Entitlement("test"));
    }

    [Fact]
    public void HasEntitlement_BeforeConfigure_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => LicenseSeat.HasEntitlement("test"));
    }

    [Fact]
    public void GetCurrentLicense_BeforeConfigure_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => LicenseSeat.GetCurrentLicense());
    }

    [Fact]
    public void Reset_BeforeConfigure_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => LicenseSeat.Reset());
    }

    [Fact]
    public void GetStatus_AfterConfigure_ReturnsStatus()
    {
        LicenseSeat.Configure("test-api-key", options => options.AutoInitialize = false);

        var status = LicenseSeat.GetStatus();

        Assert.Equal(LicenseStatusType.Inactive, status.StatusType);
    }

    [Fact]
    public void Shutdown_DisposesSharedInstance()
    {
        LicenseSeat.Configure("test-api-key", options => options.AutoInitialize = false);
        Assert.True(LicenseSeat.IsConfigured);

        LicenseSeat.Shutdown();

        Assert.False(LicenseSeat.IsConfigured);
        Assert.Null(LicenseSeat.Shared);
    }

    [Fact]
    public void Shutdown_WhenNotConfigured_DoesNotThrow()
    {
        var exception = Record.Exception(() => LicenseSeat.Shutdown());
        Assert.Null(exception);
    }
}
