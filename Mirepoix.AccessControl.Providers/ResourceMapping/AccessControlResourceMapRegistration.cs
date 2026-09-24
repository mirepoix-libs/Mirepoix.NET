using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Registers scoped mapped resource resolvers and composite hydration from <see cref="ResourceMappingOptions"/>.
/// </summary>
public static class AccessControlResourceMapRegistration
{
    /// <summary>
    /// Registers each map in <paramref name="options"/> as a scoped resolver and composite hydrator when needed.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="options">Configured resource maps.</param>
    public static void Apply(IServiceCollection services, ResourceMappingOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        foreach (var map in options.Maps)
        {
            RegisterMap(services, map);
        }
    }

    /// <summary>
    /// Throws when a <see cref="ResourceResolverRegistration"/> for <paramref name="type"/> is already present.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="type">Access-control resource type string.</param>
    /// <exception cref="InvalidOperationException">Thrown when the type is already registered.</exception>
    public static void ThrowIfResourceTypeAlreadyRegistered(IServiceCollection services, string type)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(ResourceResolverRegistration) &&
                descriptor.ImplementationInstance is ResourceResolverRegistration registration &&
                string.Equals(registration.Type, type, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Resource type '{type}' has more than one resolver.");
        }
    }

    private static readonly MethodInfo RegisterTypedMapMethod =
        typeof(AccessControlResourceMapRegistration)
            .GetMethod(nameof(RegisterTypedMap), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find map registration factory.");

    private static void RegisterMap(IServiceCollection services, ResourceEntityMap map)
    {
        ThrowIfResourceTypeAlreadyRegistered(services, map.Type);
        RegisterTypedMapMethod.MakeGenericMethod(map.ClrType).Invoke(null, [services, map]);
    }

    private static void RegisterTypedMap<TResource>(IServiceCollection services, ResourceEntityMap map)
        where TResource : class
    {
        services.AddScoped(serviceProvider => new MappedResourceResolver<TResource>(map, serviceProvider));
        services.AddScoped<IResourceResolver<TResource>>(serviceProvider =>
            serviceProvider.GetRequiredService<MappedResourceResolver<TResource>>());
        services.AddSingleton(new ResourceResolverRegistration
        {
            Type = map.Type,
            ResolverServiceType = typeof(MappedResourceResolver<TResource>),
            Invoke = static (serviceProvider, partial, cancellationToken) =>
                serviceProvider
                    .GetRequiredService<MappedResourceResolver<TResource>>()
                    .HydrateAsync(partial, cancellationToken),
        });
        TryAddCompositeHydrator(services);
    }

    private static void TryAddCompositeHydrator(IServiceCollection services) =>
        services.TryAddScoped<IResourceHydrator>(serviceProvider =>
            new CompositeResourceHydrator(
                serviceProvider,
                serviceProvider.GetRequiredService<IEnumerable<ResourceResolverRegistration>>()));
}
