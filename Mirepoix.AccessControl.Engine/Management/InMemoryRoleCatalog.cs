namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Stores roles in a dictionary-backed <see cref="IRoleCatalog"/>. <see cref="Add"/> upserts by id.
/// </summary>
public sealed class InMemoryRoleCatalog : IRoleCatalog
{
    private readonly Dictionary<string, Role> _roles = new();

    /// <inheritdoc />
    public void Add(Role role) => _roles[role.Id] = role;

    /// <inheritdoc />
    public void Remove(string roleId) => _roles.Remove(roleId);

    /// <inheritdoc />
    public IReadOnlyList<Role> List() => _roles.Values.ToList();
}
