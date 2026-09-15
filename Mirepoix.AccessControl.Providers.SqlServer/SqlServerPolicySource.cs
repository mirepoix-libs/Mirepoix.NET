using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

public sealed class SqlServerPolicySource : IPolicySource
{
    private readonly PolicySetReader _reader;
    private readonly TimeSpan? _cacheTtl;
    private readonly object _gate = new();
    private PolicySet? _cached;
    private DateTimeOffset _cachedAt;

    public SqlServerPolicySource(SqlServerProviderOptions options)
        : this(new PolicySetReader(new SqlConnectionFactory(options.ConnectionString)), options.PolicyCacheTtl)
    {
    }

    internal SqlServerPolicySource(PolicySetReader reader, TimeSpan? cacheTtl)
    {
        _reader = reader;
        _cacheTtl = cacheTtl;
    }

    public async Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken)
    {
        if (TryGetCached(out var cached))
            return cached;

        var loaded = await _reader.ReadCurrentAsync(cancellationToken).ConfigureAwait(false);
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
