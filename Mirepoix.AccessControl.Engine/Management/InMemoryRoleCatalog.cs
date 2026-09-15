namespace Mirepoix.AccessControl.Management;

public sealed class InMemoryRoleCatalog : IRoleCatalog
{
    private readonly Dictionary<string, Role> _roles = new();

    public void Add(Role role) => _roles[role.Id] = role;

    public void Remove(string roleId) => _roles.Remove(roleId);

    public IReadOnlyList<Role> List() => _roles.Values.ToList();
}
