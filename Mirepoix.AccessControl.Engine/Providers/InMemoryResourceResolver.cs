namespace Mirepoix.AccessControl.Providers;

public sealed class InMemoryResourceResolver : IResourceResolver
{
    private readonly IReadOnlyDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> _resources;

    public InMemoryResourceResolver(
        IReadOnlyDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> resources)
    {
        _resources = resources;
    }

    public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken)
    {
        if (!_resources.TryGetValue((partial.Type, partial.Id), out var attributes))
            throw new KeyNotFoundException($"Resource '{partial.Type}:{partial.Id}' was not found.");

        return Task.FromResult(new Resource(partial.Type, partial.Id, attributes));
    }
}
