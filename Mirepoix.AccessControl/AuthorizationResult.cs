namespace Mirepoix.AccessControl;

/// <summary>
/// Names the Allow / Deny effect used for policy effects, combination outcomes, and <see cref="AccessDecision.Result"/>.
/// Does not encode <i>why</i> a deny happened.
/// Full summary of a decision requires <see cref="DecisionStatus"/> and <see cref="PolicyHit"/>.
/// </summary>
public enum AuthorizationResult
{
    /// <summary>Permits access via the winning policy combination.</summary>
    Allow,

    /// <summary>
    /// Refuses access. May mean an explicit deny policy, default deny (no hit),
    /// or an infrastructure failure mapped to deny by the checker; inspect
    /// <see cref="DecisionStatus"/> to distinguish.
    /// </summary>
    Deny
}
