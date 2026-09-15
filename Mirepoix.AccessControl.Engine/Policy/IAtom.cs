using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Policy;

public interface IAtom
{
    string Name { get; }

    bool IsSatisfied(AuthorizationBundle bundle);
}
