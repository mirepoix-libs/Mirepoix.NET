using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Providers;

internal sealed class TestResourceHydrator(
    IReadOnlyDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> attributes)
    : IResourceHydrator
{
    public Task<Resource> HydrateAsync(
        Resource partial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partial);

        var id = partial.Key.TryGet("id", out var partId) ? partId : partial.Key.Canonical();
        if (!attributes.TryGetValue((partial.Type, id), out var hydratedAttributes))
        {
            throw new KeyNotFoundException(
                $"Resource '{partial.Type}/{id}' was not found.");
        }

        return Task.FromResult(
            new Resource(
                partial.Type,
                partial.Key,
                new Dictionary<string, object?>(hydratedAttributes)));
    }
}
