using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers;

public sealed class EntityFrameworkPolicySource : IPolicySource
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type _contextType;
    private readonly TimeSpan? _cacheTtl;
    private readonly object _gate = new();
    private PolicySet? _cached;
    private DateTimeOffset _cachedAt;

    public EntityFrameworkPolicySource(
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
        _cacheTtl = options.PolicyCacheTtl;
    }

    public async Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken)
    {
        if (TryGetCached(out var cached))
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = (DbContext)scope.ServiceProvider.GetRequiredService(_contextType);

        var row = await db.Set<PolicySetEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == AccessControlSchema.PolicySetSingletonId,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            throw new InvalidOperationException("No policy set row is configured in ac_policy_set.");

        var loaded = PolicySetStorageCodec.FromStorageJson(row.PayloadJson);
        lock (_gate)
        {
            _cached = loaded;
            _cachedAt = DateTimeOffset.UtcNow;
        }

        return loaded;
    }

    private bool TryGetCached(out PolicySet cached)
    {
        lock (_gate)
        {
            if (_cached is null)
            {
                cached = null!;
                return false;
            }

            if (_cacheTtl is { } ttl && DateTimeOffset.UtcNow - _cachedAt >= ttl)
            {
                cached = null!;
                return false;
            }

            cached = _cached;
            return true;
        }
    }
}
