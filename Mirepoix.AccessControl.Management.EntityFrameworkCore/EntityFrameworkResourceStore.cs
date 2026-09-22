using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores resource attributes and ownership in library-owned EF rows.
/// </summary>
/// <remarks>
/// Ownership uses the <c>ownerId</c> resource attribute. Sync methods block on async database work.
/// </remarks>
public sealed class EntityFrameworkResourceStore : IResourceStore
{
    private const string OwnerAttributeName = "ownerId";
    private readonly DbContext _db;

    /// <summary>
    /// Creates a scoped resource store over the supplied access-control context.
    /// </summary>
    /// <param name="db">Context whose resource attribute rows are managed.</param>
    public EntityFrameworkResourceStore(AccessControlDbContext db)
        : this((DbContext)db)
    {
    }

    internal EntityFrameworkResourceStore(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public void SetAttribute(string resourceType, string resourceId, string key, object? value) =>
        SetAttributeAsync(resourceType, resourceId, key, value).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void ClearAttribute(string resourceType, string resourceId, string key) =>
        ClearAttributeAsync(resourceType, resourceId, key).GetAwaiter().GetResult();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> GetAttributes(string resourceType, string resourceId) =>
        GetAttributesAsync(resourceType, resourceId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void SetOwner(string resourceType, string resourceId, string ownerSubjectId) =>
        SetOwnerAsync(resourceType, resourceId, ownerSubjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void ClearOwner(string resourceType, string resourceId) =>
        ClearOwnerAsync(resourceType, resourceId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public string? GetOwner(string resourceType, string resourceId) =>
        GetOwnerAsync(resourceType, resourceId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task SetAttributeAsync(
        string resourceType,
        string resourceId,
        string key,
        object? value,
        CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(resourceType, resourceId, key, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            _db.Add(new ResourceAttributeEntity
            {
                ResourceType = resourceType,
                ResourceId = resourceId,
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
        string resourceType,
        string resourceId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(resourceType, resourceId, key, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        _db.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, object?>> GetAttributesAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Set<ResourceAttributeEntity>()
            .AsNoTracking()
            .Where(x => x.ResourceType == resourceType && x.ResourceId == resourceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(x => x.Name, x => AttributeValueCodec.FromJson(x.ValueJson));
    }

    /// <inheritdoc />
    public Task SetOwnerAsync(
        string resourceType,
        string resourceId,
        string ownerSubjectId,
        CancellationToken cancellationToken = default) =>
        SetAttributeAsync(resourceType, resourceId, OwnerAttributeName, ownerSubjectId, cancellationToken);

    /// <inheritdoc />
    public Task ClearOwnerAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default) =>
        ClearAttributeAsync(resourceType, resourceId, OwnerAttributeName, cancellationToken);

    /// <inheritdoc />
    public async Task<string?> GetOwnerAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.Set<ResourceAttributeEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.ResourceType == resourceType
                    && x.ResourceId == resourceId
                    && x.Name == OwnerAttributeName,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : AttributeValueCodec.FromJson(row.ValueJson) as string;
    }

    private Task<ResourceAttributeEntity?> FindAsync(
        string resourceType,
        string resourceId,
        string key,
        CancellationToken cancellationToken) =>
        _db.Set<ResourceAttributeEntity>().SingleOrDefaultAsync(
            x => x.ResourceType == resourceType
                && x.ResourceId == resourceId
                && x.Name == key,
            cancellationToken);
}
