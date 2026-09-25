using System.Security.Claims;
using Mirepoix.AccessControl.Authorization.AspNetCore;
using Mirepoix.AccessControl.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

public class AccessControlServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddAccessControl_allows_pre_registered_IAccessChecker_without_policy_source()
    {
        var services = new ServiceCollection();
        var fake = new FakeChecker();
        services.AddAccessControlOperation("doc:edit");
        services.AddSingleton<IAccessChecker>(fake);

        services.AddAccessControl();

        using var sp = services.BuildServiceProvider();
        var checker = sp.GetRequiredService<IAccessChecker>();
        Assert.IsType<PublishedOperationAccessChecker>(checker);

        var decision = await checker.CheckAsync(RequestFor("doc:edit"), CancellationToken.None);
        Assert.Equal(AuthorizationResult.Allow, decision.Result);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            checker.CheckAsync(RequestFor("doc:missing"), CancellationToken.None));
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

    private static AuthorizationRequest RequestFor(string operation)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "u1"),
                    new Claim(ClaimTypes.Role, "EDITOR")
                },
                authenticationType: "test"))
        };
        var seed = new DefaultClaimsPrincipalMapper().CreateSeed(http);
        return new AuthorizationRequest(
            seed.Subject,
            new Resource(string.Empty, string.Empty, new Dictionary<string, object?>()),
            Operation.Parse(operation),
            seed.Context);
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
