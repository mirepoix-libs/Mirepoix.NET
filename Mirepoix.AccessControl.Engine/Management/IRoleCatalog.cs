namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Holds a mutable catalog of defined roles (not subject assignments).
/// </summary>
public interface IRoleCatalog
{
    /// <summary>
    /// Adds or replaces a role by <see cref="Role.Id"/>.
    /// </summary>
    /// <param name="role">Role to store.</param>
    void Add(Role role);

    /// <summary>
    /// Removes the role with <paramref name="roleId"/> if present.
    /// </summary>
    /// <param name="roleId">Role id to remove.</param>
    void Remove(string roleId);

    /// <summary>
    /// Returns a snapshot of all catalogued roles.
    /// </summary>
    IReadOnlyList<Role> List();
}
