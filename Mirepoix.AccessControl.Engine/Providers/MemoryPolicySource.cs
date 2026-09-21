using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds an in-memory <see cref="IPolicySource"/>. Public surface is read-only; whole-set swap is
/// <see cref="Replace"/> (internal) for the management plane and tests.
/// </summary>
public sealed class MemoryPolicySource : IPolicySource
{
    private PolicySet _set;

    /// <summary>
    /// Creates a source seeded with <paramref name="initial"/>.
    /// </summary>
    /// <param name="initial">Initial policy set returned by <see cref="GetPolicySetAsync"/>.</param>
    public MemoryPolicySource(PolicySet initial)
    {
        _set = initial;
    }

    /// <summary>
    /// Returns the current in-memory set (no copy).
    /// </summary>
    /// <param name="cancellationToken">Unused; completed synchronously.</param>
    public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_set);

    /// <summary>
    /// Replaces the entire policy set reference atomically (whole-set swap, not a merge).
    /// Used by <c>InMemoryPolicySetEditor</c> and tests via InternalsVisibleTo.
    /// </summary>
    /// <param name="set">New policy set to serve on subsequent reads.</param>
    internal void Replace(PolicySet set) => _set = set;
}
