using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Engine.Client.Http.Tests;

public sealed class RedirectTests
{
    [Fact]
    public async Task RegisteredClient_DoesNotFollowRedirect()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.MapPost("/access-control/check", () => Results.Redirect("/other", permanent: false, preserveMethod: true));
        app.MapPost("/other", () => Results.Text("{}", "application/json", Encoding.UTF8, (int)HttpStatusCode.OK));
        await app.StartAsync();
        var address = app.Urls.Single();

        var services = new ServiceCollection();
        services.AddAccessControlEngineClientHttp(options => options.BaseAddress = new Uri(address));
        using var provider = services.BuildServiceProvider();
        var checker = provider.GetRequiredService<IAccessChecker>();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            checker.CheckAsync(Sample(), CancellationToken.None));
    }

    private static AuthorizationRequest Sample() =>
        new(
            new Subject("alice", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("document", ResourceKey.Single("1"), new Dictionary<string, object?>()),
            Operation.Parse("document:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
}
