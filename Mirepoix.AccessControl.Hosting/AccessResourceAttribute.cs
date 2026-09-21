namespace Mirepoix.AccessControl.Hosting;

/// <summary>
/// Attaches optional resource type and route-key metadata used to build a partial <see cref="Resource"/>.
/// When absent, the PEP uses empty type/id; hydration may still fill attributes or fail closed.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AccessResourceAttribute : Attribute
{
    /// <summary>
    /// Creates metadata for <paramref name="resourceType"/> and the route value named <paramref name="idRouteKey"/>.
    /// </summary>
    /// <param name="resourceType">Resource type segment (e.g. <c>doc</c>). Must not be null.</param>
    /// <param name="idRouteKey">Route value name for the resource id. Defaults to <c>id</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    public AccessResourceAttribute(string resourceType, string idRouteKey = "id")
    {
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        IdRouteKey = idRouteKey ?? throw new ArgumentNullException(nameof(idRouteKey));
    }

    /// <summary>
    /// Holds the resource type written onto the partial <see cref="Resource"/>.
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// Names the route value keyed for the resource id (looked up on <c>HttpContext.Request.RouteValues</c>).
    /// </summary>
    public string IdRouteKey { get; }
}
