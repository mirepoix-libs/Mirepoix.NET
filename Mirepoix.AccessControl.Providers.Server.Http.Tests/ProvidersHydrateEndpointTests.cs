using System.Net;
using System.Net.Http.Json;
using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Server.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Providers.Server.Http.Tests;

public sealed class ProvidersHydrateEndpointTests
{
    [Fact]
    public async Task SubjectHydrate_ReturnsHydratedSubject()
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
        builder.Services.AddAccessControlProvidersServerHttp(options => options.AddSubject());
        await using var app = builder.Build();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath,
            new { id = "alice", roles = Array.Empty<string>(), attributes = new { } },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SubjectDto>(AccessControlHttpJson.DefaultOptions);
        var subject = dto!.ToDomain();
        Assert.Equal("alice", subject.Id);
        Assert.Contains("editor", subject.Roles);
        Assert.Equal("eng", subject.Attributes["dept"]);
    }

    [Fact]
    public async Task SubjectHydrate_Returns404_WhenSubjectMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISubjectResolver>(
            new InMemorySubjectResolver(new Dictionary<string, Subject>()));
        builder.Services.AddAccessControlProvidersServerHttp(options => options.AddSubject());
        await using var app = builder.Build();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath,
            new { id = "missing", roles = Array.Empty<string>(), attributes = new { } },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void MapAccessControlProviders_Throws_WhenNoSlicesEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlProvidersServerHttp(_ => { });
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Contains("slice", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapAccessControlProviders_Throws_WhenSubjectEnabledButResolverMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlProvidersServerHttp(options => options.AddSubject());
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Contains("ISubjectResolver", exception.Message);
    }
}
