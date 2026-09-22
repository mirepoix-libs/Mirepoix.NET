using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Authorization.AspNetCore;

/// <summary>
/// Runs the shared HTTP PEP as an endpoint filter (minimal APIs / endpoint pipeline).
/// Continues on Allow; returns Unauthorized on Challenge and Forbid on other failures.
/// </summary>
public sealed class AccessEndpointFilter : IEndpointFilter
{
    private readonly IClaimsPrincipalMapper _mapper;
    private readonly IAccessChecker _checker;

    /// <summary>
    /// Creates a filter bound to <paramref name="mapper"/> and <paramref name="checker"/>.
    /// </summary>
    /// <param name="mapper">Principal-to-seed mapper.</param>
    /// <param name="checker">Access decision facade.</param>
    public AccessEndpointFilter(IClaimsPrincipalMapper mapper, IAccessChecker checker)
    {
        _mapper = mapper;
        _checker = checker;
    }

    /// <summary>
    /// Evaluates access before invoking <paramref name="next"/>.
    /// </summary>
    /// <param name="context">Endpoint filter invocation context.</param>
    /// <param name="next">Next filter / endpoint delegate.</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><description>Allow: result of <paramref name="next"/>.</description></item>
    /// <item><description>Challenge: <see cref="Results.Unauthorized"/>.</description></item>
    /// <item><description>Forbid (and other non-Allow): <see cref="Results.Forbid"/>.</description></item>
    /// </list>
    /// </returns>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var (outcome, _) = await AccessCheckPipeline
            .EvaluateAsync(context.HttpContext, _mapper, _checker, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        return outcome switch
        {
            AccessCheckPipeline.Outcome.Allow => await next(context).ConfigureAwait(false),
            AccessCheckPipeline.Outcome.Challenge => Results.Unauthorized(),
            _ => Results.Forbid()
        };
    }
}
