using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Replaces the singleton policy row with a complete encoded policy set.
/// </summary>
/// <remarks>
/// Sync methods block on async database work and should not be used on thread-pool-sensitive paths.
/// </remarks>
public sealed class EntityFrameworkPolicySetEditor : IPolicySetEditor
{
    private readonly DbContext _db;

    /// <summary>
    /// Creates a scoped policy editor over the supplied access-control context.
    /// </summary>
    /// <param name="db">Context whose singleton policy row is updated.</param>
    public EntityFrameworkPolicySetEditor(AccessControlDbContext db)
        : this((DbContext)db)
    {
    }

    internal EntityFrameworkPolicySetEditor(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <inheritdoc />
    public void Replace(PolicySet set) => ReplaceAsync(set).GetAwaiter().GetResult();

    /// <inheritdoc />
    public async Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(set);
        var row = await _db.Set<PolicySetEntity>()
            .SingleOrDefaultAsync(
                x => x.Id == AccessControlSchema.PolicySetSingletonId,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new PolicySetEntity { Id = AccessControlSchema.PolicySetSingletonId };
            _db.Add(row);
        }

        row.Version = set.Version;
        row.PayloadJson = PolicySetStorageCodec.ToStorageJson(set);
        row.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
