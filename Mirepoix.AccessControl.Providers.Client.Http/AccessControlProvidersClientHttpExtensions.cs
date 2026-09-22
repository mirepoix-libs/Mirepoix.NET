using Mirepoix.AccessControl.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirepoix.AccessControl.Providers.Client.Http;

/// <summary>
/// Registers remote providers HTTP resolvers (PDP → PIP client).
/// </summary>
public static class AccessControlProvidersClientHttpExtensions
{
    /// <summary>Named <see cref="HttpClient"/> used by Http*Resolver implementations.</summary>
    public const string HttpClientName = "Mirepoix.AccessControl.Providers.Client.Http";

    /// <summary>
    /// Registers enabled Http*Resolver types and TryAdds a <see cref="CompositeBundleHydrator"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configure">Required; must set <see cref="AccessControlProvidersClientHttpOptions.BaseAddress"/> and at least one slice.</param>
    public static IServiceCollection AddAccessControlProvidersClientHttp(
        this IServiceCollection services,
        Action<AccessControlProvidersClientHttpOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AccessControlProvidersClientHttpOptions();
        configure(options);

        if (options.BaseAddress is null || !options.BaseAddress.IsAbsoluteUri)
        {
            throw new InvalidOperationException(
                "BaseAddress must be an absolute URI for AddAccessControlProvidersClientHttp.");
        }

        if (!options.SubjectEnabled && !options.ResourceEnabled && !options.ContextEnabled)
        {
            throw new InvalidOperationException(
                "AddAccessControlProvidersClientHttp requires at least one slice. Call AddSubject, AddResource, and/or AddContext.");
        }

        services.AddSingleton(options);
        services.AddHttpClient(HttpClientName, (serviceProvider, client) =>
        {
            var resolvedOptions = serviceProvider.GetRequiredService<AccessControlProvidersClientHttpOptions>();
            client.BaseAddress = resolvedOptions.BaseAddress;
            resolvedOptions.ConfigureHttpClient?.Invoke(client);
        });

        if (options.SubjectEnabled)
        {
            services.RemoveAll<ISubjectResolver>();
            services.AddSingleton<ISubjectResolver>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                return new HttpSubjectResolver(factory.CreateClient(HttpClientName));
            });
        }

        if (options.ResourceEnabled)
        {
            services.RemoveAll<IResourceResolver>();
            services.AddSingleton<IResourceResolver>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                return new HttpResourceResolver(factory.CreateClient(HttpClientName));
            });
        }

        if (options.ContextEnabled)
        {
            services.RemoveAll<IContextResolver>();
            services.AddSingleton<IContextResolver>(serviceProvider =>
            {
                var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
                return new HttpContextResolver(factory.CreateClient(HttpClientName));
            });
        }

        services.TryAddSingleton<IBundleHydrator>(serviceProvider =>
            new CompositeBundleHydrator(
                serviceProvider.GetService<ISubjectResolver>(),
                serviceProvider.GetService<IResourceResolver>(),
                serviceProvider.GetService<IContextResolver>() ?? new PassThroughContextResolver()));

        return services;
    }
}
