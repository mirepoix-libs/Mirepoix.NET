using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.SqlServer.Internal.Data;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Reads the singleton <c>ac_policy_set</c> row and deserializes <c>payload_json</c> as an
/// <see cref="IPolicySource"/>. Caches the loaded set in-process per <see cref="SqlServerProviderOptions.PolicyCacheTtl"/>.
/// </summary>
public sealed class SqlServerPolicySource : IPolicySource
{
    private readonly PolicySetReader _reader;
    private readonly TimeSpan? _cacheTtl;
    private readonly object _gate = new();
    private PolicySet? _cached;
    private DateTimeOffset _cachedAt;

    /// <summary>
    /// Creates a source from <paramref name="options"/> (connection string + cache TTL).
    /// </summary>
    /// <param name="options">Provider options.</param>
    public SqlServerPolicySource(SqlServerProviderOptions options)
        : this(new PolicySetReader(new SqlConnectionFactory(options.ConnectionString)), options.PolicyCacheTtl)
    {
    }

    /// <summary>
    /// Creates a source with an injected reader (package/tests).
    /// </summary>
    internal SqlServerPolicySource(PolicySetReader reader, TimeSpan? cacheTtl)
    {
        _reader = reader;
        _cacheTtl = cacheTtl;
    }

    /// <summary>
    /// Returns the cached set when fresh; otherwise loads from SQL and updates the cache under a lock.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for the database read.</param>
    /// <returns>Current <see cref="PolicySet"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the singleton policy set row is missing.</exception>
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
