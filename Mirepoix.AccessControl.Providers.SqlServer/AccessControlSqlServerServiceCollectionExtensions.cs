using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// DI helpers for SqlServer read/hydrate adapters.
/// Prefer slice helpers when mixing sources; the unified helper calls all three slices.
/// </summary>
public static class AccessControlSqlServerServiceCollectionExtensions
{
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

    public static IServiceCollection AddAccessControlPolicyProviders(
        this IServiceCollection services,
        Action<SqlServerProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqlServerProviderOptions();
        configure(options);
        return services.AddAccessControlPolicyProviders(options);
    }

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

    public static IServiceCollection AddAccessControlSubjectProviders(
        this IServiceCollection services,
        Action<SqlServerProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new SqlServerProviderOptions();
        configure(options);
        return services.AddAccessControlSubjectProviders(options);
    }

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
