using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Builds an <see cref="AccessPrincipalSeed"/> from the authenticated principal's claims.
/// Maps NameIdentifier/sub to subject id, Role/role claims to roles, other claims into context,
/// and sets <see cref="AccessContext.Time"/> to UTC now. Leaves subject attributes empty for hydration.
/// </summary>
public sealed class DefaultClaimsPrincipalMapper : IClaimsPrincipalMapper
{
    /// <summary>
    /// Maps <paramref name="httpContext"/>.User into subject id, roles, context claims, and UTC time.
    /// </summary>
    /// <param name="httpContext">Current HTTP request context.</param>
    /// <returns>
    /// Seed with:
    /// <list type="bullet">
    /// <item><description>Subject id from <see cref="ClaimTypes.NameIdentifier"/> or <c>sub</c> (empty string if neither).</description></item>
    /// <item><description>Roles from <see cref="ClaimTypes.Role"/> and <c>role</c> claims (empty values skipped).</description></item>
    /// <item><description>Context claims: all other claim types (last value wins on duplicate type).</description></item>
    /// <item><description>Context time: <see cref="DateTimeOffset.UtcNow"/>; Values bag empty.</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpContext"/> is null.</exception>
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
