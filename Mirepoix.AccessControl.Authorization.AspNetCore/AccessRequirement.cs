using Microsoft.AspNetCore.Authorization;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Marks the shared ASP.NET authorization policy handled by <see cref="AccessAuthorizationHandler"/>.
/// Carries no parameters; operation and resource come from endpoint metadata.
/// </summary>
public sealed class AccessRequirement : IAuthorizationRequirement
{
}
