using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Shared finalize steps for provider options after host-specific registration.
/// </summary>
public static class AccessControlProviderFinalizer
{
    /// <summary>
    /// Registers mapped resource resolvers and composite hydration when options require it.
    /// Idempotent when called multiple times on the same options instance.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="options">Configured provider options.</param>
    public static void FinalizeResources(IServiceCollection services, AccessControlProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

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
