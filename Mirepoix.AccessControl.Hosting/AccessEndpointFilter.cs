using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Hosting;

public sealed class AccessEndpointFilter : IEndpointFilter
{
    private readonly IClaimsPrincipalMapper _mapper;
    private readonly IAccessChecker _checker;

    public AccessEndpointFilter(IClaimsPrincipalMapper mapper, IAccessChecker checker)
    {
        _mapper = mapper;
        _checker = checker;
    }

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
