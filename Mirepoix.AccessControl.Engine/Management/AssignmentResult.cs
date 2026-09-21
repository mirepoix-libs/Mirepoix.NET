namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Holds the result of <see cref="IRoleAssignmentStore.Assign"/>. On conflict, <see cref="ConstraintId"/> and
/// <see cref="Roles"/> describe which constraint and overlapping roles blocked the assign.
/// </summary>
/// <param name="Outcome">Holds Assigned or SodConflict.</param>
/// <param name="ConstraintId">Names the conflicting constraint id when <see cref="Outcome"/> is SodConflict; otherwise null.</param>
/// <param name="Roles">Lists overlapping mutually exclusive roles on conflict; otherwise null.</param>
public sealed record AssignmentResult(
    AssignmentOutcome Outcome,
    string? ConstraintId = null,
    IReadOnlyList<string>? Roles = null)
{
    /// <summary>Builds a successful assignment result.</summary>
    public static AssignmentResult Assigned() =>
        new(AssignmentOutcome.Assigned);

    /// <summary>
    /// Builds a SoD conflict result.
    /// </summary>
    /// <param name="constraintId">Names the blocking constraint id.</param>
    /// <param name="roles">Lists roles from the proposed set that overlap the constraint.</param>
    public static AssignmentResult SodConflict(string constraintId, IReadOnlyList<string> roles) =>
        new(AssignmentOutcome.SodConflict, constraintId, roles);
}
