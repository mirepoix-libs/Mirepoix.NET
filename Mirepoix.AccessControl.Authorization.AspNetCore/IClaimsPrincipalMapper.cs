using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Carries the principal-derived <see cref="Subject"/> and <see cref="AccessContext"/> for one HTTP check.
/// Resource and operation are filled later by the HTTP PEP from endpoint metadata.
/// </summary>
/// <param name="Subject">Subject built from the authenticated principal (often id + claim roles only).</param>
/// <param name="Context">Context built from the principal (Hosting supplies UTC time and non-role claims).</param>
public sealed record AccessPrincipalSeed(Subject Subject, AccessContext Context);

/// <summary>
/// Maps an <see cref="HttpContext"/> principal into an <see cref="AccessPrincipalSeed"/>.
/// Does not resolve resource or operation; PEPs add those before calling <see cref="IAccessChecker"/>.
/// </summary>
public interface IClaimsPrincipalMapper
{
    /// <summary>
    /// Builds subject and context from <paramref name="httpContext"/>.User (and related request state).
    /// </summary>
    /// <param name="httpContext">Current HTTP request context.</param>
    /// <returns>Seed used to construct an <see cref="AuthorizationRequest"/>.</returns>
    AccessPrincipalSeed CreateSeed(HttpContext httpContext);
}
