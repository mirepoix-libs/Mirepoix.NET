using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores separation-of-duties constraints and their role sets in the shared EF model.
/// </summary>
/// <remarks>
/// Sync methods block on async database work and should not be used on thread-pool-sensitive paths.
/// </remarks>
public sealed class EntityFrameworkSodConstraintStore : ISodConstraintStore
{
    private readonly DbContext _db;

    /// <summary>
    /// Creates a scoped constraint store over the supplied access-control context.
    /// </summary>
    /// <param name="db">Context whose constraint rows are updated.</param>
    public EntityFrameworkSodConstraintStore(AccessControlDbContext db)
        : this((DbContext)db)
    {
    }

    internal EntityFrameworkSodConstraintStore(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public void Add(SodConstraint constraint) => AddAsync(constraint).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void Remove(string id) => RemoveAsync(id).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlyList<SodConstraint> List() => ListAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task AddAsync(SodConstraint constraint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        var row = await _db.Set<SodConstraintEntity>()
            .Include(x => x.Roles)
            .SingleOrDefaultAsync(x => x.ConstraintId == constraint.Id, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new SodConstraintEntity { ConstraintId = constraint.Id };
            _db.Add(row);
        }
        else
        {
            var desiredRoleIds = constraint.MutuallyExclusiveRoles.ToHashSet();
            var removedRoles = row.Roles
                .Where(role => !desiredRoleIds.Contains(role.RoleId))
                .ToList();
            _db.RemoveRange(removedRoles);
            foreach (var removedRole in removedRoles)
                row.Roles.Remove(removedRole);
        }

        var existingRoleIds = row.Roles.Select(role => role.RoleId).ToHashSet();
        foreach (var roleId in constraint.MutuallyExclusiveRoles.Where(roleId => !existingRoleIds.Contains(roleId)))
        {
            row.Roles.Add(new SodConstraintRoleEntity
            {
                ConstraintId = constraint.Id,
                RoleId = roleId,
            });
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<SodConstraintEntity>()
            .SingleOrDefaultAsync(x => x.ConstraintId == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        _db.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SodConstraint>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.Set<SodConstraintEntity>()
            .AsNoTracking()
            .Include(x => x.Roles)
            .OrderBy(x => x.ConstraintId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(x => new SodConstraint(
                x.ConstraintId,
                x.Roles.Select(role => role.RoleId).ToHashSet()))
            .ToList();
    }
}
