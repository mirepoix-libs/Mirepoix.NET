using Mirepoix.AccessControl.Engine.Client.Http;
using Mirepoix.AccessControl.Engine.Server.Http;
using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

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

    [Fact]
    public async Task Client_RoundTrips_CompositeResourceKey_Through_Server_LocalChecker()
    {
        AuthorizationRequest? captured = null;
        await using var server = await StartServerAsync(new LocalAccessChecker(
            new MemoryPolicySource(new PolicySet("v1", new[]
            {
                new PolicyModel("p1", AuthorizationResult.Allow, "admins", new IAtom[]
                {
                    new RoleMembershipAtom(new[] { "ADMIN" }),
                }),
            })),
            new CapturingPassThroughHydrator(request => captured = request)));

        using var http = server.GetTestClient();
        var checker = new HttpAccessChecker(http);

        var expectedKey = ResourceKey.From(("tenantId", "acme"), ("documentId", "1"));
        var result = await checker.CheckAsync(CompositeSampleRequest(expectedKey), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, result.Result);
        Assert.Equal(DecisionStatus.Success, result.Status);
        Assert.NotNull(captured);
        Assert.Equal("document", captured!.Resource.Type);
        Assert.Equal(expectedKey, captured.Resource.Key);
    }

    private static async Task<WebApplication> StartServerAsync(IAccessChecker checker)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(checker);
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlEngineServerHttp(options => options.AuthorizationPolicy = "Caller");
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlEngine();
        await app.StartAsync();
        return app;
    }

    private static AuthorizationRequest SampleRequest() =>
        new(
            new Subject("u1", new HashSet<string> { "ADMIN" }, new Dictionary<string, object?>()),
            new Resource("doc", ResourceKey.Single("1"), new Dictionary<string, object?>()),
            Operation.Parse("doc:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

    private static AuthorizationRequest CompositeSampleRequest(ResourceKey key) =>
        new(
            new Subject("u1", new HashSet<string> { "ADMIN" }, new Dictionary<string, object?>()),
            new Resource("document", key, new Dictionary<string, object?>()),
            Operation.Parse("document:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

    private static void UseCallerPolicy(WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, AllowTestHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("Caller", policy =>
        {
            policy.AddAuthenticationSchemes("Test");
            policy.RequireAuthenticatedUser();
        }));
    }

    private sealed class AllowTestHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public AllowTestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
    }

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

    private sealed class CapturingPassThroughHydrator : IBundleHydrator
    {
        private readonly Action<AuthorizationRequest> _capture;

        public CapturingPassThroughHydrator(Action<AuthorizationRequest> capture) => _capture = capture;

        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            _capture(request);
            return Task.FromResult(new AuthorizationBundle(
                request.Subject,
                request.Resource,
                request.Operation,
                request.Context));
        }
    }

    private sealed class ThrowingHydrator : IBundleHydrator
    {
        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("hydration failed");
    }
}
