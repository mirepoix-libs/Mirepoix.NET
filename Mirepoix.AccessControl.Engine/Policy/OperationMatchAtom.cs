using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Policy;

public sealed class OperationMatchAtom : IAtom
{
    public OperationMatchAtom(Operation pattern)
    {
        Pattern = pattern;
    }

    public Operation Pattern { get; }

    public string Name => "operation-match";

    public bool IsSatisfied(AuthorizationBundle bundle) =>
        bundle.Operation.Matches(Pattern);
}
