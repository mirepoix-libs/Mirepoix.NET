namespace Mirepoix.AccessControl;

/// <summary>
/// Reports how an <see cref="AccessDecision"/> was produced. Orthogonal to <see cref="AuthorizationResult"/>.
/// Explains the pipeline outcome, not the allow/deny effect.
/// HTTP PEPs typically succeed only on <see cref="AuthorizationResult.Allow"/> + <see cref="Success"/>.
/// </summary>
public enum DecisionStatus
{
    /// <summary>
    /// Marks that the kernel ran and the combination strategy produced an effect from one or more policy hits.
    /// </summary>
    Success,

    /// <summary>
    /// Marks that the kernel ran but no policy hit; checker applied the default effect (Deny in the default Engine path).
    /// Distinct from infrastructure failure: policies loaded and hydration succeeded.
    /// </summary>
    Defaulted,

    /// <summary>
    /// Marks that bundle hydration / resolver failed before evaluation.
    /// Decision is Deny with empty hits; the partial bundle was not evaluated.
    /// </summary>
    HydrationFailed,

    /// <summary>
    /// Marks that the policy source failed before evaluation.
    /// Decision is Deny with empty hits; policy set version is typically unset.
    /// </summary>
    PolicySourceFailed
}
