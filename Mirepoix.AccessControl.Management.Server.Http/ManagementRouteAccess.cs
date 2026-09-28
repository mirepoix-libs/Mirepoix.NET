namespace Mirepoix.AccessControl.Management.Server.Http;

/// <summary>
/// Names the fixed operation and resource checked before a management route handler runs.
/// </summary>
internal sealed class ManagementRouteAccess
{
    /// <summary>
    /// Stores the operation, resource type, and optional route-value key for one management route.
    /// </summary>
    /// <param name="operation">Concrete operation passed to <see cref="IAccessChecker"/>.</param>
    /// <param name="resourceType">Resource type. Empty for policy-set and operations routes.</param>
    /// <param name="idRouteKey">Route value used as the resource id. Null means an empty id.</param>
    public ManagementRouteAccess(string operation, string resourceType, string? idRouteKey)
    {
        Operation = operation;
        ResourceType = resourceType;
        IdRouteKey = idRouteKey;
    }

    /// <summary>Gets the operation string for this route.</summary>
    public string Operation { get; }

    /// <summary>Gets the resource type for this route.</summary>
    public string ResourceType { get; }

    /// <summary>Gets the route key whose value is the resource id, or null when the id is empty.</summary>
    public string? IdRouteKey { get; }
}
