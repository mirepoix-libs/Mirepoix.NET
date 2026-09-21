namespace Mirepoix.AccessControl.Evaluation;

/// <summary>
/// Returns Allow when any hit is Allow; otherwise Deny if any Deny hit exists; otherwise
/// <c>defaultResult</c>. Order-independent among override strategies.
/// </summary>
public sealed class PermitOverridesStrategy : ICombinationStrategy
{
    /// <inheritdoc />
    public AuthorizationResult Combine(IReadOnlyList<PolicyHit> hits, AuthorizationResult defaultResult)
    {
        var sawDeny = false;
        foreach (var hit in hits)
        {
            if (hit.Effect == AuthorizationResult.Allow)
                return AuthorizationResult.Allow;
            if (hit.Effect == AuthorizationResult.Deny)
                sawDeny = true;
        }

        return sawDeny ? AuthorizationResult.Deny : defaultResult;
    }
}
