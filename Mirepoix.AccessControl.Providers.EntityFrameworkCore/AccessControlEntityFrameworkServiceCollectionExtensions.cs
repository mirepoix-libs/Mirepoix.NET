using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Registers EF Core read/hydrate adapters into DI.
/// Two modes by overload: package-driven (registers <see cref="AccessControlDbContext"/> +
/// <see cref="AccessControlSchemaApplier"/>, requires <see cref="EntityFrameworkProviderOptions.ConfigureDb"/>)
/// vs app-owned (generic <c>TContext</c>; app registers the context and owns migrations).
/// Prefer slice helpers when mixing sources; unified helpers call all three slices.
/// Seam guards prevent silent replacement. Registers <see cref="IBundleHydrator"/> once as
/// <see cref="CompositeBundleHydrator"/> with <see cref="PassThroughContextResolver"/> when absent.
/// Do not mix package-driven and app-owned registration against the same options/context.
/// </summary>
public static class AccessControlEntityFrameworkServiceCollectionExtensions
{
    /// <summary>
    /// Registers package-driven policies, subjects, and resources (shared DbContext / schema applier).
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Must set <see cref="EntityFrameworkProviderOptions.ConfigureDb"/> on first registration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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
    /// Registers app-owned policies, subjects, and resources against <typeparamref name="TContext"/>.
    /// Does not register <see cref="AccessControlDbContext"/> or <see cref="AccessControlSchemaApplier"/>.
    /// </summary>
    /// <typeparam name="TContext">App <see cref="DbContext"/> that includes the access-control model.</typeparam>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Optional cache/mapping options (<see cref="EntityFrameworkProviderOptions.ConfigureDb"/> ignored).</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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

    /// <summary>
    /// Registers an <see cref="EntityFrameworkPolicySource"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Required with <see cref="EntityFrameworkProviderOptions.ConfigureDb"/> on first call.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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

    /// <summary>
    /// Registers a <see cref="EntityFrameworkSubjectResolver"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Optional; <see cref="EntityFrameworkProviderOptions.ConfigureDb"/> required if options not yet registered.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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

    /// <summary>
    /// Registers a package-driven <see cref="EntityFrameworkResourceResolver"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Optional; <see cref="EntityFrameworkProviderOptions.ConfigureDb"/> required if options not yet registered.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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

    /// <summary>
    /// Registers an app-owned <see cref="EntityFrameworkPolicySource"/> against <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">App context type.</typeparam>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Optional options (sets <see cref="EntityFrameworkProviderOptions.ContextType"/>).</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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

    /// <summary>
    /// Registers an app-owned <see cref="EntityFrameworkSubjectResolver"/> against <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">App context type.</typeparam>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Optional options.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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

    /// <summary>
    /// Registers an app-owned <see cref="EntityFrameworkResourceResolver"/> against <typeparamref name="TContext"/>.
    /// </summary>
    /// <typeparam name="TContext">App context type.</typeparam>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Optional options.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
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
