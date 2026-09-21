namespace Mirepoix.AccessControl.Evaluation;

/// <summary>
/// Returns the effect of the first hit in policy-set order. Empty hits yield <c>defaultResult</c>.
/// Unlike override strategies, hit list order matters.
/// </summary>
public sealed class FirstApplicableStrategy : ICombinationStrategy
{
    /// <inheritdoc />
    public AuthorizationResult Combine(IReadOnlyList<PolicyHit> hits, AuthorizationResult defaultResult)
    {
        return hits.Count > 0 ? hits[0].Effect : defaultResult;
    }
}
