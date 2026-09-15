using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers;

public sealed class EntityFrameworkSubjectResolver : ISubjectResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type _contextType;

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

        _scopeFactory = scopeFactory;
        _contextType = options.ContextType;
    }

    public async Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken)
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
