using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Hydrates subjects via EF as an <see cref="ISubjectResolver"/>.
/// Empty maps: load <see cref="SubjectEntity"/> with roles/attributes from <c>ac_subject*</c>.
/// With maps: <see cref="SubjectMappingLookup"/> + <see cref="EntityFrameworkSubjectFetch"/> against
/// <c>DbSet&lt;T&gt;</c> for each map's CLR type. Validates maps at construction (<c>requireStorageTable: false</c>).
/// Creates a DI scope per hydrate call.
/// </summary>
public sealed class EntityFrameworkSubjectResolver : ISubjectResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type _contextType;
    private readonly SubjectMappingOptions _subjectMapping;

    /// <summary>
    /// Creates a resolver. <see cref="EntityFrameworkProviderOptions.ContextType"/> must be a <see cref="DbContext"/> type.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for per-call DbContext resolution.</param>
    /// <param name="options">Context type and subject mapping.</param>
    public EntityFrameworkSubjectResolver(
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

        options.SubjectMapping.Validate(requireStorageTable: false);

        _scopeFactory = scopeFactory;
        _contextType = options.ContextType;
        _subjectMapping = options.SubjectMapping;
    }

    /// <summary>
    /// Hydrates <paramref name="partial"/> via built-in entities or mapped CLR types.
    /// </summary>
    /// <param name="partial">Partial subject (id required; optional type hint when mapped).</param>
    /// <param name="cancellationToken">Cancellation for EF queries.</param>
    /// <returns>Hydrated subject.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the subject cannot be found.</exception>
    public Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken)
    {
        if (_subjectMapping.HasMaps)
            return HydrateMappedAsync(partial, cancellationToken);

        return HydrateBuiltInAsync(partial, cancellationToken);
    }

    private async Task<Subject> HydrateMappedAsync(Subject partial, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(_contextType);

        return await SubjectMappingLookup.HydrateAsync(
                partial,
                _subjectMapping,
                (map, id, ct) => EntityFrameworkSubjectFetch.FindAsync(db, map, id, ct),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Subject> HydrateBuiltInAsync(Subject partial, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(_contextType);

        var entity = await db.Set<SubjectEntity>()
            .AsNoTracking()
            .Include(x => x.Roles)
            .Include(x => x.Attributes)
            .SingleOrDefaultAsync(x => x.SubjectId == partial.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            throw new KeyNotFoundException($"Subject '{partial.Id}' was not found.");

        var roles = new HashSet<string>(entity.Roles.Select(r => r.Role), StringComparer.Ordinal);
        var attributes = entity.Attributes.ToDictionary(
            a => a.Name,
            a => AttributeValueCodec.FromJson(a.ValueJson),
            StringComparer.Ordinal);

        return new Subject(entity.SubjectId, roles, attributes);
    }
}
