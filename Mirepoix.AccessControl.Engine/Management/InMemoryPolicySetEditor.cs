using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Management;

public sealed class InMemoryPolicySetEditor : IPolicySetEditor
{
    private readonly MemoryPolicySource _source;

    public InMemoryPolicySetEditor(MemoryPolicySource source)
    {
        _source = source;
    }

    public void Replace(PolicySet set) => _source.Replace(set);
}
