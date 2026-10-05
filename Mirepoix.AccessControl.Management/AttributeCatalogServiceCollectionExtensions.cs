using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Registers the attribute-catalog pull, snapshot, and validator.
/// </summary>
public static class AttributeCatalogServiceCollectionExtensions
{
    /// <summary>
    /// Name of the <see cref="HttpClient"/> used to GET attribute catalogs.
    /// </summary>
    public const string HttpClientName = "AccessControl.AttributeCatalog";

    /// <summary>
    /// Stores <paramref name="configure"/> apps and registers the attribute catalog services.
    /// </summary>
    /// <param name="services">Collection that receives the catalog services.</param>
    /// <param name="configure">Adds provider and enforcement apps. An empty list is allowed.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// Registers <see cref="AttributeCatalogOptions"/>, a named <see cref="HttpClient"/> (<see cref="HttpClientName"/>),
    /// <see cref="AttributeCatalogSnapshot"/>, <see cref="AttributeCatalogValidator"/>, and
    /// <see cref="AttributeCatalogClient"/> as <see cref="IAttributeCatalogClient"/>.
    /// <see cref="AttributeCatalogOptions.ConfigureHttpClient"/> runs against that named client when it is not null.
    /// When <see cref="IPolicySetEditor"/> is already registered, wraps it once with
    /// <see cref="ValidatingPolicySetEditor"/> (the same decorator as
    /// <see cref="OperationCatalogServiceCollectionExtensions.AddAccessControlOperationCatalog"/>).
    /// A missing editor does not throw; the pull services are still registered.
    /// A second call returns without replacing the first registration.
    /// </remarks>
    public static IServiceCollection AddAccessControlAttributeCatalog(
        this IServiceCollection services,
        Action<AttributeCatalogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(AttributeCatalogOptions)))
            return services;

        var options = new AttributeCatalogOptions();
        configure(options);

        services.AddSingleton(options);
        var builder = services.AddHttpClient(HttpClientName);
        if (options.ConfigureHttpClient is not null)
            builder.ConfigureHttpClient(options.ConfigureHttpClient);

        services.AddSingleton<AttributeCatalogSnapshot>();
        services.AddSingleton<AttributeCatalogValidator>();
        services.AddSingleton<IAttributeCatalogClient>(provider =>
        {
            var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            return new AttributeCatalogClient(client, provider.GetRequiredService<AttributeCatalogOptions>(), provider);
        });
        services.EnsureValidatingPolicySetEditor();
        return services;
    }
}
