using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

public class MemoryPolicySourceTests
{
    [Fact]
    public async Task MemoryPolicySource_returns_initial_set()
    {
        var source = new MemoryPolicySource(new PolicySet("v1", Array.Empty<Policy>()));
        var set = await source.GetPolicySetAsync(CancellationToken.None);
        Assert.Equal("v1", set.Version);
        Assert.Empty(set.Policies);
    }

    [Fact]
    public async Task MemoryPolicySource_swap_is_visible()
    {
        var source = new MemoryPolicySource(new PolicySet("v1", Array.Empty<Policy>()));
        source.Replace(new PolicySet("v2", Array.Empty<Policy>()));
        var set = await source.GetPolicySetAsync(CancellationToken.None);
        Assert.Equal("v2", set.Version);
    }
}
