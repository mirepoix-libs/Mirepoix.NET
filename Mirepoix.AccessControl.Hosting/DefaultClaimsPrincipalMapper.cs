using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Hosting;

public sealed class DefaultClaimsPrincipalMapper : IClaimsPrincipalMapper
{
    public AccessPrincipalSeed CreateSeed(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var user = httpContext.User;
        var id =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
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

        var subject = new Subject(id, roles, new Dictionary<string, object?>());
        var context = new AccessContext(
            DateTimeOffset.UtcNow,
            claims,
            new Dictionary<string, object?>());

        return new AccessPrincipalSeed(subject, context);
    }
}
