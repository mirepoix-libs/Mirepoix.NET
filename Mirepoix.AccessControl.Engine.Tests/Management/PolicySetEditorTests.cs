using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class PolicySetEditorTests
{
    [Fact]
    public async Task Replace_swaps_active_policy_set_on_source()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        var set = new PolicySet("v2", Array.Empty<Policy>());

        mgr.Policies.Replace(set);

        var loaded = await mgr.PolicySource.GetPolicySetAsync(CancellationToken.None);
        Assert.Same(set, loaded);
        Assert.Equal("v2", loaded.Version);
    }
}
