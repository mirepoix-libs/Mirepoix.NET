namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Looks up resource attributes in memory by (type, id). Missing key throws
/// <see cref="KeyNotFoundException"/>. Builds a new <see cref="Resource"/> with stored attributes
/// and the partial's type/id (does not keep partial attributes).
/// </summary>
public sealed class InMemoryResourceResolver : IResourceResolver
{
    private readonly IReadOnlyDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> _resources;

    /// <summary>
    /// Creates a resolver over a static (type, id) to attributes map.
    /// </summary>
    /// <param name="resources">Lookup table of resource attributes.</param>
    public InMemoryResourceResolver(
        IReadOnlyDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> resources)
    {
        _resources = resources;
    }

    /// <summary>
    /// Looks up attributes for <paramref name="partial"/>'s type and id.
    /// </summary>
    /// <param name="partial">Partial resource (type + id used as key).</param>
    /// <param name="cancellationToken">Unused; completed synchronously.</param>
    /// <returns>Resource with stored attributes.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key is not in the map.</exception>
    public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken)
    {
        if (!_resources.TryGetValue((partial.Type, partial.Id), out var attributes))
            throw new KeyNotFoundException($"Resource '{partial.Type}:{partial.Id}' was not found.");

        return Task.FromResult(new Resource(partial.Type, partial.Id, attributes));
    }
}
