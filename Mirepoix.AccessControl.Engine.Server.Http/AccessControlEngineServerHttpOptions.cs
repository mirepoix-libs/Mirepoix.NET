namespace Mirepoix.AccessControl.Engine.Server.Http;

/// <summary>
/// Route and authorization settings for the engine HTTP check endpoint.
/// </summary>
public sealed class AccessControlEngineServerHttpOptions
{
    /// <summary>Gets or sets the route group prefix. Default is <c>/access-control</c>.</summary>
    public string RoutePrefix { get; set; } = "/access-control";

    /// <summary>
    /// Gets or sets the host authorization policy name applied to the check route. Required at map time.
    /// </summary>
    /// <remarks>
    /// <c>MapAccessControlEngine</c> throws <see cref="InvalidOperationException"/> when this value is null or white space.
    /// The message is "Access-control HTTP routes require an authorization policy."
    /// This package does not register an authentication scheme.
    /// </remarks>
    public string? AuthorizationPolicy { get; set; }
}
