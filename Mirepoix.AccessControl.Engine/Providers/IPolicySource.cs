using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers;

public interface IPolicySource
{
    Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken);
}
