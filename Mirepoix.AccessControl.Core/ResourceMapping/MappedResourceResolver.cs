namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Hydrates resources through a fluent <see cref="ResourceEntityMap"/>.
/// </summary>
/// <typeparam name="T">Mapped domain resource type.</typeparam>
public sealed class MappedResourceResolver<T> : IResourceResolver<T>
    where T : class
{
    private readonly ResourceEntityMap _map;

    /// <summary>Creates a resolver backed by <paramref name="map"/>.</summary>
    public MappedResourceResolver(ResourceEntityMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        if (map.ClrType != typeof(T))
        {
            throw new ArgumentException(
                $"Map type '{map.ClrType.Name}' does not match resolver type '{typeof(T).Name}'.",
                nameof(map));
        }

        _map = map;
    }

    /// <inheritdoc />
    public async Task<Resource> HydrateAsync(
        Resource partial,
        CancellationToken cancellationToken)
    {
        var entity = await _map.Load(partial.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            throw new KeyNotFoundException($"Resource '{_map.Type}:{partial.Id}' was not found.");

        return ResourceFactory.Create(entity, _map);
    }
}
