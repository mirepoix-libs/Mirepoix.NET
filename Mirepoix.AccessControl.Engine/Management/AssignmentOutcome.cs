namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Names the outcome of a role assignment attempt. Conflicts are returned, not thrown.
/// </summary>
public enum AssignmentOutcome
{
    /// <summary>Marks that the role was assigned (or was already held).</summary>
    Assigned,

    /// <summary>Marks that assignment was blocked by a SoD constraint; store unchanged.</summary>
    SodConflict
}
