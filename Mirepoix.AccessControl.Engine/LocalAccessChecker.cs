using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl;

/// <summary>
/// Loads policies, hydrates the request into a bundle, then runs the evaluation kernel in-process.
/// Swallows provider failures into <see cref="AccessDecision"/> values (Deny + status). Does not catch
/// <see cref="OperationCanceledException"/>.
/// </summary>
public sealed class LocalAccessChecker : IAccessChecker
{
    private readonly IPolicySource _source;
    private readonly IBundleHydrator _hydrator;
    private readonly Authorizer _authorizer;

    /// <summary>
    /// Creates a checker. When <paramref name="strategy"/> is null, uses <see cref="DenyOverridesStrategy"/>.
    /// </summary>
    /// <param name="source">Policy set provider. Failures map to <see cref="DecisionStatus.PolicySourceFailed"/>.</param>
    /// <param name="hydrator">Request-to-bundle hydration. Failures map to <see cref="DecisionStatus.HydrationFailed"/>.</param>
    /// <param name="strategy">Optional combination strategy; default is deny-overrides.</param>
    public LocalAccessChecker(
        IPolicySource source,
        IBundleHydrator hydrator,
        ICombinationStrategy? strategy = null)
    {
        _source = source;
        _hydrator = hydrator;
        _authorizer = new Authorizer(strategy ?? new DenyOverridesStrategy());
    }

    /// <summary>
    /// Loads the policy set, hydrates <paramref name="request"/>, then authorizes.
    /// </summary>
    /// <param name="request">Caller-known subject, resource, operation, and context (may be partial).</param>
    /// <param name="cancellationToken">Passed to policy source and hydrator; cancellation is not converted into a decision.</param>
    /// <returns>
    /// Always an <see cref="AccessDecision"/> for non-cancellation outcomes. Status mapping:
    /// <list type="bullet">
    /// <item><description>Policy source exception (except cancel): Deny + <see cref="DecisionStatus.PolicySourceFailed"/> (empty hits, null version).</description></item>
    /// <item><description>Hydrator exception (except cancel): Deny + <see cref="DecisionStatus.HydrationFailed"/> (empty hits, version from the loaded set).</description></item>
    /// <item><description>Otherwise: delegates to <see cref="Authorizer"/> (<see cref="DecisionStatus.Success"/> or <see cref="DecisionStatus.Defaulted"/>).</description></item>
    /// </list>
    /// </returns>
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
