using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Shared finalize steps for provider options after host-specific registration.
/// </summary>
public static class AccessControlProviderFinalizer
{
    /// <summary>
    /// Registers the provider attribute catalog, then mapped resource resolvers when options require them.
    /// The catalog singleton is registered even when maps are empty. Resource hydration stays idempotent
    /// on the same options instance.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="options">Configured provider options.</param>
    public static void FinalizeResources(IServiceCollection services, AccessControlProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.TryAddSingleton<IPublishedProviderAttributeSource>(
            new PublishedProviderAttributeSource(options));

        if (!options.NeedsResourceHydrator)
        {
            return;
        }

        if (options.ResourceMapsApplied)
        {
            return;
        }

        AccessControlResourceMapRegistration.Apply(services, options.ResourceMapping);
        options.MarkResourceMapsApplied();
    }
}
