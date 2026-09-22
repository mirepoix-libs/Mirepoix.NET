namespace Mirepoix.AccessControl.Engine.Server.Http;

/// <summary>
/// Route and authorization settings for the engine HTTP check endpoint.
/// </summary>
public sealed class AccessControlEngineServerHttpOptions
{
    /// <summary>Gets or sets the route group prefix. Default is <c>/access-control</c>.</summary>
    public string RoutePrefix { get; set; } = "/access-control";

    /// <summary>Gets or sets an optional authorization policy applied to the check endpoint.</summary>
    public string? AuthorizationPolicy { get; set; }
}
