using Mirepoix.AccessControl.Authorization.AspNetCore;
using Mirepoix.AccessControl.Policy;
using Microsoft.Extensions.DependencyInjection;

public class AccessControlServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAccessControl_allows_pre_registered_IAccessChecker_without_policy_source()
    {
        var services = new ServiceCollection();
        var fake = new FakeChecker();
        services.AddSingleton<IAccessChecker>(fake);

        services.AddAccessControl();

        using var sp = services.BuildServiceProvider();
        Assert.Same(fake, sp.GetRequiredService<IAccessChecker>());
    }

    [Fact]
    public void AddAccessControl_throws_without_policy_source()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControl());

        Assert.Contains("IPolicySource", ex.Message);
    }

    [Fact]
    public void AddAccessControl_registers_checker_with_memory_policy_set()
    {
        var services = new ServiceCollection();
        var set = new PolicySet("v1", Array.Empty<Policy>());

        services.AddAccessControl(ac => ac.UseMemoryPolicySet(set));

        using var sp = services.BuildServiceProvider();
        var checker = sp.GetRequiredService<IAccessChecker>();
        Assert.NotNull(checker);
        Assert.NotNull(sp.GetRequiredService<IClaimsPrincipalMapper>());
        Assert.Equal(AccessControlOptions.PolicyName, "MirepoixAccess");
    }

    private sealed class FakeChecker : IAccessChecker
    {
        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AccessDecision(
                AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1"));
    }
}
