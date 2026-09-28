using System.Net;
using System.Text;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management.Server.Http.Tests;

public sealed class PinnedPolicyEndpointTests
{
    [Fact]
    public async Task Get_appends_the_pin_without_changing_version()
    {
        await using var app = await StartAsync();
        using var client = AuthenticatedClient(app);

        var response = await client.GetAsync("/access-control/policy-set");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var set = PolicySerializers.FromJson(await response.Content.ReadAsStringAsync());
        Assert.Equal("v3", set.Version);
        Assert.Equal("builtin.policy-admin", set.Policies[^1].Id);
    }

    [Fact]
    public async Task Put_of_exact_pin_does_not_store_it()
    {
        await using var app = await StartAsync();
        using var client = AuthenticatedClient(app);
        var body = PolicySerializers.ToJson(new PolicySet("v4", [Editable(), Pin()]));

        var response = await client.PutAsync("/access-control/policy-set", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var set = await ReadSetAsync(app);
        Assert.Equal(new[] { "editable" }, set.Policies.Select(policy => policy.Id));
        Assert.Equal("builtin.policy-admin", (await GetBodyAsync(client)).Policies[^1].Id);
    }

    [Fact]
    public async Task Put_that_changes_a_pin_is_400_and_keeps_the_store()
    {
        await using var app = await StartAsync();
        using var client = AuthenticatedClient(app);
        var body = PolicySerializers.ToJson(new PolicySet("v5", [ChangedPin()]));

        var response = await client.PutAsync("/access-control/policy-set", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Policy 'builtin.policy-admin' is pinned.", await response.Content.ReadAsStringAsync());
        Assert.Equal("v3", (await ReadSetAsync(app)).Version);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var store = new Store(new PolicySet("v3", [Editable()]));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(store);
        builder.Services.AddSingleton<IPolicySource>(store);
        builder.Services.AddSingleton<IPolicySetEditor>(store);
        ManagementTestCaller.Register(builder.Services, new PolicySetChecker());
        builder.Services.AddAccessControlManagementServerHttp(options =>
        {
            options.AddPolicySet();
            options.PinPolicy(Pin());
        });
        var app = builder.Build();
        ManagementTestCaller.Use(app);
        app.MapAccessControlManagement();
        await app.StartAsync();
        return app;
    }

    private static HttpClient AuthenticatedClient(WebApplication app) =>
        ManagementTestCaller.Client(app);

    private static Task<PolicySet> ReadSetAsync(WebApplication app) =>
        Task.FromResult(app.Services.GetRequiredService<Store>().Current);

    private static async Task<PolicySet> GetBodyAsync(HttpClient client)
    {
        var response = await client.GetAsync("/access-control/policy-set");
        response.EnsureSuccessStatusCode();
        return PolicySerializers.FromJson(await response.Content.ReadAsStringAsync());
    }

    private static PolicyModel Pin() => new(
        "builtin.policy-admin",
        AuthorizationResult.Allow,
        "Policy admins may read and replace the policy set.",
        [new RoleMembershipAtom(["PolicyAdmin"]), new OperationMatchAtom(Operation.Parse("access-control:policy-set:*"))]);

    private static PolicyModel ChangedPin() => new(
        "builtin.policy-admin",
        AuthorizationResult.Deny,
        "Policy admins may read and replace the policy set.",
        [new RoleMembershipAtom(["PolicyAdmin"])]);

    private static PolicyModel Editable() => new(
        "editable",
        AuthorizationResult.Deny,
        null,
        [new RoleMembershipAtom(["guest"])]);

    private sealed class PolicySetChecker : IAccessChecker
    {
        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken)
        {
            var allowed = request.Operation.Value is "access-control:policy-set:read" or "access-control:policy-set:replace";
            return Task.FromResult(new AccessDecision(
                allowed ? AuthorizationResult.Allow : AuthorizationResult.Deny,
                [],
                allowed ? DecisionStatus.Success : DecisionStatus.Defaulted,
                "v"));
        }
    }

    private sealed class Store : IPolicySource, IPolicySetEditor
    {
        public Store(PolicySet set) => Current = set;

        public PolicySet Current { get; private set; }

        public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public void Replace(PolicySet set) => Current = set;

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
        {
            Current = set;
            return Task.CompletedTask;
        }
    }
}
