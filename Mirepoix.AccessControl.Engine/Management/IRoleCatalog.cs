namespace Mirepoix.AccessControl.Management;

public interface IRoleCatalog
{
    void Add(Role role);

    void Remove(string roleId);

    IReadOnlyList<Role> List();
}
