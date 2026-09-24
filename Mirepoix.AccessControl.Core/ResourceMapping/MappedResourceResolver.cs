namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Hydrates resources through a fluent <see cref="ResourceEntityMap"/>.
/// </summary>
/// <typeparam name="T">Mapped domain resource type.</typeparam>
public sealed class MappedResourceResolver<T> : IResourceResolver<T>
    where T : class
{
    private readonly ResourceEntityMap _map;
    private readonly IServiceProvider _services;

    /// <summary>
    /// Creates a resolver backed by <paramref name="map"/>, resolving load services from
    /// <paramref name="services"/> (typically the current DI scope).
    /// </summary>
    public MappedResourceResolver(ResourceEntityMap map, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(services);
        if (map.ClrType != typeof(T))
        {
            throw new ArgumentException(
                $"Map type '{map.ClrType.Name}' does not match resolver type '{typeof(T).Name}'.",
                nameof(map));
        }

        _map = map;
        _services = services;
    }

    /// <inheritdoc />
    public async Task<Resource> HydrateAsync(
        Resource partial,
        CancellationToken cancellationToken)
    {
        var entity = await _map.Load(_services, partial.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            throw new KeyNotFoundException($"Resource '{_map.Type}:{partial.Id}' was not found.");

        return ResourceFactory.Create(entity, _map);
    }
}
