using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Handles the shared <see cref="AccessRequirement"/> policy by running <see cref="AccessCheckPipeline"/>.
/// Succeeds only on Allow; Challenge and Forbid both call <c>context.Fail()</c> (ASP.NET maps authn vs 403).
/// </summary>
public sealed class AccessAuthorizationHandler : AuthorizationHandler<AccessRequirement>
{
    private readonly IClaimsPrincipalMapper _mapper;
    private readonly IAccessChecker _checker;

    /// <summary>
    /// Creates a handler bound to <paramref name="mapper"/> and <paramref name="checker"/>.
    /// </summary>
    /// <param name="mapper">Principal-to-seed mapper.</param>
    /// <param name="checker">Access decision facade.</param>
    public AccessAuthorizationHandler(IClaimsPrincipalMapper mapper, IAccessChecker checker)
    {
        _mapper = mapper;
        _checker = checker;
    }

    /// <summary>
    /// Runs the shared PEP against the HTTP context attached to <paramref name="context"/>.
    /// Fails immediately when no <see cref="HttpContext"/> can be resolved from the authorization resource.
    /// </summary>
    /// <param name="context">ASP.NET authorization context.</param>
    /// <param name="requirement">Marker requirement for the Mirepoix policy.</param>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccessRequirement requirement)
    {
        var httpContext = TryGetHttpContext(context);
        if (httpContext is null)
        {
            context.Fail();
            return;
        }

        var (outcome, _) = await AccessCheckPipeline
            .EvaluateAsync(httpContext, _mapper, _checker, httpContext.RequestAborted)
            .ConfigureAwait(false);

        switch (outcome)
        {
            case AccessCheckPipeline.Outcome.Allow:
                context.Succeed(requirement);
                break;
            case AccessCheckPipeline.Outcome.Challenge:
            case AccessCheckPipeline.Outcome.Forbid:
            default:
                context.Fail();
                break;
        }
    }

    /// <summary>
    /// Resolves <see cref="HttpContext"/> from <paramref name="context"/>.Resource
    /// (direct <see cref="HttpContext"/> or MVC <c>AuthorizationFilterContext</c>).
    /// </summary>
    private static HttpContext? TryGetHttpContext(AuthorizationHandlerContext context)
    {
        if (context.Resource is HttpContext http)
            return http;

        if (context.Resource is Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext mvc)
            return mvc.HttpContext;

        return null;
    }
}
