namespace Mirepoix.AccessControl.Management;

public sealed class InMemorySodConstraintStore : ISodConstraintStore
{
    private readonly Dictionary<string, SodConstraint> _constraints = new();

    public void Add(SodConstraint constraint) => _constraints[constraint.Id] = constraint;

    public void Remove(string id) => _constraints.Remove(id);

    public IReadOnlyList<SodConstraint> List() => _constraints.Values.OrderBy(c => c.Id).ToList();
}
