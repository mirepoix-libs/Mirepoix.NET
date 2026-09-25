using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Maps the enforcement catalog GET.
/// </summary>
public static class PublishedOperationEndpointExtensions
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Maps <c>GET</c> <see cref="PublishedOperation.EnforcementPath"/> to the current catalog as camelCase JSON.
    /// When <paramref name="authorizationPolicy"/> is non-empty, that endpoint requires the named policy.
    /// An empty or null policy adds no authorization requirement.
    /// </summary>
    /// <param name="endpoints">Route builder that owns the catalog route.</param>
    /// <param name="authorizationPolicy">Optional authorization policy name.</param>
    /// <returns>The mapped endpoint convention builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="endpoints"/> is null.</exception>
    public static IEndpointConventionBuilder MapAccessControlOperations(
        this IEndpointRouteBuilder endpoints,
        string? authorizationPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var mapped = endpoints.MapGet(
            PublishedOperation.EnforcementPath,
            (PublishedOperationSource source) => Results.Json(source.List(), CamelCase));

        if (!string.IsNullOrEmpty(authorizationPolicy))
            mapped.RequireAuthorization(authorizationPolicy);

        return mapped;
    }
}
