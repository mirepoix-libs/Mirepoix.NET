using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers;

public sealed class EntityFrameworkResourceResolver : IResourceResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type _contextType;

    public EntityFrameworkResourceResolver(
        IServiceScopeFactory scopeFactory,
        EntityFrameworkProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        if (options.ContextType is null || !typeof(DbContext).IsAssignableFrom(options.ContextType))
        {
            throw new InvalidOperationException(
                "EntityFrameworkProviderOptions.ContextType must be set to a DbContext type.");
        }

        _scopeFactory = scopeFactory;
        _contextType = options.ContextType;
    }

    public async Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(_contextType);

        var rows = await db.Set<ResourceAttributeEntity>()
            .AsNoTracking()
            .Where(x => x.ResourceType == partial.Type && x.ResourceId == partial.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            throw new KeyNotFoundException($"Resource '{partial.Type}:{partial.Id}' was not found.");

        var attributes = rows.ToDictionary(
            r => r.Name,
            r => AttributeValueCodec.FromJson(r.ValueJson),
            StringComparer.Ordinal);

        return new Resource(partial.Type, partial.Id, attributes);
    }
}
