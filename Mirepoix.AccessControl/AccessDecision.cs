namespace Mirepoix.AccessControl;

public sealed record AccessDecision(
    AuthorizationResult Result,
    IReadOnlyList<PolicyHit> PolicyHits,
    DecisionStatus Status,
    string? PolicySetVersion);
