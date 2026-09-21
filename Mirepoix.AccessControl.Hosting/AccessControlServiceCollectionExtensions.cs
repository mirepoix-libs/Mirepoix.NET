using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Hosting;

/// <summary>
/// Registers AccessControl Hosting services via <see cref="AccessControlBuilder"/>.
/// </summary>
public static class AccessControlServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Hosting PEP stack and <see cref="IAccessChecker"/>, then completes the builder.
    /// Requires an <see cref="IPolicySource"/> either via <paramref name="configure"/>
    /// (<see cref="AccessControlBuilder.UseMemoryPolicySet"/> / <see cref="AccessControlBuilder.UsePolicySource{TSource}"/>)
    /// or already present on <paramref name="services"/>.
    /// </summary>
    /// <param name="services">Application service collection.</param>
    /// <param name="configure">Optional builder configuration (policy source, hydrator, strategy, mapper).</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown by <see cref="AccessControlBuilder"/> completion when no <see cref="IPolicySource"/> is registered.
    /// </exception>
    public static IServiceCollection AddAccessControl(
        this IServiceCollection services,
        Action<AccessControlBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var builder = new AccessControlBuilder(services);
        configure?.Invoke(builder);
        builder.Complete();
        return services;
    }
}

/// <summary>
/// Attaches AccessControl policy and metadata to minimal API / endpoint builders.
/// </summary>
public static class AccessControlEndpointExtensions
{
    /// <summary>
    /// Applies the shared AccessControl authorization policy (handler PEP).
    /// Pair with <see cref="WithAccessOperation{TBuilder}"/> (and optionally <see cref="WithAccessResource{TBuilder}"/>).
    /// </summary>
    /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
    /// <param name="builder">Endpoint being configured.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static TBuilder RequireAccessControl<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(AccessControlOptions.PolicyName);
    }

    /// <summary>
    /// Adds <see cref="AccessOperationAttribute"/> metadata required by the HTTP PEP.
    /// </summary>
    /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
    /// <param name="builder">Endpoint being configured.</param>
    /// <param name="operation">Operation string (same format as <see cref="Operation.Parse"/>).</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static TBuilder WithAccessOperation<TBuilder>(this TBuilder builder, string operation)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new AccessOperationAttribute(operation));
    }

    /// <summary>
    /// Adds <see cref="AccessResourceAttribute"/> metadata used to build a partial resource from route values.
    /// </summary>
    /// <typeparam name="TBuilder">Endpoint convention builder type.</typeparam>
    /// <param name="builder">Endpoint being configured.</param>
    /// <param name="resourceType">Resource type segment.</param>
    /// <param name="idRouteKey">Route value name for the resource id. Defaults to <c>id</c>.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static TBuilder WithAccessResource<TBuilder>(
        this TBuilder builder,
        string resourceType,
        string idRouteKey = "id")
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new AccessResourceAttribute(resourceType, idRouteKey));
    }
}
