using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Registers SqlServer read/hydrate adapters (<see cref="IPolicySource"/>, <see cref="ISubjectResolver"/>,
/// <see cref="IResourceResolver"/>). Prefer slice helpers when mixing sources; the unified helper calls all three slices.
/// First successful slice registration installs shared <see cref="SqlServerProviderOptions"/> and
/// <see cref="SqlServerSchemaMigrator"/>. Seam guards prevent silent replacement by another provider package.
/// Also registers <see cref="IBundleHydrator"/> once as a <see cref="CompositeBundleHydrator"/> with
/// <see cref="PassThroughContextResolver"/> when absent.
/// </summary>
public static class AccessControlSqlServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers policy + subject + resource SqlServer providers using <paramref name="options"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="options">Options including a non-empty connection string.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlProviders(
        this IServiceCollection services,
        SqlServerProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        services.AddAccessControlPolicyProviders(options);
        services.AddAccessControlSubjectProviders();
        services.AddAccessControlResourceProviders();
        return services;
    }

    /// <summary>
    /// Registers all three SqlServer slices after building options via <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Must set a non-empty connection string.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlProviders(
        this IServiceCollection services,
        Action<SqlServerProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SqlServerProviderOptions();
        configure(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        return services.AddAccessControlProviders(options);
    }

    /// <summary>
    /// Registers <see cref="SqlServerPolicySource"/> as <see cref="IPolicySource"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="options">Options with connection string (also seeds shared options on first call).</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlPolicyProviders(
        this IServiceCollection services,
        SqlServerProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);

        EnsureShared(services, options, nameof(AddAccessControlPolicyProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlPolicyProviders),
            typeof(IPolicySource),
            typeof(SqlServerPolicySource));
        services.AddSingleton<IPolicySource, SqlServerPolicySource>();
        TryAddCompositeHydrator(services);
        return services;
    }

    /// <summary>
    /// Registers the policy slice with options built from <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Options configuration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlPolicyProviders(
        this IServiceCollection services,
        Action<SqlServerProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqlServerProviderOptions();
        configure(options);
        return services.AddAccessControlPolicyProviders(options);
    }

    /// <summary>
    /// Registers <see cref="SqlServerSubjectResolver"/> as <see cref="ISubjectResolver"/>.
    /// Requires shared options already registered, or pass <paramref name="options"/> with a connection string.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="options">Optional; required with connection string on first SqlServer registration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlSubjectProviders(
        this IServiceCollection services,
        SqlServerProviderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureShared(services, options, nameof(AddAccessControlSubjectProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlSubjectProviders),
            typeof(ISubjectResolver),
            typeof(SqlServerSubjectResolver));
        services.AddSingleton<ISubjectResolver, SqlServerSubjectResolver>();
        TryAddCompositeHydrator(services);
        return services;
    }

    /// <summary>
    /// Registers the subject slice with options built from <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Options configuration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlSubjectProviders(
        this IServiceCollection services,
        Action<SqlServerProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqlServerProviderOptions();
        configure(options);
        return services.AddAccessControlSubjectProviders(options);
    }

    /// <summary>
    /// Registers <see cref="SqlServerResourceResolver"/> as <see cref="IResourceResolver"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="options">Optional; required with connection string on first SqlServer registration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlResourceProviders(
        this IServiceCollection services,
        SqlServerProviderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureShared(services, options, nameof(AddAccessControlResourceProviders));
        AccessControlServiceCollectionGuard.EnsureCanRegisterSeam(
            services,
            nameof(AddAccessControlResourceProviders),
            typeof(IResourceResolver),
            typeof(SqlServerResourceResolver));
        services.AddSingleton<IResourceResolver, SqlServerResourceResolver>();
        TryAddCompositeHydrator(services);
        return services;
    }

    /// <summary>
    /// Registers the resource slice with options built from <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Options configuration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddAccessControlResourceProviders(
        this IServiceCollection services,
        Action<SqlServerProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqlServerProviderOptions();
        configure(options);
        return services.AddAccessControlResourceProviders(options);
    }

    private static void EnsureShared(
        IServiceCollection services,
        SqlServerProviderOptions? options,
        string registrationMethod)
    {
        if (AccessControlServiceCollectionGuard.IsServiceRegistered(services, typeof(SqlServerProviderOptions)))
            return;

        if (options is null || string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                "SqlServerProviderOptions with a connection string is required on the first SqlServer slice registration. " +
                $"Pass options to {registrationMethod} (or call AddAccessControlPolicyProviders first).");
        }

        services.AddSingleton(options);
        AccessControlServiceCollectionGuard.RegisterSharedOnce(
            services,
            registrationMethod,
            typeof(SqlServerSchemaMigrator),
            typeof(SqlServerSchemaMigrator),
            () => services.AddSingleton<SqlServerSchemaMigrator>());
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
