using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores resource ownership as the JSON-encoded "ownerId" resource attribute.
/// </summary>
/// <remarks>
/// Sync methods block on async database work and should not be used on thread-pool-sensitive paths.
/// </remarks>
public sealed class EntityFrameworkOwnershipHelper : IOwnershipHelper
{
    private const string OwnerAttributeName = "ownerId";
    private readonly DbContext _db;

    /// <summary>
    /// Creates a scoped ownership helper over the supplied access-control context.
    /// </summary>
    /// <param name="db">Context whose resource attribute rows are updated.</param>
    public EntityFrameworkOwnershipHelper(AccessControlDbContext db)
        : this((DbContext)db)
    {
    }

    internal EntityFrameworkOwnershipHelper(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public void SetOwner(string resourceType, string resourceId, string ownerSubjectId) =>
        SetOwnerAsync(resourceType, resourceId, ownerSubjectId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void ClearOwner(string resourceType, string resourceId) =>
        ClearOwnerAsync(resourceType, resourceId).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task SetOwnerAsync(
        string resourceType,
        string resourceId,
        string ownerSubjectId,
        CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(resourceType, resourceId, cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            _db.Add(new ResourceAttributeEntity
            {
                ResourceType = resourceType,
                ResourceId = resourceId,
                Name = OwnerAttributeName,
                ValueJson = AttributeValueCodec.ToJson(ownerSubjectId),
            });
        }
        else
        {
            row.ValueJson = AttributeValueCodec.ToJson(ownerSubjectId);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ClearOwnerAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(resourceType, resourceId, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return;

        _db.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<ResourceAttributeEntity?> FindAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken) =>
        _db.Set<ResourceAttributeEntity>().SingleOrDefaultAsync(
            x => x.ResourceType == resourceType
                && x.ResourceId == resourceId
                && x.Name == OwnerAttributeName,
            cancellationToken);
}
