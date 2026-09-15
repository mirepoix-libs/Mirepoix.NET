using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Hosting;

public sealed class AccessAuthorizationHandler : AuthorizationHandler<AccessRequirement>
{
    private readonly IClaimsPrincipalMapper _mapper;
    private readonly IAccessChecker _checker;

    public AccessAuthorizationHandler(IClaimsPrincipalMapper mapper, IAccessChecker checker)
    {
        _mapper = mapper;
        _checker = checker;
    }

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

    private static HttpContext? TryGetHttpContext(AuthorizationHandlerContext context)
    {
        if (context.Resource is HttpContext http)
            return http;

        if (context.Resource is Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext mvc)
            return mvc.HttpContext;

        return null;
    }
}
