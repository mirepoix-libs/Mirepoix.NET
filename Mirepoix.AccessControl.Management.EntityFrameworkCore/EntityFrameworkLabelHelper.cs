using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores JSON-encoded subject and resource labels in library-owned attribute rows.
/// </summary>
/// <remarks>
/// Subject operations reject mapped subject layouts because mapped entities own their labels.
/// Sync methods block on async database work and should not be used on thread-pool-sensitive paths.
/// </remarks>
public sealed class EntityFrameworkLabelHelper : ILabelHelper
{
    private const string MappedSubjectMessage =
        "Subject labels are not managed by AccessControl when subject entity maps are configured; the application owns mapped subject storage.";

    private readonly DbContext _db;
    private readonly SubjectStorageLayout _layout;

    /// <summary>
    /// Creates a scoped label helper and resolves subject storage ownership from provider mappings.
    /// </summary>
    /// <param name="db">Context whose attribute rows are updated.</param>
    /// <param name="options">Provider subject mappings used to guard subject label operations.</param>
    public EntityFrameworkLabelHelper(
        AccessControlDbContext db,
        EntityFrameworkProviderOptions options)
        : this((DbContext)db, options)
    {
    }

    internal EntityFrameworkLabelHelper(
        DbContext db,
        EntityFrameworkProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _layout = SubjectStorageLayoutResolver.Resolve(options.SubjectMapping);
    }

    /// <inheritdoc />
    public void SetSubjectLabel(string subjectId, string key, object? value) =>
        SetSubjectLabelAsync(subjectId, key, value).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void ClearSubjectLabel(string subjectId, string key) =>
        ClearSubjectLabelAsync(subjectId, key).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void SetResourceLabel(string resourceType, string resourceId, string key, object? value) =>
        SetResourceLabelAsync(resourceType, resourceId, key, value).GetAwaiter().GetResult();

    /// <inheritdoc />
    public void ClearResourceLabel(string resourceType, string resourceId, string key) =>
        ClearResourceLabelAsync(resourceType, resourceId, key).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task SetSubjectLabelAsync(
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
    public async Task ClearSubjectLabelAsync(
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
    public async Task SetResourceLabelAsync(
        string resourceType,
        string resourceId,
        string key,
        object? value,
        CancellationToken cancellationToken = default)
    {
        var row = await FindResourceAsync(resourceType, resourceId, key, cancellationToken)
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
    public async Task ClearResourceLabelAsync(
        string resourceType,
        string resourceId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var row = await FindResourceAsync(resourceType, resourceId, key, cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        _db.Remove(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<ResourceAttributeEntity?> FindResourceAsync(
        string resourceType,
        string resourceId,
        string key,
        CancellationToken cancellationToken) =>
        _db.Set<ResourceAttributeEntity>().SingleOrDefaultAsync(
            x => x.ResourceType == resourceType
                && x.ResourceId == resourceId
                && x.Name == key,
            cancellationToken);

    private void EnsureNativeSubjectStorage()
    {
        if (_layout != SubjectStorageLayout.Native)
            throw new InvalidOperationException(MappedSubjectMessage);
    }
}
