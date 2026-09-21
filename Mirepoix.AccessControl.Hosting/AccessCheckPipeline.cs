using Microsoft.AspNetCore.Http;

namespace Mirepoix.AccessControl.Hosting;

/// <summary>
/// Runs the shared HTTP PEP for both authorization handler and endpoint filter.
/// Maps metadata + principal seed into an <see cref="AuthorizationRequest"/>, calls
/// <see cref="IAccessChecker"/>, and classifies Allow / Forbid / Challenge.
/// </summary>
internal static class AccessCheckPipeline
{
    /// <summary>
    /// Classifies the HTTP PEP outcome after authentication and access evaluation.
    /// </summary>
    internal enum Outcome
    {
        /// <summary>
        /// Continues the request: decision was Allow with <see cref="DecisionStatus.Success"/>.
        /// </summary>
        Allow,

        /// <summary>
        /// Denies the request (403 path): missing/invalid operation metadata, or non-success decision.
        /// </summary>
        Forbid,

        /// <summary>
        /// Defers to authentication (401 path): principal is not authenticated.
        /// </summary>
        Challenge
    }

    /// <summary>
    /// Evaluates access for the current endpoint: authn check, operation/resource metadata,
    /// mapper seed, then <see cref="IAccessChecker.CheckAsync"/>.
    /// </summary>
    /// <param name="httpContext">Current request.</param>
    /// <param name="mapper">Builds subject/context from the principal.</param>
    /// <param name="checker">Performs hydrate + authorize.</param>
    /// <param name="cancellationToken">Passed to <see cref="IAccessChecker.CheckAsync"/>.</param>
    /// <returns>
    /// Outcome plus optional decision:
    /// <list type="bullet">
    /// <item><description>Unauthenticated: <see cref="Outcome.Challenge"/>, null decision.</description></item>
    /// <item><description>Missing/blank <see cref="AccessOperationAttribute"/>, or <see cref="Operation.Parse"/> throws: <see cref="Outcome.Forbid"/>, null decision.</description></item>
    /// <item><description>Allow + <see cref="DecisionStatus.Success"/>: <see cref="Outcome.Allow"/> with decision.</description></item>
    /// <item><description>Any other decision (Deny, Defaulted, hydration/policy failure): <see cref="Outcome.Forbid"/> with decision.</description></item>
    /// </list>
    /// Resource type/id come from <see cref="AccessResourceAttribute"/> when present; missing route id yields empty id (partial resource).
    /// </returns>
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
