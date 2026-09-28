using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Client.Http;
using Mirepoix.AccessControl.Providers.Server.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Mirepoix.AccessControl.Providers.Client.Http.Tests;

public sealed class ClientServerIntegrationTests
{
    [Fact]
    public async Task Client_RoundTrips_Subject_Through_Server()
    {
        await using var server = await StartServerAsync();
        using var http = server.GetTestClient();
        var resolver = new HttpSubjectResolver(http);

        var result = await resolver.HydrateAsync(
            new Subject("alice", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("alice", result.Id);
        Assert.Contains("editor", result.Roles);
        Assert.Equal("eng", result.Attributes["dept"]);
    }

    [Fact]
    public async Task Client_SubjectMiss_Throws_And_CompositeHydrator_Fails()
    {
        await using var server = await StartServerAsync();
        using var http = server.GetTestClient();
        var resolver = new HttpSubjectResolver(http);
        var hydrator = new CompositeBundleHydrator(subject: resolver);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await hydrator.HydrateAsync(
                new AuthorizationRequest(
                    new Subject("missing", new HashSet<string>(), new Dictionary<string, object?>()),
                    new Resource("document", "1", new Dictionary<string, object?>()),
                    Operation.Parse("document:read"),
                    new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>())),
                CancellationToken.None));
    }

    [Fact]
    public async Task Client_RoundTrips_Resource_Through_Server_Hydrator()
    {
        await using var server = await StartServerAsync();
        using var http = server.GetTestClient();
        var hydrator = new HttpResourceHydrator(http);

        var result = await hydrator.HydrateAsync(
            new Resource("document", "1", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("alice", result.Attributes["ownerId"]);
    }

    private static async Task<WebApplication> StartServerAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISubjectResolver>(
            new InMemorySubjectResolver(new Dictionary<string, Subject>
            {
                ["alice"] = new Subject(
                    "alice",
                    new HashSet<string> { "editor" },
                    new Dictionary<string, object?> { ["dept"] = "eng" }),
            }));
        builder.Services.AddSingleton<IResourceHydrator, TestResourceHydrator>();
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddSubject().AddResource();
            options.AuthorizationPolicy = "Caller";
        });
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlProviders();
        await app.StartAsync();
        return app;
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

    private sealed class TestResourceHydrator : IResourceHydrator
    {
        public Task<Resource> HydrateAsync(
            Resource partial,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new Resource(
                    partial.Type,
                    partial.Id,
                    new Dictionary<string, object?> { ["ownerId"] = "alice" }));
    }
}
