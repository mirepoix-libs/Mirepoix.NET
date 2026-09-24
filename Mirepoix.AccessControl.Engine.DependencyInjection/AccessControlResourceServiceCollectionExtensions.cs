using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Registers typed domain resource resolvers and composite resource hydration.
/// </summary>
public static class AccessControlResourceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TResolver"/> as the scoped resolver for the resource type
    /// declared by its <see cref="AccessResourceTypeAttribute"/>.
    /// </summary>
    /// <typeparam name="TResolver">Typed resource resolver implementation.</typeparam>
    /// <param name="services">DI collection.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the resolver does not implement exactly one typed resolver interface,
    /// has no non-blank resource type attribute, or duplicates an existing resource type.
    /// </exception>
    public static IServiceCollection AddAccessControlResourceResolver<TResolver>(
        this IServiceCollection services)
        where TResolver : class
    {
        ArgumentNullException.ThrowIfNull(services);

        var resolverInterfaces = typeof(TResolver)
            .GetInterfaces()
            .Where(candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IResourceResolver<>))
            .ToArray();

        if (resolverInterfaces.Length != 1)
        {
            throw new InvalidOperationException(
                $"{typeof(TResolver).FullName} must implement exactly one IResourceResolver<TResource>.");
        }

        var attribute = typeof(TResolver).GetCustomAttribute<AccessResourceTypeAttribute>();
        if (attribute is null || string.IsNullOrWhiteSpace(attribute.Type))
        {
            throw new InvalidOperationException(
                $"{typeof(TResolver).FullName} must declare a non-blank {nameof(AccessResourceTypeAttribute)}.");
        }

        ThrowIfTypeAlreadyRegistered(services, attribute.Type);

        var resourceType = resolverInterfaces[0].GetGenericArguments()[0];
        var registration = CreateRegistrationMethod
            .MakeGenericMethod(typeof(TResolver), resourceType)
            .Invoke(null, [attribute.Type]) as ResourceResolverRegistration
            ?? throw new InvalidOperationException("Could not create resource resolver registration.");

        services.AddScoped<TResolver>();
        services.AddScoped(
            resolverInterfaces[0],
            serviceProvider => serviceProvider.GetRequiredService<TResolver>());
        services.AddSingleton(registration);
        TryAddCompositeHydrator(services);

        return services;
    }

    /// <summary>
    /// Registers a scoped resolver built from a fluent domain resource map.
    /// </summary>
    /// <typeparam name="TResource">Mapped domain resource type.</typeparam>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Map configuration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlResourceMap<TResource>(
        this IServiceCollection services,
        Action<ResourceEntityMapBuilder<TResource>> configure)
        where TResource : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ResourceEntityMapBuilder<TResource>();
        configure(builder);
        var map = builder.Build();
        ThrowIfTypeAlreadyRegistered(services, map.Type);

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

        return services;
    }

    private static readonly MethodInfo CreateRegistrationMethod =
        typeof(AccessControlResourceServiceCollectionExtensions)
            .GetMethod(nameof(CreateRegistration), BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not find resource registration factory.");

    private static ResourceResolverRegistration CreateRegistration<TResolver, TResource>(string type)
        where TResolver : class, IResourceResolver<TResource> =>
        new()
        {
            Type = type,
            ResolverServiceType = typeof(TResolver),
            Invoke = static (serviceProvider, partial, cancellationToken) =>
                serviceProvider
                    .GetRequiredService<TResolver>()
                    .HydrateAsync(partial, cancellationToken)
        };

    private static void ThrowIfTypeAlreadyRegistered(IServiceCollection services, string type)
    {
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(ResourceResolverRegistration) &&
                descriptor.ImplementationInstance is ResourceResolverRegistration registration &&
                string.Equals(registration.Type, type, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Resource type '{type}' has more than one resolver.");
        }
    }

    private static void TryAddCompositeHydrator(IServiceCollection services) =>
        services.TryAddScoped<IResourceHydrator>(serviceProvider =>
            new CompositeResourceHydrator(
                serviceProvider,
                serviceProvider.GetRequiredService<IEnumerable<ResourceResolverRegistration>>()));
}
