using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Policy;

public sealed class RoleMembershipAtom : IAtom
{
    public RoleMembershipAtom(IReadOnlyCollection<string> roles)
    {
        Roles = roles;
    }

    public IReadOnlyCollection<string> Roles { get; }

    public string Name => "role-membership";

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
