using Mirepoix.AccessControl.Engine.Server.Http;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;
using Mirepoix.AccessControl.Protocol.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Mirepoix.AccessControl.Engine.Server.Http.Tests;

public sealed class EngineCheckEndpointTests
{
    [Fact]
    public async Task Check_ReturnsAllow_WhenLocalCheckerAllows()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IAccessChecker>(new FakeAllowChecker());
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlEngineServerHttp(options => options.AuthorizationPolicy = "Caller");
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
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
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlEngineServerHttp(options => options.AuthorizationPolicy = "Caller");
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
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

    [Fact]
    public void MapAccessControlEngine_ThrowsWhenAuthorizationPolicyMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IAccessChecker>(new FakeAllowChecker());
        builder.Services.AddAccessControlEngineServerHttp(_ => { });
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlEngine());

        Assert.Equal("Access-control HTTP routes require an authorization policy.", exception.Message);
    }

    [Fact]
    public async Task Check_WithoutCaller_DoesNotReturnDecision()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IAccessChecker>(new FakeAllowChecker());
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderTestHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("Caller", policy =>
        {
            policy.AddAuthenticationSchemes("Test");
            policy.RequireAuthenticatedUser();
        }));
        builder.Services.AddAccessControlEngineServerHttp(options => options.AuthorizationPolicy = "Caller");
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlEngine();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteCheckPath,
            new { subject = new { id = "alice" }, resource = new { type = "document", id = "1" }, operation = "document:read" },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("result", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

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

    private sealed class HeaderTestHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HeaderTestHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test")], "Test");
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "Test")));
        }
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
