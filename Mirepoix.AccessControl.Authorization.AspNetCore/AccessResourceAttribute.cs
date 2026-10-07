namespace Mirepoix.AccessControl.Authorization.AspNetCore;

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
        KeyBindings = null;
    }

    /// <summary>
    /// Creates metadata for <paramref name="resourceType"/> with ordered composite key bindings from route values.
    /// </summary>
    /// <param name="resourceType">Resource type segment (e.g. <c>document</c>). Must not be null.</param>
    /// <param name="keyBindings">Non-empty route value names used as <see cref="ResourceKey"/> parts, in order.</param>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keyBindings"/> is empty or any name is blank.</exception>
    public AccessResourceAttribute(string resourceType, string[] keyBindings)
    {
        ResourceType = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
        ArgumentNullException.ThrowIfNull(keyBindings);
        if (keyBindings.Length == 0)
            throw new ArgumentException("KeyBindings must be non-empty.", nameof(keyBindings));
        foreach (var name in keyBindings)
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

        IdRouteKey = "id"; // ignored when KeyBindings set
        KeyBindings = keyBindings;
    }

    /// <summary>
    /// Holds the resource type written onto the partial <see cref="Resource"/>.
    /// </summary>
    public string ResourceType { get; }

    /// <summary>
    /// Names the route value keyed for the resource id (looked up on <c>HttpContext.Request.RouteValues</c>).
    /// Ignored when <see cref="KeyBindings"/> is non-null.
    /// </summary>
    public string IdRouteKey { get; }

    /// <summary>
    /// Ordered route value names used to build a composite <see cref="ResourceKey"/>.
    /// When null or empty, the PEP uses <see cref="IdRouteKey"/> / <see cref="ResourceKey.Single"/>.
    /// </summary>
    public IReadOnlyList<string>? KeyBindings { get; }
}
