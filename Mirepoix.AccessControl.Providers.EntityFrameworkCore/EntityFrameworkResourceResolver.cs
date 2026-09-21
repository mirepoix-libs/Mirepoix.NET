using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Loads <see cref="ResourceAttributeEntity"/> rows for the partial's type and id as an <see cref="IResourceResolver"/>.
/// Zero rows means not found. Creates a DI scope per call.
/// </summary>
public sealed class EntityFrameworkResourceResolver : IResourceResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type _contextType;

    /// <summary>
    /// Creates a resolver. <see cref="EntityFrameworkProviderOptions.ContextType"/> must be a <see cref="DbContext"/> type.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for per-call DbContext resolution.</param>
    /// <param name="options">Context type.</param>
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

    /// <summary>
    /// Hydrates resource attributes for <paramref name="partial"/>.
    /// </summary>
    /// <param name="partial">Resource type and id.</param>
    /// <param name="cancellationToken">Cancellation for EF queries.</param>
    /// <returns>Resource with stored attributes.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no attribute rows exist.</exception>
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
