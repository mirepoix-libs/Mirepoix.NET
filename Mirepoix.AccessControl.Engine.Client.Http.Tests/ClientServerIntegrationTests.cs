using Mirepoix.AccessControl.Engine.Client.Http;
using Mirepoix.AccessControl.Engine.Server.Http;
using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Engine.Client.Http.Tests;

public sealed class ClientServerIntegrationTests
{
    [Fact]
    public async Task Client_RoundTrips_Through_Server_LocalChecker()
    {
        await using var server = await StartServerAsync(new LocalAccessChecker(
            new MemoryPolicySource(new PolicySet("v1", new[]
            {
                new PolicyModel("p1", AuthorizationResult.Allow, "admins", new IAtom[]
                {
                    new RoleMembershipAtom(new[] { "ADMIN" }),
                }),
            })),
            new PassThroughHydrator()));

        using var http = server.GetTestClient();
        var checker = new HttpAccessChecker(http);

        var result = await checker.CheckAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, result.Result);
        Assert.Equal(DecisionStatus.Success, result.Status);
        Assert.Equal("v1", result.PolicySetVersion);
    }

    [Fact]
    public async Task Client_RoundTrips_HydrationFailed_Status()
    {
        await using var server = await StartServerAsync(new LocalAccessChecker(
            new MemoryPolicySource(new PolicySet("v1", Array.Empty<PolicyModel>())),
            new ThrowingHydrator()));

        using var http = server.GetTestClient();
        var checker = new HttpAccessChecker(http);

        var result = await checker.CheckAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, result.Result);
        Assert.Equal(DecisionStatus.HydrationFailed, result.Status);
        Assert.Equal("v1", result.PolicySetVersion);
    }

    private static async Task<WebApplication> StartServerAsync(IAccessChecker checker)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(checker);
        builder.Services.AddAccessControlEngineServerHttp(_ => { });
        var app = builder.Build();
        app.MapAccessControlEngine();
        await app.StartAsync();
        return app;
    }

    private static AuthorizationRequest SampleRequest() =>
        new(
            new Subject("u1", new HashSet<string> { "ADMIN" }, new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

    private sealed class PassThroughHydrator : IBundleHydrator
    {
        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AuthorizationBundle(
                request.Subject,
                request.Resource,
                request.Operation,
                request.Context));
    }

    private sealed class ThrowingHydrator : IBundleHydrator
    {
        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("hydration failed");
    }
}
