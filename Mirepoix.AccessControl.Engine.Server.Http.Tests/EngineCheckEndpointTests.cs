using Mirepoix.AccessControl.Engine.Server.Http;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;
using Mirepoix.AccessControl.Protocol.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace Mirepoix.AccessControl.Engine.Server.Http.Tests;

public sealed class EngineCheckEndpointTests
{
    [Fact]
    public async Task Check_ReturnsAllow_WhenLocalCheckerAllows()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IAccessChecker>(new FakeAllowChecker());
        builder.Services.AddAccessControlEngineServerHttp(_ => { });
        await using var app = builder.Build();
        app.MapAccessControlEngine();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteCheckPath,
            new
            {
                subject = new { id = "alice", roles = Array.Empty<string>(), attributes = new { } },
                resource = new { type = "document", id = "1", attributes = new { } },
                operation = "document:read",
                context = new { time = (string?)null, claims = new { }, values = new { } },
            },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<AccessDecisionDto>(AccessControlHttpJson.DefaultOptions);
        Assert.Equal(AuthorizationResult.Allow, decision!.ToDomain().Result);
    }

    [Fact]
    public async Task Check_ReturnsHydrationFailedStatus_InBody()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IAccessChecker>(new LocalAccessChecker(
            new MemoryPolicySource(new PolicySet("v1", Array.Empty<PolicyModel>())),
            new ThrowingHydrator()));
        builder.Services.AddAccessControlEngineServerHttp(_ => { });
        await using var app = builder.Build();
        app.MapAccessControlEngine();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteCheckPath,
            new
            {
                subject = new { id = "alice", roles = Array.Empty<string>(), attributes = new { } },
                resource = new { type = "document", id = "1", attributes = new { } },
                operation = "document:read",
                context = new { time = (string?)null, claims = new { }, values = new { } },
            },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decision = await response.Content.ReadFromJsonAsync<AccessDecisionDto>(AccessControlHttpJson.DefaultOptions);
        Assert.Equal(DecisionStatus.HydrationFailed, decision!.ToDomain().Status);
        Assert.Equal(AuthorizationResult.Deny, decision.ToDomain().Result);
        Assert.Equal("v1", decision.ToDomain().PolicySetVersion);
    }

    [Fact]
    public void MapAccessControlEngine_ThrowsWhenIAccessCheckerMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlEngineServerHttp(_ => { });
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlEngine());

        Assert.Contains("IAccessChecker", exception.Message);
    }

    private sealed class FakeAllowChecker : IAccessChecker
    {
        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AccessDecision(
                AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1"));
    }

    private sealed class ThrowingHydrator : IBundleHydrator
    {
        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("hydration failed");
    }
}
