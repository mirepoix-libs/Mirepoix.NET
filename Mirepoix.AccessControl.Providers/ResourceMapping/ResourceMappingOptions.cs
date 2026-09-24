namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds ordered resource entity maps shared by durable provider adapters.
/// Empty <see cref="Maps"/> means adapters should use built-in resource storage instead of mapped CLR types.
/// </summary>
public sealed class ResourceMappingOptions
{
    private readonly List<ResourceEntityMap> _maps = new();

    /// <summary>Lists configured maps in registration order.</summary>
    public IReadOnlyList<ResourceEntityMap> Maps => _maps;

    /// <summary>Reports <see langword="true"/> when at least one map is configured.</summary>
    public bool HasMaps => _maps.Count > 0;

    /// <summary>
    /// Adds a map for <typeparamref name="T"/> via a fluent builder and returns this instance for chaining.
    /// </summary>
    /// <typeparam name="T">CLR entity type.</typeparam>
    /// <param name="configure">Builder configuration.</param>
    /// <returns>This options instance.</returns>
    public ResourceMappingOptions MapEntity<T>(Action<ResourceEntityMapBuilder<T>> configure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ResourceEntityMapBuilder<T>();
        configure(builder);
        Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a built resource map. Throws when a map with the same <see cref="ResourceEntityMap.Type"/> already exists.
    /// </summary>
    /// <param name="map">The map to add.</param>
    /// <exception cref="InvalidOperationException">Thrown when duplicate type strings are detected.</exception>
    public void Add(ResourceEntityMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (_maps.Any(existing => string.Equals(existing.Type, map.Type, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Duplicate resource type value '{map.Type}'. Each Type must be unique.");
        }

        _maps.Add(map);
    }

    /// <summary>
    /// Validates configured maps. Call at registration or first options use.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when duplicate <see cref="ResourceEntityMap.Type"/> values exist.
    /// </exception>
    public void Validate()
    {
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var map in _maps)
        {
            if (!seenTypes.Add(map.Type))
            {
                throw new InvalidOperationException(
                    $"Duplicate resource type value '{map.Type}'. Each Type must be unique.");
            }
        }
    }
}
