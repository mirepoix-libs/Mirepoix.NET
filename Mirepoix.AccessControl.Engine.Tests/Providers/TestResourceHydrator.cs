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

        if (!attributes.TryGetValue((partial.Type, partial.Id), out var hydratedAttributes))
        {
            throw new KeyNotFoundException(
                $"Resource '{partial.Type}/{partial.Id}' was not found.");
        }

        return Task.FromResult(
            new Resource(
                partial.Type,
                partial.Id,
                new Dictionary<string, object?>(hydratedAttributes)));
    }
}
