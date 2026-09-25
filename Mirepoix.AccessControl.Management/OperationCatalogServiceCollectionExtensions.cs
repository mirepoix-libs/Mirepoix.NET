using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Registers the operation-catalog pull and decorates <see cref="IPolicySetEditor"/>.
/// </summary>
public static class OperationCatalogServiceCollectionExtensions
{
    /// <summary>
    /// Name of the <see cref="HttpClient"/> used to GET enforcement catalogs.
    /// </summary>
    public const string HttpClientName = "AccessControl.OperationCatalog";

    /// <summary>
    /// Stores <paramref name="configure"/> apps, registers the catalog client, and wraps the existing editor once.
    /// </summary>
    /// <param name="services">Collection that already contains <see cref="IPolicySetEditor"/>.</param>
    /// <param name="configure">Adds at least one enforcement app.</param>
    /// <returns><paramref name="services"/> for chaining.</returns>
    /// <remarks>
    /// Registers options, a named <see cref="HttpClient"/> (<see cref="HttpClientName"/>),
    /// <see cref="OperationCatalogSnapshot"/>, <see cref="OperationPatternValidator"/>, and
    /// <see cref="EnforcementCatalogClient"/> as <see cref="IEnforcementCatalogClient"/>.
    /// The decorator uses the same lifetime as the editor it replaces.
    /// A second call returns without wrapping again.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="configure"/> adds no apps.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown with message "AddAccessControlOperationCatalog requires IPolicySetEditor." when that service is missing.
    /// </exception>
    public static IServiceCollection AddAccessControlOperationCatalog(
        this IServiceCollection services,
        Action<OperationCatalogOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OperationCatalogOptions();
        configure(options);
        if (options.Apps.Count == 0)
            throw new ArgumentException("At least one enforcement app is required.", nameof(configure));

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ValidatingPolicySetEditor)))
            return services;

        var editor = services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IPolicySetEditor));
        if (editor is null)
            throw new InvalidOperationException("AddAccessControlOperationCatalog requires IPolicySetEditor.");

        services.AddSingleton(options);
        services.AddHttpClient(HttpClientName);
        services.AddSingleton<OperationCatalogSnapshot>();
        services.AddSingleton<OperationPatternValidator>();
        services.AddSingleton<IEnforcementCatalogClient>(provider =>
        {
            var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            return new EnforcementCatalogClient(http, provider.GetRequiredService<OperationCatalogOptions>());
        });

        services.Remove(editor);
        services.Add(new ServiceDescriptor(
            typeof(IPolicySetEditor),
            provider => new ValidatingPolicySetEditor(
                CreateEditor(provider, editor),
                provider.GetRequiredService<OperationCatalogSnapshot>(),
                provider.GetRequiredService<IEnforcementCatalogClient>(),
                provider.GetRequiredService<OperationPatternValidator>()),
            editor.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(ValidatingPolicySetEditor),
            provider => (ValidatingPolicySetEditor)provider.GetRequiredService<IPolicySetEditor>(),
            editor.Lifetime));
        return services;
    }

    private static IPolicySetEditor CreateEditor(IServiceProvider provider, ServiceDescriptor editor)
    {
        if (editor.ImplementationInstance is IPolicySetEditor instance)
            return instance;

        if (editor.ImplementationFactory is not null)
            return (IPolicySetEditor)editor.ImplementationFactory(provider);

        return (IPolicySetEditor)ActivatorUtilities.CreateInstance(provider, editor.ImplementationType!);
    }
}
