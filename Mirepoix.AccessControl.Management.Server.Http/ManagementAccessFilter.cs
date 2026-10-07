using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Management.Server.Http;

/// <summary>
/// Returns 401 for an unauthenticated principal, then calls <see cref="IAccessChecker"/> before the handler.
/// </summary>
internal static class ManagementAccessFilter
{
    /// <summary>
    /// Stops the request when the principal is anonymous or the decision is not Allow with <see cref="DecisionStatus.Success"/>.
    /// </summary>
    /// <param name="context">Current endpoint invocation, including <see cref="HttpContext.User"/> and route values.</param>
    /// <param name="next">Handler invoked only after an authenticated Allow with success status.</param>
    /// <returns>401, 403, or the handler result.</returns>
    public static async ValueTask<object?> Invoke(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true)
            return Results.Unauthorized();

        var access = http.GetEndpoint()?.Metadata.GetMetadata<ManagementRouteAccess>();
        if (access is null)
            return Results.Forbid();

        var (subject, accessContext) = MapPrincipal(http.User);
        var resourceId = string.Empty;
        if (access.IdRouteKey is not null &&
            http.Request.RouteValues.TryGetValue(access.IdRouteKey, out var raw) &&
            raw is not null)
        {
            resourceId = raw.ToString() ?? string.Empty;
        }

        var checker = http.RequestServices.GetRequiredService<IAccessChecker>();
        var decision = await checker.CheckAsync(
            new AuthorizationRequest(
                subject,
                new Resource(
                    access.ResourceType,
                    string.IsNullOrWhiteSpace(resourceId) ? ResourceKey.Empty : ResourceKey.Single(resourceId),
                    new Dictionary<string, object?>()),
                Operation.Parse(access.Operation),
                accessContext),
            http.RequestAborted).ConfigureAwait(false);

        if (decision.Result == AuthorizationResult.Allow && decision.Status == DecisionStatus.Success)
            return await next(context).ConfigureAwait(false);

        return Results.Forbid();
    }

    private static (Subject Subject, AccessContext Context) MapPrincipal(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? string.Empty;

        var roles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in user.FindAll(ClaimTypes.Role))
        {
            if (!string.IsNullOrEmpty(claim.Value))
                roles.Add(claim.Value);
        }

        foreach (var claim in user.FindAll("role"))
        {
            if (!string.IsNullOrEmpty(claim.Value))
                roles.Add(claim.Value);
        }

        var claims = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var claim in user.Claims)
        {
            if (claim.Type is ClaimTypes.NameIdentifier or ClaimTypes.Role or "sub" or "role")
                continue;

            claims[claim.Type] = claim.Value;
        }

        return (
            new Subject(id, roles, new Dictionary<string, object?>()),
            new AccessContext(DateTimeOffset.UtcNow, claims, new Dictionary<string, object?>()));
    }
}
