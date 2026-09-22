using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Loads the current <see cref="PolicySet"/> on demand. No per-request keying: policies are global.
/// Change propagation is the source's concern; the checker asks for the set on each decision.
/// </summary>
public interface IPolicySource
{
    /// <summary>
    /// Returns the current policy set.
    /// </summary>
    /// <param name="cancellationToken">Cancellation for I/O-bound load.</param>
    /// <returns>Policy set used for this decision (version flows to <see cref="AccessDecision.PolicySetVersion"/>).</returns>
    Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken);
}
