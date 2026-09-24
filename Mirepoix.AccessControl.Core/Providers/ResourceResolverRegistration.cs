namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Describes one resource type registration for composite dispatch.
/// </summary>
public sealed class ResourceResolverRegistration
{
    /// <summary>Gets the resource type matched with ordinal equality.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the resolver's service type.</summary>
    public required Type ResolverServiceType { get; init; }

    /// <summary>Gets the delegate that resolves and invokes the registered resolver.</summary>
    public required Func<IServiceProvider, Resource, CancellationToken, Task<Resource>> Invoke { get; init; }
}
