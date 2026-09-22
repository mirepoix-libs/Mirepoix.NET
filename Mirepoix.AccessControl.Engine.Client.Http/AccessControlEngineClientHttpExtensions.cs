using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirepoix.AccessControl.Engine.Client.Http;

/// <summary>
/// Registers the remote engine HTTP access checker (PEP client).
/// </summary>
public static class AccessControlEngineClientHttpExtensions
{
    /// <summary>Named <see cref="HttpClient"/> used by <see cref="HttpAccessChecker"/>.</summary>
    public const string HttpClientName = "Mirepoix.AccessControl.Engine.Client.Http";

    /// <summary>
    /// Registers <see cref="HttpAccessChecker"/> as <see cref="IAccessChecker"/> backed by a named
    /// <see cref="HttpClient"/> pointed at the PDP.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Required; must set <see cref="AccessControlEngineClientHttpOptions.BaseAddress"/>.</param>
    public static IServiceCollection AddAccessControlEngineClientHttp(
        this IServiceCollection services,
        Action<AccessControlEngineClientHttpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AccessControlEngineClientHttpOptions();
        configure(options);

        if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                "BaseAddress must be an absolute URI for AddAccessControlEngineClientHttp.");
        }

        services.AddSingleton(options);
        services.AddHttpClient(HttpClientName, (serviceProvider, client) =>
        {
            var resolvedOptions = serviceProvider.GetRequiredService<AccessControlEngineClientHttpOptions>();
            client.BaseAddress = resolvedOptions.BaseAddress;
            resolvedOptions.ConfigureHttpClient?.Invoke(client);
        });

        services.RemoveAll<IAccessChecker>();
        services.AddSingleton<IAccessChecker>(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            return new HttpAccessChecker(factory.CreateClient(HttpClientName));
        });

        return services;
    }
}
