using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Builds the enforcement catalog from endpoint metadata plus <see cref="PublishedOperationList"/>.
/// Each read walks the current <see cref="EndpointDataSource"/> set, so routes mapped after startup are included.
/// </summary>
public sealed class PublishedOperationSource
{
    private readonly IEnumerable<EndpointDataSource> _endpoints;
    private readonly PublishedOperationList _registrations;

    /// <summary>
    /// Captures the live endpoint sources and the code-registration list.
    /// </summary>
    /// <param name="endpoints">Every endpoint data source in DI. Empty when the host has not mapped routes.</param>
    /// <param name="registrations">Concrete strings from <c>AddAccessControlOperation</c>.</param>
    public PublishedOperationSource(
        IEnumerable<EndpointDataSource> endpoints,
        PublishedOperationList registrations)
    {
        _endpoints = endpoints;
        _registrations = registrations;
    }

    /// <summary>
    /// Lists one <see cref="PublishedOperation"/> per <see cref="AccessOperationAttribute"/>, then one per code registration.
    /// A single HTTP method is stored as that method. Several methods are joined with <c>,</c>.
    /// Missing route or method metadata becomes an empty string. Code registrations always use empty route and method.
    /// </summary>
    /// <returns>The catalog in endpoint order, then registration order.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an endpoint operation is blank or any segment equals <c>*</c>.
    /// </exception>
    public IReadOnlyList<PublishedOperation> List()
    {
        var published = new List<PublishedOperation>();
        foreach (var source in _endpoints)
        {
            foreach (var endpoint in source.Endpoints)
            {
                foreach (var attribute in endpoint.Metadata.GetOrderedMetadata<AccessOperationAttribute>())
                {
                    RejectUnpublishable(attribute.Operation);
                    published.Add(new PublishedOperation(
                        attribute.Operation,
                        RouteTemplate(endpoint),
                        HttpMethod(endpoint)));
                }
            }
        }

        foreach (var operation in _registrations.Operations)
            published.Add(new PublishedOperation(operation, string.Empty, string.Empty));

        return published;
    }

    /// <summary>
    /// Throws when <paramref name="operation"/> is blank or contains a <c>*</c> segment.
    /// Wildcards belong on policy patterns, not on published operations.
    /// </summary>
    /// <param name="operation">Raw attribute or registration string.</param>
    /// <exception cref="InvalidOperationException">Thrown when the string cannot be published.</exception>
    internal static void RejectUnpublishable(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation) || operation.Split(':').Any(segment => segment == "*"))
        {
            throw new InvalidOperationException(
                $"Operation '{operation}' cannot be published. Use a concrete value with no * segment.");
        }
    }

    private static string RouteTemplate(Endpoint endpoint) =>
        endpoint is RouteEndpoint route
            ? route.RoutePattern.RawText ?? string.Empty
            : string.Empty;

    private static string HttpMethod(Endpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        if (methods is null || methods.Count == 0)
            return string.Empty;

        return methods.Count == 1 ? methods[0] : string.Join(",", methods);
    }
}
