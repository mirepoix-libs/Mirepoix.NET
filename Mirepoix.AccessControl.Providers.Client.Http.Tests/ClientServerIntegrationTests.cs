using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Client.Http;
using Mirepoix.AccessControl.Providers.Server.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

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
        builder.Services.AddAccessControlProvidersServerHttp(options =>
            options.AddSubject().AddResource());
        var app = builder.Build();
        app.MapAccessControlProviders();
        await app.StartAsync();
        return app;
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
