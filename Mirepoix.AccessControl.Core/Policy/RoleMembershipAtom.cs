namespace Mirepoix.AccessControl.Policy;

/// <summary>
/// Matches when the subject holds at least one of the configured roles (OR across <see cref="Roles"/>).
/// Reads <see cref="Subject.Roles"/> only; does not look at attributes.
/// </summary>
public sealed class RoleMembershipAtom : IAtom
{
    /// <summary>
    /// Creates an atom that succeeds if any listed role is present on the subject.
    /// </summary>
    /// <param name="roles">Lists candidate role names. Empty collection never satisfies.</param>
    public RoleMembershipAtom(IReadOnlyCollection<string> roles)
    {
        Roles = roles;
    }

    /// <summary>Lists role names accepted by this atom (any one is enough).</summary>
    public IReadOnlyCollection<string> Roles { get; }

    /// <inheritdoc />
    public string Name => "role-membership";

    /// <summary>
    /// Returns <see langword="true"/> if <see cref="Subject.Roles"/> contains any entry from <see cref="Roles"/>.
    /// </summary>
    /// <param name="bundle">Bundle whose subject roles are checked.</param>
    public bool IsSatisfied(AuthorizationBundle bundle)
    {
        foreach (var role in Roles)
        {
            if (bundle.Subject.Roles.Contains(role))
                return true;
        }

        return false;
    }
}
