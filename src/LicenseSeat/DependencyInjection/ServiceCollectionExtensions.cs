using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LicenseSeat;

/// <summary>
/// Extension methods for adding LicenseSeat services to an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the LicenseSeat client to the service collection as a singleton.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">The API key for authentication.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddLicenseSeatClient("your-api-key");
    /// </code>
    /// </example>
    public static IServiceCollection AddLicenseSeatClient(
        this IServiceCollection services,
        string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be empty", nameof(apiKey));
        }

        return services.AddLicenseSeatClient(options => options.ApiKey = apiKey);
    }

    /// <summary>
    /// Adds the LicenseSeat client to the service collection as a singleton with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the client options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddLicenseSeatClient(options => {
    ///     options.ApiKey = "your-api-key";
    ///     options.ApiBaseUrl = "https://custom.api.com";
    ///     options.Debug = true;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLicenseSeatClient(
        this IServiceCollection services,
        Action<LicenseSeatClientOptions> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        services.Configure(configure);

        services.TryAddSingleton<ILicenseSeatClient>(sp =>
        {
            var optionsSnapshot = sp.GetRequiredService<IOptions<LicenseSeatClientOptions>>();
            return new LicenseSeatClient(optionsSnapshot.Value);
        });

        return services;
    }

    /// <summary>
    /// Adds the LicenseSeat client to the service collection as a singleton with the provided options instance.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The pre-configured options instance.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// var options = new LicenseSeatClientOptions("your-api-key")
    /// {
    ///     Debug = true,
    ///     AutoValidateInterval = TimeSpan.FromMinutes(30)
    /// };
    /// services.AddLicenseSeatClient(options);
    /// </code>
    /// </example>
    public static IServiceCollection AddLicenseSeatClient(
        this IServiceCollection services,
        LicenseSeatClientOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        services.TryAddSingleton<ILicenseSeatClient>(new LicenseSeatClient(options));

        return services;
    }

    /// <summary>
    /// Adds the LicenseSeat client to the service collection using a factory function.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">Factory function to create the client.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddLicenseSeatClient(sp => {
    ///     var config = sp.GetRequiredService&lt;IConfiguration&gt;();
    ///     return new LicenseSeatClient(new LicenseSeatClientOptions(config["LicenseSeat:ApiKey"]));
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddLicenseSeatClient(
        this IServiceCollection services,
        Func<IServiceProvider, ILicenseSeatClient> factory)
    {
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }

        services.TryAddSingleton(factory);

        return services;
    }
}
