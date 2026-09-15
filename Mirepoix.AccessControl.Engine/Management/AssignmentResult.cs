namespace Mirepoix.AccessControl.Management;

public sealed record AssignmentResult(
    AssignmentOutcome Outcome,
    string? ConstraintId = null,
    IReadOnlyList<string>? Roles = null)
{
    public static AssignmentResult Assigned() =>
        new(AssignmentOutcome.Assigned);

    public static AssignmentResult SodConflict(string constraintId, IReadOnlyList<string> roles) =>
        new(AssignmentOutcome.SodConflict, constraintId, roles);
}
