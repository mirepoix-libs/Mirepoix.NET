namespace Mirepoix.AccessControl.Management;

public interface ISodConstraintStore
{
    void Add(SodConstraint constraint);

    void Remove(string id);

    IReadOnlyList<SodConstraint> List();
}
