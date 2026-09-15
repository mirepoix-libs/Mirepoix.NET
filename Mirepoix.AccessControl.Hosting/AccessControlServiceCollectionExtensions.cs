using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Hosting;

public static class AccessControlServiceCollectionExtensions
{
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

public static class AccessControlEndpointExtensions
{
    /// <summary>
    /// Applies the shared AccessControl authorization policy (handler PEP).
    /// Pair with <see cref="WithAccessOperation{TBuilder}"/> metadata.
    /// </summary>
    public static TBuilder RequireAccessControl<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.RequireAuthorization(AccessControlOptions.PolicyName);
    }

    public static TBuilder WithAccessOperation<TBuilder>(this TBuilder builder, string operation)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new AccessOperationAttribute(operation));
    }

    public static TBuilder WithAccessResource<TBuilder>(
        this TBuilder builder,
        string resourceType,
        string idRouteKey = "id")
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new AccessResourceAttribute(resourceType, idRouteKey));
    }
}
