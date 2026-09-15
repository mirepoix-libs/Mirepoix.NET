namespace Mirepoix.AccessControl.Evaluation;

public sealed class DenyOverridesStrategy : ICombinationStrategy
{
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
