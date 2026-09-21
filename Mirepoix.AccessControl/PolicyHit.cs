namespace Mirepoix.AccessControl;

/// <summary>
/// Records one policy that matched the bundle (all atoms satisfied). Forms the decision "receipt"
/// for allows and denies (e.g. "denied because policy X"). Hits are collected in policy-set
/// order before the combination strategy picks a winner; a hit is not by itself the final effect.
/// </summary>
/// <param name="PolicyId">Names the stable id of the matching policy.</param>
/// <param name="Effect">Holds that policy's configured effect (<see cref="AuthorizationResult.Allow"/> or <see cref="AuthorizationResult.Deny"/>).</param>
/// <param name="Description">Carries an optional human-readable policy description for traces/UI; may be null.</param>
public sealed record PolicyHit(
    string PolicyId,
    AuthorizationResult Effect,
    string? Description);
