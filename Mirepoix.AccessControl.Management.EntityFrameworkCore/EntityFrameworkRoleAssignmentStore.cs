using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores subject role assignments in EF and rejects proposed sets that overlap an SoD constraint twice.
/// </summary>
/// <remarks>
/// Sync methods block on async database work and should not be used on thread-pool-sensitive paths.
/// </remarks>
public sealed class EntityFrameworkRoleAssignmentStore : IRoleAssignmentStore
{
    private readonly DbContext _db;
    private readonly ISodConstraintStore _sod;

    /// <summary>
    /// Creates a scoped assignment store that consults the supplied constraint store before inserts.
    /// </summary>
    /// <param name="db">Context whose subject role rows are updated.</param>
    /// <param name="sodConstraints">Constraint source consulted during assignment.</param>
    public EntityFrameworkRoleAssignmentStore(
        AccessControlDbContext db,
        ISodConstraintStore sodConstraints)
        : this((DbContext)db, sodConstraints)
    {
    }

    internal EntityFrameworkRoleAssignmentStore(
        DbContext db,
        ISodConstraintStore sodConstraints)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sodConstraints);
        _db = db;
        _sod = sodConstraints;
    }

    /// <inheritdoc />
    public AssignmentResult Assign(string subjectId, string roleId) =>
        AssignAsync(subjectId, roleId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void Revoke(string subjectId, string roleId) =>
        RevokeAsync(subjectId, roleId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlySet<string> GetRoles(string subjectId) =>
        GetRolesAsync(subjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task<AssignmentResult> AssignAsync(
        string subjectId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetRolesAsync(subjectId, cancellationToken).ConfigureAwait(false);
        if (current.Contains(roleId))
            return AssignmentResult.Assigned();

        var proposed = current.ToHashSet();
        proposed.Add(roleId);
        foreach (var constraint in await _sod.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            var overlap = proposed.Intersect(constraint.MutuallyExclusiveRoles).ToList();
            if (overlap.Count >= 2)
                return AssignmentResult.SodConflict(constraint.Id, overlap);
        }

        _db.Add(new SubjectRoleEntity { SubjectId = subjectId, Role = roleId });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return AssignmentResult.Assigned();
    }

    /// <inheritdoc />
    public async Task RevokeAsync(
        string subjectId,
        string roleId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<SubjectRoleEntity>()
            .SingleOrDefaultAsync(
                x => x.SubjectId == subjectId && x.Role == roleId,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        _db.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetRolesAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        var roles = await _db.Set<SubjectRoleEntity>()
            .AsNoTracking()
            .Where(x => x.SubjectId == subjectId)
            .Select(x => x.Role)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return roles.ToHashSet();
    }
}
