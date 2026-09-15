namespace Mirepoix.AccessControl.Evaluation;

public sealed class PermitOverridesStrategy : ICombinationStrategy
{
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
