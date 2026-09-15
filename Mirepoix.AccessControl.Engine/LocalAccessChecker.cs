using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl;

public sealed class LocalAccessChecker : IAccessChecker
{
    private readonly IPolicySource _source;
    private readonly IBundleHydrator _hydrator;
    private readonly Authorizer _authorizer;

    public LocalAccessChecker(
        IPolicySource source,
        IBundleHydrator hydrator,
        ICombinationStrategy? strategy = null)
    {
        _source = source;
        _hydrator = hydrator;
        _authorizer = new Authorizer(strategy ?? new DenyOverridesStrategy());
    }

    public async Task<AccessDecision> CheckAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        PolicySet set;
        try
        {
            set = await _source.GetPolicySetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AccessDecision(
                AuthorizationResult.Deny,
                Array.Empty<PolicyHit>(),
                DecisionStatus.PolicySourceFailed,
                null);
        }

        AuthorizationBundle bundle;
        try
        {
            bundle = await _hydrator.HydrateAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new AccessDecision(
                AuthorizationResult.Deny,
                Array.Empty<PolicyHit>(),
                DecisionStatus.HydrationFailed,
                set.Version);
        }

        return _authorizer.Authorize(bundle, set);
    }
}
