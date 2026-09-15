using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Hosting;

internal static class AccessCheckPipeline
{
    internal enum Outcome
    {
        Allow,
        Forbid,
        Challenge
    }

    internal static async Task<(Outcome Outcome, AccessDecision? Decision)> EvaluateAsync(
        HttpContext httpContext,
        IClaimsPrincipalMapper mapper,
        IAccessChecker checker,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
            return (Outcome.Challenge, null);

        var endpoint = httpContext.GetEndpoint();
        var operationAttr = endpoint?.Metadata.GetMetadata<AccessOperationAttribute>();
        if (operationAttr is null || string.IsNullOrWhiteSpace(operationAttr.Operation))
            return (Outcome.Forbid, null);

        Operation operation;
        try
        {
            operation = Operation.Parse(operationAttr.Operation);
        }
        catch (ArgumentException)
        {
            return (Outcome.Forbid, null);
        }

        var resourceAttr = endpoint!.Metadata.GetMetadata<AccessResourceAttribute>();
        var resourceType = resourceAttr?.ResourceType ?? string.Empty;
        var resourceId = string.Empty;
        if (resourceAttr is not null
            && httpContext.Request.RouteValues.TryGetValue(resourceAttr.IdRouteKey, out var routeId)
            && routeId is not null)
        {
            resourceId = Convert.ToString(routeId) ?? string.Empty;
        }

        var seed = mapper.CreateSeed(httpContext);
        var request = new AuthorizationRequest(
            seed.Subject,
            new Resource(resourceType, resourceId, new Dictionary<string, object?>()),
            operation,
            seed.Context);

        var decision = await checker.CheckAsync(request, cancellationToken).ConfigureAwait(false);
        if (decision.Result == AuthorizationResult.Allow
            && decision.Status == DecisionStatus.Success)
        {
            return (Outcome.Allow, decision);
        }

        return (Outcome.Forbid, decision);
    }
}
