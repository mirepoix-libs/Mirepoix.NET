namespace Mirepoix.AccessControl;

public interface IAccessChecker
{
    Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
