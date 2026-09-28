using System.Net;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management.Server.Http.Tests;

public sealed class ManagementAccessTests
{
    [Fact]
    public void Map_throws_when_checker_missing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IPolicySource>(new FixedSource(new PolicySet("v", [])));
        builder.Services.AddSingleton<IPolicySetEditor, StoreEditor>();
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddPolicySet());
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlManagement());

        Assert.Equal("Management HTTP routes require IAccessChecker.", exception.Message);
    }

    [Fact]
    public async Task Anonymous_policy_set_get_is_401()
    {
        var (app, checker) = await StartAsync(AuthorizationResult.Allow);
        await using (app)
        {
            using var client = app.GetTestClient();
            var response = await client.GetAsync("/access-control/policy-set");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, checker.Calls);
        }
    }

    [Fact]
    public async Task Authenticated_without_allow_is_403()
    {
        var (app, checker) = await StartAsync(AuthorizationResult.Deny);
        await using (app)
        {
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Test ada:PolicyAdmin");
            var response = await client.GetAsync("/access-control/policy-set");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("access-control:policy-set:read", checker.LastOperation);
        }
    }

    [Fact]
    public async Task Authenticated_allow_reaches_the_handler()
    {
        var (app, _) = await StartAsync(AuthorizationResult.Allow);
        await using (app)
        {
            using var client = app.GetTestClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Test ada:PolicyAdmin");
            var response = await client.GetAsync("/access-control/policy-set");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task<(WebApplication App, ScriptedChecker Checker)> StartAsync(AuthorizationResult result)
    {
        var checker = new ScriptedChecker(result);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPolicySource>(new FixedSource(new PolicySet("v3", [Editable()])));
        builder.Services.AddSingleton<IPolicySetEditor, StoreEditor>();
        ManagementTestCaller.Register(builder.Services, checker);
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddPolicySet());
        var app = builder.Build();
        ManagementTestCaller.Use(app);
        app.MapAccessControlManagement();
        await app.StartAsync();
        return (app, checker);
    }

    private static PolicyModel Editable() => new(
        "editable",
        AuthorizationResult.Deny,
        null,
        [new RoleMembershipAtom(["guest"])]);

    private sealed class FixedSource(PolicySet set) : IPolicySource
    {
        public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(set);
    }

    private sealed class StoreEditor : IPolicySetEditor
    {
        public void Replace(PolicySet set)
        {
        }

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
