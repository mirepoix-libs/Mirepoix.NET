using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Registers scoped EF Core management writers against the context configured by the provider package.
/// </summary>
public static class AccessControlManagementServiceCollectionExtensions
{
    /// <summary>
    /// Registers scoped management writers against the package-owned <see cref="AccessControlDbContext"/>.
    /// Requires package-driven provider registration to run first and omits role assignment management
    /// when mapped subjects own their role storage.
    /// </summary>
    /// <param name="services">DI collection containing package-driven EF provider registration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when EF providers have not been registered first or use a different context type.
    /// </exception>
    public static IServiceCollection AddAccessControlManagement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = GetProviderOptions(services);
        EnsureContextType(options, typeof(AccessControlDbContext));
        return Register<AccessControlDbContext>(services, options);
    }

    /// <summary>
    /// Registers scoped management writers against the app-owned <typeparamref name="TContext"/>.
    /// Requires generic provider registration for the same context to run first and omits role assignment
    /// management when mapped subjects own their role storage.
    /// </summary>
    /// <typeparam name="TContext">App context containing the access-control EF model.</typeparam>
    /// <param name="services">DI collection containing app-owned EF provider registration.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when EF providers have not been registered first or use a different context type.
    /// </exception>
    public static IServiceCollection AddAccessControlManagement<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = GetProviderOptions(services);
        EnsureContextType(options, typeof(TContext));
        return Register<TContext>(services, options);
    }

    private static EntityFrameworkProviderOptions GetProviderOptions(IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(
            candidate => candidate.ServiceType == typeof(EntityFrameworkProviderOptions));
        if (descriptor?.ImplementationInstance is EntityFrameworkProviderOptions options)
            return options;

        throw new InvalidOperationException(
            "Entity Framework access-control providers must be registered before management writers. " +
            "Call AddAccessControlProviders first.");
    }

    private static void EnsureContextType(EntityFrameworkProviderOptions options, Type contextType)
    {
        if (options.ContextType == contextType)
            return;

        throw new InvalidOperationException(
            $"Access-control providers are registered for context '{options.ContextType?.Name}', " +
            $"but management writers were requested for '{contextType.Name}'. " +
            "Call the matching AddAccessControlProviders overload first.");
    }

    private static IServiceCollection Register<TContext>(
        IServiceCollection services,
        EntityFrameworkProviderOptions options)
        where TContext : DbContext
    {
        services.AddScoped<IRoleCatalog>(provider =>
            new EntityFrameworkRoleCatalog(provider.GetRequiredService<TContext>()));
        services.AddScoped<ISodConstraintStore>(provider =>
            new EntityFrameworkSodConstraintStore(provider.GetRequiredService<TContext>()));
        services.AddScoped<IPolicySetEditor>(provider =>
            new EntityFrameworkPolicySetEditor(provider.GetRequiredService<TContext>()));
        services.AddScoped<IResourceStore>(provider =>
            new EntityFrameworkResourceStore(provider.GetRequiredService<TContext>()));

        var layout = SubjectStorageLayoutResolver.Resolve(options.SubjectMapping);
        if (layout is SubjectStorageLayout.Native or SubjectStorageLayout.MappedLibraryRoles)
        {
            services.AddScoped<ISubjectStore>(provider =>
                new EntityFrameworkSubjectStore(
                    provider.GetRequiredService<TContext>(),
                    provider.GetRequiredService<ISodConstraintStore>(),
                    layout));
        }

        return services;
    }
}
