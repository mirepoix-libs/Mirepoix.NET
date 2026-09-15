namespace Mirepoix.AccessControl.Evaluation;

public interface ICombinationStrategy
{
    AuthorizationResult Combine(IReadOnlyList<PolicyHit> hits, AuthorizationResult defaultResult);
}
