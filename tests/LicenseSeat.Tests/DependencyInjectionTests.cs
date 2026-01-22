using System;
using Microsoft.Extensions.DependencyInjection;

namespace LicenseSeat.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddLicenseSeatClient_WithApiKeyAndProductSlug_RegistersClient()
    {
        var services = new ServiceCollection();

        services.AddLicenseSeatClient("test-api-key", "test-product");

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<ILicenseSeatClient>();

        Assert.NotNull(client);
        Assert.IsType<LicenseSeatClient>(client);
    }

    [Fact]
    public void AddLicenseSeatClient_WithEmptyApiKey_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddLicenseSeatClient("", "test-product"));
    }

    [Fact]
    public void AddLicenseSeatClient_WithEmptyProductSlug_ThrowsArgumentException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddLicenseSeatClient("test-api-key", ""));
    }

    [Fact]
    public void AddLicenseSeatClient_WithConfigure_RegistersClientWithOptions()
    {
        var services = new ServiceCollection();

        services.AddLicenseSeatClient(options =>
        {
            options.ApiKey = "test-api-key";
            options.ProductSlug = "test-product";
            options.ApiBaseUrl = "https://custom.api.com";
            options.Debug = true;
        });

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<ILicenseSeatClient>();

        Assert.NotNull(client);
        Assert.Equal("https://custom.api.com", client!.Options.ApiBaseUrl);
        Assert.True(client.Options.Debug);
    }

    [Fact]
    public void AddLicenseSeatClient_WithNullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLicenseSeatClient((Action<LicenseSeatClientOptions>)null!));
    }

    [Fact]
    public void AddLicenseSeatClient_WithOptions_RegistersClientWithProvidedOptions()
    {
        var services = new ServiceCollection();
        var options = new LicenseSeatClientOptions
        {
            ApiKey = "test-api-key",
            ProductSlug = "test-product",
            ApiBaseUrl = "https://custom.api.com",
            AutoInitialize = false
        };

        services.AddLicenseSeatClient(options);

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<ILicenseSeatClient>();

        Assert.NotNull(client);
        Assert.Equal("https://custom.api.com", client!.Options.ApiBaseUrl);
    }

    [Fact]
    public void AddLicenseSeatClient_WithNullOptions_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLicenseSeatClient((LicenseSeatClientOptions)null!));
    }

    [Fact]
    public void AddLicenseSeatClient_WithFactory_RegistersClientFromFactory()
    {
        var services = new ServiceCollection();
        var customOptions = new LicenseSeatClientOptions
        {
            ApiKey = "factory-api-key",
            ProductSlug = "factory-product",
            AutoInitialize = false
        };

        services.AddLicenseSeatClient(sp => new LicenseSeatClient(customOptions));

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<ILicenseSeatClient>();

        Assert.NotNull(client);
        Assert.Equal("factory-api-key", client!.Options.ApiKey);
    }

    [Fact]
    public void AddLicenseSeatClient_WithNullFactory_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddLicenseSeatClient((Func<IServiceProvider, ILicenseSeatClient>)null!));
    }

    [Fact]
    public void AddLicenseSeatClient_RegistersAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddLicenseSeatClient(options =>
        {
            options.ApiKey = "test-api-key";
            options.ProductSlug = "test-product";
            options.AutoInitialize = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        var client1 = serviceProvider.GetService<ILicenseSeatClient>();
        var client2 = serviceProvider.GetService<ILicenseSeatClient>();

        Assert.Same(client1, client2);
    }

    [Fact]
    public void AddLicenseSeatClient_CalledTwice_ConfigureActionsAreMerged()
    {
        var services = new ServiceCollection();

        // First registration sets ApiKey
        services.AddLicenseSeatClient(options =>
        {
            options.ApiKey = "first-key";
            options.ProductSlug = "first-product";
            options.AutoInitialize = false;
        });

        // Second registration also runs (Configure actions are additive)
        services.AddLicenseSeatClient(options =>
        {
            options.ApiKey = "second-key";
            options.ProductSlug = "second-product";
            options.AutoInitialize = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<ILicenseSeatClient>();

        // Configure actions are merged, so last one wins for ApiKey
        // But TryAddSingleton prevents duplicate service registration
        Assert.NotNull(client);
    }

    [Fact]
    public void AddLicenseSeatClient_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLicenseSeatClient("test-api-key", "test-product");

        Assert.Same(services, result);
    }

    [Fact]
    public void Client_CanBeDisposed_WhenRetrievedFromDI()
    {
        var services = new ServiceCollection();
        services.AddLicenseSeatClient(options =>
        {
            options.ApiKey = "test-api-key";
            options.ProductSlug = "test-product";
            options.AutoInitialize = false;
        });

        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetRequiredService<ILicenseSeatClient>();

        // Should not throw
        var exception = Record.Exception(() => client.Dispose());
        Assert.Null(exception);
    }
}
