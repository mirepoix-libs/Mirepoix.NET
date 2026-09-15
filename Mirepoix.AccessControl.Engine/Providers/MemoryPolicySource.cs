using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers;

public sealed class MemoryPolicySource : IPolicySource
{
    private PolicySet _set;

    public MemoryPolicySource(PolicySet initial)
    {
        _set = initial;
    }

    public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_set);

    internal void Replace(PolicySet set) => _set = set;
}
