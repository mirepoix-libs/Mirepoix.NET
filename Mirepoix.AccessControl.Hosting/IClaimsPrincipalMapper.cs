using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Hosting;

/// <summary>
/// Principal-derived subject and context. Resource and operation are filled by the HTTP PEP.
/// </summary>
public sealed record AccessPrincipalSeed(Subject Subject, AccessContext Context);

public interface IClaimsPrincipalMapper
{
    AccessPrincipalSeed CreateSeed(HttpContext httpContext);
}
