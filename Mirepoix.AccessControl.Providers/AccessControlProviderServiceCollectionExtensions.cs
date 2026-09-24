using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Agnostic access-control provider registration (resource mapping only; no policy or subject adapters).
/// </summary>
public static class AccessControlProviderServiceCollectionExtensions
{
    /// <summary>
    /// Registers mapped resource hydration from fluent MapResource configuration.
    /// Subject mapping and resolver registration require EF, SqlServer, or a custom <see cref="ISubjectResolver"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Resource map configuration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when MapSubject or AddSubjectResolver was used on agnostic options.
    /// </exception>
    public static IServiceCollection AddAccessControlProviders(
        this IServiceCollection services,
        Action<AgnosticAccessControlProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new AgnosticAccessControlProviderOptions();
        configure(options);

        if (options.SubjectMapping.HasMaps || options.SubjectResolverRequested)
        {
            throw new InvalidOperationException(
                "MapSubject / AddSubjectResolver requires EF or SqlServer AddAccessControlProviders (or a custom ISubjectResolver registration).");
        }

        AccessControlProviderFinalizer.FinalizeResources(services, options);
        return services;
    }
}
