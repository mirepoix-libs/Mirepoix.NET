namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores SoD constraints in a dictionary-backed <see cref="ISodConstraintStore"/>. <see cref="List"/> returns constraints ordered by id.
/// </summary>
public sealed class InMemorySodConstraintStore : ISodConstraintStore
{
    private readonly Dictionary<string, SodConstraint> _constraints = new();

    /// <inheritdoc />
    public void Add(SodConstraint constraint) => _constraints[constraint.Id] = constraint;

    /// <inheritdoc />
    public void Remove(string id) => _constraints.Remove(id);

    /// <inheritdoc />
    public IReadOnlyList<SodConstraint> List() => _constraints.Values.OrderBy(c => c.Id).ToList();

    /// <inheritdoc />
    public Task AddAsync(SodConstraint constraint, CancellationToken cancellationToken = default)
    {
        Add(constraint);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        Remove(id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<SodConstraint>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(List());
}
