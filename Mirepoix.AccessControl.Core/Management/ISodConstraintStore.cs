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

    /// <summary>
    /// Does the same as <see cref="Add"/> asynchronously.
    /// </summary>
    /// <param name="constraint">Constraint to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddAsync(SodConstraint constraint, CancellationToken cancellationToken = default);

    /// <summary>
    /// Does the same as <see cref="Remove"/> asynchronously.
    /// </summary>
    /// <param name="id">Constraint id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Does the same as <see cref="List"/> asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<SodConstraint>> ListAsync(CancellationToken cancellationToken = default);
}
