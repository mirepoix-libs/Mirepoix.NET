namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Selects which subject and role tables the access-control library owns for a subject mapping configuration.
/// </summary>
public enum SubjectStorageLayout
{
    /// <summary>Uses library-owned subject headers, attributes, and role assignments.</summary>
    Native,

    /// <summary>Uses mapped subject storage with library-owned role assignments.</summary>
    MappedLibraryRoles,

    /// <summary>Uses mapped subject storage whose mapped entities also own role assignments.</summary>
    MappedAppOwnedRoles,
}

/// <summary>
/// Resolves subject-table ownership from configured entity maps and their explicit role members.
/// </summary>
public static class SubjectStorageLayoutResolver
{
    /// <summary>
    /// Selects native storage when no maps exist, app-owned role storage when any map has an explicit
    /// role member, and library-owned role storage for all other mapped configurations.
    /// Type disposition does not affect role-storage ownership.
    /// </summary>
    /// <param name="mapping">Subject mapping configuration to inspect.</param>
    /// <returns>The storage layout required by the mapping.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapping"/> is null.</exception>
    public static SubjectStorageLayout Resolve(SubjectMappingOptions mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        if (!mapping.HasMaps)
            return SubjectStorageLayout.Native;

        return mapping.Maps.Any(map => map.RoleMembers.Count > 0)
            ? SubjectStorageLayout.MappedAppOwnedRoles
            : SubjectStorageLayout.MappedLibraryRoles;
    }
}
