using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores native subject data and library-owned subject role assignments in EF.
/// </summary>
/// <remarks>
/// Mapped-library-role layouts support roles only because the application owns subject headers and attributes.
/// Sync methods block on async database work.
/// </remarks>
public sealed class EntityFrameworkSubjectStore : ISubjectStore
{
    private const string MappedSubjectMessage =
        "Subject headers and attributes are not managed by AccessControl for MappedLibraryRoles; " +
        "the application owns that storage. Register a custom ISubjectStore or use Native layout.";

    private readonly DbContext _db;
    private readonly ISodConstraintStore _sod;
    private readonly SubjectStorageLayout _layout;

    /// <summary>
    /// Creates a scoped subject store over the supplied access-control context.
    /// </summary>
    /// <param name="db">Context whose subject rows are managed.</param>
    /// <param name="sodConstraints">Constraint source consulted during role assignment.</param>
    /// <param name="layout">Subject storage owned by the library.</param>
    public EntityFrameworkSubjectStore(
        AccessControlDbContext db,
        ISodConstraintStore sodConstraints,
        SubjectStorageLayout layout)
        : this((DbContext)db, sodConstraints, layout)
    {
    }

    internal EntityFrameworkSubjectStore(
        DbContext db,
        ISodConstraintStore sodConstraints,
        SubjectStorageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(sodConstraints);
        _db = db;
        _sod = sodConstraints;
        _layout = layout;
    }

    /// <inheritdoc />
    public void Create(string subjectId) =>
        CreateAsync(subjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public bool Exists(string subjectId) =>
        ExistsAsync(subjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void Delete(string subjectId) =>
        DeleteAsync(subjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlyList<string> ListIds() =>
        ListIdsAsync().GetAwaiter().GetResult();

    /// <inheritdoc />
    public void SetAttribute(string subjectId, string key, object? value) =>
        SetAttributeAsync(subjectId, key, value).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void ClearAttribute(string subjectId, string key) =>
        ClearAttributeAsync(subjectId, key).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> GetAttributes(string subjectId) =>
        GetAttributesAsync(subjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public AssignmentResult AssignRole(string subjectId, string roleId) =>
        AssignRoleAsync(subjectId, roleId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void RevokeRole(string subjectId, string roleId) =>
        RevokeRoleAsync(subjectId, roleId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlySet<string> GetRoles(string subjectId) =>
        GetRolesAsync(subjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task CreateAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        EnsureNativeSubjectStorage();
        if (await ExistsAsync(subjectId, cancellationToken).ConfigureAwait(false))
            return;

        _db.Add(new SubjectEntity { SubjectId = subjectId });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        EnsureNativeSubjectStorage();
        return _db.Set<SubjectEntity>()
            .AsNoTracking()
            .AnyAsync(x => x.SubjectId == subjectId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        EnsureNativeSubjectStorage();
        var attributes = await _db.Set<SubjectAttributeEntity>()
            .Where(x => x.SubjectId == subjectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var roles = await _db.Set<SubjectRoleEntity>()
            .Where(x => x.SubjectId == subjectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var subject = await _db.Set<SubjectEntity>()
            .SingleOrDefaultAsync(x => x.SubjectId == subjectId, cancellationToken)
            .ConfigureAwait(false);

        _db.RemoveRange(attributes);
        _db.RemoveRange(roles);
        if (subject is not null)
            _db.Remove(subject);

        if (attributes.Count > 0 || roles.Count > 0 || subject is not null)
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListIdsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNativeSubjectStorage();
        return await _db.Set<SubjectEntity>()
            .AsNoTracking()
            .OrderBy(x => x.SubjectId)
            .Select(x => x.SubjectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetAttributeAsync(
        string subjectId,
        string key,
        object? value,
        CancellationToken cancellationToken = default)
    {
        EnsureNativeSubjectStorage();
        var subject = await _db.Set<SubjectEntity>()
            .SingleOrDefaultAsync(x => x.SubjectId == subjectId, cancellationToken)
            .ConfigureAwait(false);
        if (subject is null)
            _db.Add(new SubjectEntity { SubjectId = subjectId });

        var row = await _db.Set<SubjectAttributeEntity>()
            .SingleOrDefaultAsync(
                x => x.SubjectId == subjectId && x.Name == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            _db.Add(new SubjectAttributeEntity
            {
                SubjectId = subjectId,
                Name = key,
                ValueJson = AttributeValueCodec.ToJson(value),
            });
        }
        else
        {
            row.ValueJson = AttributeValueCodec.ToJson(value);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearAttributeAsync(
        string subjectId,
        string key,
        CancellationToken cancellationToken = default)
    {
        EnsureNativeSubjectStorage();
        var row = await _db.Set<SubjectAttributeEntity>()
            .SingleOrDefaultAsync(
                x => x.SubjectId == subjectId && x.Name == key,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        _db.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        EnsureNativeSubjectStorage();
        var rows = await _db.Set<SubjectAttributeEntity>()
            .AsNoTracking()
            .Where(x => x.SubjectId == subjectId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(x => x.Name, x => AttributeValueCodec.FromJson(x.ValueJson));
    }

    /// <inheritdoc />
    public async Task<AssignmentResult> AssignRoleAsync(
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
    public async Task RevokeRoleAsync(
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

    private void EnsureNativeSubjectStorage()
    {
        if (_layout != SubjectStorageLayout.Native)
            throw new InvalidOperationException(MappedSubjectMessage);
    }
}
