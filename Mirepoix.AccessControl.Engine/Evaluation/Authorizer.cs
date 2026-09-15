using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Evaluation;

public sealed class Authorizer
{
    private readonly ICombinationStrategy _strategy;

    public Authorizer(ICombinationStrategy strategy)
    {
        _strategy = strategy;
    }

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
