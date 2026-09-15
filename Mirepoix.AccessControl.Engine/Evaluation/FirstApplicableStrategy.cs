namespace Mirepoix.AccessControl.Evaluation;

public sealed class FirstApplicableStrategy : ICombinationStrategy
{
    public AuthorizationResult Combine(IReadOnlyList<PolicyHit> hits, AuthorizationResult defaultResult)
    {
        return hits.Count > 0 ? hits[0].Effect : defaultResult;
    }
}
