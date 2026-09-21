namespace Mirepoix.AccessControl;

/// <summary>
/// Returns the full outcome of an access check: effect, ordered policy-hit trace, production status, and policy-set version.
/// </summary>
/// <param name="Result">Holds the final allow/deny after combination (or default/failure mapping).</param>
/// <param name="PolicyHits">
/// Lists ordered hits for policies that matched (all atoms satisfied). Empty when no policy matched
/// or when evaluation never ran (<see cref="DecisionStatus.HydrationFailed"/> /
/// <see cref="DecisionStatus.PolicySourceFailed"/>). Explains both allows and denies.
/// </param>
/// <param name="Status">Reports whether the kernel succeeded, defaulted, or stopped on a provider failure.</param>
/// <param name="PolicySetVersion">
/// Carries the version string from the loaded policy set for audit/correlation. Null when the policy set
/// was never obtained (e.g. <see cref="DecisionStatus.PolicySourceFailed"/>) or the source
/// supplied no version.
/// </param>
public sealed record AccessDecision(
    AuthorizationResult Result,
    IReadOnlyList<PolicyHit> PolicyHits,
    DecisionStatus Status,
    string? PolicySetVersion);
