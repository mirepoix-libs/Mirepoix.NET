using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Evaluation;

/// <summary>
/// Evaluates policies by ANDing atoms into ordered hits, then combining them.
/// Sync and side-effect free. Emits only <see cref="DecisionStatus.Success"/> or
/// <see cref="DecisionStatus.Defaulted"/> (never hydration/policy-source failure statuses).
/// Always passes <see cref="AuthorizationResult.Deny"/> as the strategy default.
/// </summary>
public sealed class Authorizer
{
    private readonly ICombinationStrategy _strategy;

    /// <summary>
    /// Creates a kernel bound to <paramref name="strategy"/>.
    /// </summary>
    /// <param name="strategy">Hit combination strategy.</param>
    public Authorizer(ICombinationStrategy strategy)
    {
        _strategy = strategy;
    }

    /// <summary>
    /// Evaluates <paramref name="policies"/> against <paramref name="bundle"/>.
    /// No hits: Deny + <see cref="DecisionStatus.Defaulted"/>. Otherwise: combined effect +
    /// <see cref="DecisionStatus.Success"/>. Version is always <see cref="PolicySet.Version"/>.
    /// </summary>
    /// <param name="bundle">Hydrated evaluation input.</param>
    /// <param name="policies">Policy set to evaluate in list order.</param>
    /// <returns>Decision with full hit list (including when Defaulted with empty hits).</returns>
    public AccessDecision Authorize(AuthorizationBundle bundle, PolicySet policies)
    {
        var hits = new List<PolicyHit>();
        foreach (var policy in policies.Policies)
        {
            if (!AllAtomsSatisfied(policy.Atoms, bundle))
                continue;

            hits.Add(new PolicyHit(policy.Id, policy.Effect, policy.Description));
        }

        if (hits.Count == 0)
        {
            return new AccessDecision(
                AuthorizationResult.Deny,
                hits,
                DecisionStatus.Defaulted,
                policies.Version);
        }

        var result = _strategy.Combine(hits, AuthorizationResult.Deny);
        return new AccessDecision(result, hits, DecisionStatus.Success, policies.Version);
    }

    private static bool AllAtomsSatisfied(IReadOnlyList<IAtom> atoms, AuthorizationBundle bundle)
    {
        foreach (var atom in atoms)
        {
            if (!atom.IsSatisfied(bundle))
                return false;
        }

        return true;
    }
}
