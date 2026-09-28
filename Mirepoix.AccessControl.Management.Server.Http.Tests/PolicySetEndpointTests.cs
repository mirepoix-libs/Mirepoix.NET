using System.Net;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Management.Server.Http.Tests;

public sealed class PolicySetEndpointTests
{
    [Fact]
    public async Task Get_returns_the_current_policy_set()
    {
        var source = new FixedPolicySource(CurrentSet());
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPolicySource>(source);
        builder.Services.AddSingleton<IPolicySetEditor, UnusedEditor>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddPolicySet());
        await using var app = builder.Build();
        app.MapAccessControlManagement();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/access-control/policy-set");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var loaded = PolicySerializers.FromJson(await response.Content.ReadAsStringAsync());
        Assert.Equal("v7", loaded.Version);
        var atom = Assert.IsType<RoleMembershipAtom>(Assert.Single(Assert.Single(loaded.Policies).Atoms));
        Assert.Equal("admin", Assert.Single(atom.Roles));
    }

    [Fact]
    public void MapAccessControlManagement_ThrowsWhenPolicySourceIsMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IPolicySetEditor, UnusedEditor>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddPolicySet());
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => app.MapAccessControlManagement());

        Assert.Contains("Policy set management HTTP slice requires IPolicySource.", exception.Message);
    }

    private static PolicySet CurrentSet() => new(
        "v7",
        [new PolicyModel("p", AuthorizationResult.Allow, null, [new RoleMembershipAtom(["admin"])])]);

    private sealed class FixedPolicySource(PolicySet set) : IPolicySource
    {
        public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) => Task.FromResult(set);
    }

    private sealed class UnusedEditor : IPolicySetEditor
    {
        public void Replace(PolicySet set) => throw new InvalidOperationException("GET does not replace.");

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("GET does not replace.");
    }
}
