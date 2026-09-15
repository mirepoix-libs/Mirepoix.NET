using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// DI helpers for EF Core read/hydrate adapters.
/// Pair with Hosting <c>AddAccessControl</c> for HTTP PEPs.
/// Prefer slice helpers when mixing sources; the unified helpers call all three slices.
/// </summary>
public static class AccessControlEntityFrameworkServiceCollectionExtensions
{
    /// <summary>
    /// Package-driven: policies + subjects + resources (+ shared DbContext / schema applier).
    /// </summary>
    public static IServiceCollection AddAccessControlProviders(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddAccessControlPolicyProviders(configure);
        services.AddAccessControlSubjectProviders();
        services.AddAccessControlResourceProviders();
        return services;
    }

    /// <summary>
    /// App-owned: policies + subjects + resources against <typeparamref name="TContext"/>.
    /// </summary>
    public static IServiceCollection AddAccessControlProviders<TContext>(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAccessControlPolicyProviders<TContext>(configure);
        services.AddAccessControlSubjectProviders<TContext>();
        services.AddAccessControlResourceProviders<TContext>();
        return services;
    }

    public static IServiceCollection AddAccessControlPolicyProviders(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsurePackageDrivenShared(services, configure, nameof(AddAccessControlPolicyProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlPolicyProviders),
            typeof(IPolicySource),
            typeof(EntityFrameworkPolicySource));
        services.AddSingleton<IPolicySource, EntityFrameworkPolicySource>();
        TryAddCompositeHydrator(services);
        return services;
    }

    public static IServiceCollection AddAccessControlSubjectProviders(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsurePackageDrivenShared(services, configure, nameof(AddAccessControlSubjectProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlSubjectProviders),
            typeof(ISubjectResolver),
            typeof(EntityFrameworkSubjectResolver));
        services.AddSingleton<ISubjectResolver, EntityFrameworkSubjectResolver>();
        TryAddCompositeHydrator(services);
        return services;
    }

    public static IServiceCollection AddAccessControlResourceProviders(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsurePackageDrivenShared(services, configure, nameof(AddAccessControlResourceProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlResourceProviders),
            typeof(IResourceResolver),
            typeof(EntityFrameworkResourceResolver));
        services.AddSingleton<IResourceResolver, EntityFrameworkResourceResolver>();
        TryAddCompositeHydrator(services);
        return services;
    }

    public static IServiceCollection AddAccessControlPolicyProviders<TContext>(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureAppOwnedShared<TContext>(services, configure, nameof(AddAccessControlPolicyProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlPolicyProviders),
            typeof(IPolicySource),
            typeof(EntityFrameworkPolicySource));
        services.AddSingleton<IPolicySource, EntityFrameworkPolicySource>();
        TryAddCompositeHydrator(services);
        return services;
    }

    public static IServiceCollection AddAccessControlSubjectProviders<TContext>(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureAppOwnedShared<TContext>(services, configure, nameof(AddAccessControlSubjectProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlSubjectProviders),
            typeof(ISubjectResolver),
            typeof(EntityFrameworkSubjectResolver));
        services.AddSingleton<ISubjectResolver, EntityFrameworkSubjectResolver>();
        TryAddCompositeHydrator(services);
        return services;
    }

    public static IServiceCollection AddAccessControlResourceProviders<TContext>(
        this IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure = null)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureAppOwnedShared<TContext>(services, configure, nameof(AddAccessControlResourceProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlResourceProviders),
            typeof(IResourceResolver),
            typeof(EntityFrameworkResourceResolver));
        services.AddSingleton<IResourceResolver, EntityFrameworkResourceResolver>();
        TryAddCompositeHydrator(services);
        return services;
    }

    private static void EnsurePackageDrivenShared(
        IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure,
        string registrationMethod)
    {
        if (AccessControlServiceCollectionGuard.IsServiceRegistered(services, typeof(EntityFrameworkProviderOptions)))
        {
            EnsureExistingOptionsCompatible(services, typeof(AccessControlDbContext), registrationMethod);
            return;
        }

        var options = new EntityFrameworkProviderOptions();
        configure?.Invoke(options);
        if (options.ConfigureDb is null)
        {
            throw new InvalidOperationException(
                "ConfigureDb is required for package-driven Entity Framework registration. " +
                $"Pass it on the first slice call (e.g. {registrationMethod}), " +
                "or use AddAccessControlProviders<TContext> for app-owned migrations.");
        }

        options.ContextType = typeof(AccessControlDbContext);
        services.AddSingleton(options);
        services.AddDbContext<AccessControlDbContext>(options.ConfigureDb);
        AccessControlServiceCollectionGuard.RegisterSharedOnce(
            services,
            registrationMethod,
            typeof(AccessControlSchemaApplier),
            typeof(AccessControlSchemaApplier),
            () => services.AddSingleton<AccessControlSchemaApplier>());
    }

    private static void EnsureAppOwnedShared<TContext>(
        IServiceCollection services,
        Action<EntityFrameworkProviderOptions>? configure,
        string registrationMethod)
        where TContext : DbContext
    {
        if (AccessControlServiceCollectionGuard.IsServiceRegistered(services, typeof(EntityFrameworkProviderOptions)))
        {
            EnsureExistingOptionsCompatible(services, typeof(TContext), registrationMethod);
            return;
        }

        var options = new EntityFrameworkProviderOptions();
        configure?.Invoke(options);
        options.ContextType = typeof(TContext);
        services.AddSingleton(options);
    }

    private static void EnsureExistingOptionsCompatible(
        IServiceCollection services,
        Type expectedContextType,
        string registrationMethod)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(EntityFrameworkProviderOptions))
                continue;

            if (descriptor.ImplementationInstance is EntityFrameworkProviderOptions existing)
            {
                if (existing.ContextType == expectedContextType)
                    return;

                throw new InvalidOperationException(
                    $"EntityFrameworkProviderOptions is already registered for context '{existing.ContextType?.Name}'. " +
                    $"Cannot use {registrationMethod} with '{expectedContextType.Name}'.");
            }

            throw new InvalidOperationException(
                $"EntityFrameworkProviderOptions is already registered. Cannot call {registrationMethod}.");
        }
    }

    private static void TryAddCompositeHydrator(IServiceCollection services)
    {
        AccessControlServiceCollectionGuard.RegisterSharedOnce(
            services,
            nameof(AddAccessControlProviders),
            typeof(IBundleHydrator),
            expectedImplementation: null,
            () => services.AddSingleton<IBundleHydrator>(sp =>
                new CompositeBundleHydrator(
                    sp.GetService<ISubjectResolver>(),
                    sp.GetService<IResourceResolver>(),
                    new PassThroughContextResolver())));
    }
}
