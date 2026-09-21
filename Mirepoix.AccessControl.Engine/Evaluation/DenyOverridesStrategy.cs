namespace Mirepoix.AccessControl.Evaluation;

/// <summary>
/// Returns Deny when any hit is Deny; otherwise Allow if any Allow hit exists; otherwise
/// <c>defaultResult</c>. Order-independent among override strategies.
/// Default strategy used by <see cref="LocalAccessChecker"/> when none is supplied.
/// </summary>
public sealed class DenyOverridesStrategy : ICombinationStrategy
{
    /// <inheritdoc />
    public AuthorizationResult Combine(IReadOnlyList<PolicyHit> hits, AuthorizationResult defaultResult)
    {
        var sawAllow = false;
        foreach (var hit in hits)
        {
            if (hit.Effect == AuthorizationResult.Deny)
                return AuthorizationResult.Deny;
            if (hit.Effect == AuthorizationResult.Allow)
                sawAllow = true;
        }

        return sawAllow ? AuthorizationResult.Allow : defaultResult;
    }
}
