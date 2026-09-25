namespace Mirepoix.AccessControl;

/// <summary>
/// One concrete operation an enforcement app publishes.
/// Empty route template and HTTP method mean the operation was registered in code, not on a route.
/// </summary>
/// <param name="Operation">Concrete operation string passed to <c>CheckAsync</c>.</param>
/// <param name="RouteTemplate">Route template, or empty when there is no route.</param>
/// <param name="HttpMethod">HTTP method, or empty when there is no route.</param>
public sealed record PublishedOperation(string Operation, string RouteTemplate, string HttpMethod)
{
    /// <summary>Relative path of the enforcement catalog GET.</summary>
    public const string EnforcementPath = "/access-control/operations";
}
