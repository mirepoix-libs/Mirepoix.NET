namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores SoD constraints consulted by role assignment.
/// </summary>
public interface ISodConstraintStore
{
    /// <summary>
    /// Adds or replaces a constraint by <see cref="SodConstraint.Id"/>.
    /// </summary>
    /// <param name="constraint">Constraint to store.</param>
    void Add(SodConstraint constraint);

    /// <summary>
    /// Removes the constraint with <paramref name="id"/> if present.
    /// </summary>
    /// <param name="id">Constraint id.</param>
    void Remove(string id);

    /// <summary>
    /// Returns all constraints (implementation may order them).
    /// </summary>
    IReadOnlyList<SodConstraint> List();
}
