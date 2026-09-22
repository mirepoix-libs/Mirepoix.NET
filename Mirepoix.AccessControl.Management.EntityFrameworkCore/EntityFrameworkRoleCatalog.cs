using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores role definitions in the shared access-control EF model.
/// </summary>
/// <remarks>
/// Sync methods block on async database work and should not be used on thread-pool-sensitive paths.
/// </remarks>
public sealed class EntityFrameworkRoleCatalog : IRoleCatalog
{
    private readonly DbContext _db;

    /// <summary>
    /// Creates a scoped catalog over the supplied access-control context.
    /// </summary>
    /// <param name="db">Context whose role rows are updated.</param>
    public EntityFrameworkRoleCatalog(AccessControlDbContext db)
        : this((DbContext)db)
    {
    }

    internal EntityFrameworkRoleCatalog(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public void Add(Role role) => AddAsync(role).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void Remove(string roleId) => RemoveAsync(roleId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlyList<Role> List() => ListAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(role);
        var row = await _db.Set<RoleEntity>()
            .SingleOrDefaultAsync(x => x.RoleId == role.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            _db.Add(new RoleEntity { RoleId = role.Id, Description = role.Description });
        }
        else
        {
            row.Description = role.Description;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<RoleEntity>()
            .SingleOrDefaultAsync(x => x.RoleId == roleId, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        _db.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.Set<RoleEntity>()
            .AsNoTracking()
            .OrderBy(x => x.RoleId)
            .Select(x => new Role(x.RoleId, x.Description))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
