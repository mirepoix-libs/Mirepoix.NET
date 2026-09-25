using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Mirepoix.AccessControl.Authorization.AspNetCore;
using Mirepoix.AccessControl.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Authorization.AspNetCore.Tests;

public class PublishedOperationCatalogTests
{
    [Fact]
    public async Task Catalog_lists_route_registration_and_code_registration()
    {
        await using var host = await CatalogHost.StartAsync(
            services => services.AddAccessControlOperation("invoice:settle"),
            app =>
            {
                app.MapPost("/invoices/{id}", () => Results.Ok()).WithAccessOperation("invoice:post");
                app.MapAccessControlOperations();
                app.MapControllers();
            });

        var response = await host.Client.GetAsync(PublishedOperation.EnforcementPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var operations = await ReadOperationsAsync(response);
        var post = Assert.Single(operations, op => op.Operation == "invoice:post");
        Assert.Contains("invoices", post.RouteTemplate, StringComparison.Ordinal);
        Assert.Equal("POST", post.HttpMethod);
        var settle = Assert.Single(operations, op => op.Operation == "invoice:settle");
        Assert.Equal(string.Empty, settle.RouteTemplate);
        Assert.Equal(string.Empty, settle.HttpMethod);
        var edit = Assert.Single(operations, op =>
            op.Operation == "doc:edit"
            && op.RouteTemplate.Contains("/docs/", StringComparison.Ordinal));
        Assert.Contains("docs", edit.RouteTemplate, StringComparison.Ordinal);
        Assert.Equal("GET", edit.HttpMethod);
    }

    [Fact]
    public void AddAccessControlOperation_rejects_wildcard_segment()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<ArgumentException>(() =>
            services.AddAccessControlOperation("invoice:*"));

        Assert.Equal("operation", ex.ParamName);
    }

    [Fact]
    public async Task CheckAsync_rejects_unpublished_operation_and_allows_published()
    {
        await using var host = await CatalogHost.StartAsync(
            services => services.AddAccessControlOperation("invoice:settle"),
            app =>
            {
                app.MapPost("/invoices/{id}", () => Results.Ok()).WithAccessOperation("invoice:post");
                app.MapControllers();
            });

        var checker = host.App.Services.GetRequiredService<IAccessChecker>();

        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            checker.CheckAsync(RequestFor("invoice:missing"), CancellationToken.None));
        Assert.Contains("invoice:missing", missing.Message, StringComparison.Ordinal);

        var decision = await checker.CheckAsync(RequestFor("invoice:settle"), CancellationToken.None);
        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
    }

    [Fact]
    public async Task Catalog_get_throws_when_endpoint_operation_has_wildcard_segment()
    {
        await using var host = await CatalogHost.StartAsync(
            map: app =>
            {
                app.MapGet("/bad", () => Results.Ok()).WithAccessOperation("bad:*");
                app.MapAccessControlOperations();
            });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.Client.GetAsync(PublishedOperation.EnforcementPath));

        Assert.Contains("bad:*", ex.Message, StringComparison.Ordinal);
    }

    private static AuthorizationRequest RequestFor(string operation)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "u1"),
                    new Claim(ClaimTypes.Role, "EDITOR")
                },
                authenticationType: "test"))
        };
        var seed = new DefaultClaimsPrincipalMapper().CreateSeed(http);
        return new AuthorizationRequest(
            seed.Subject,
            new Resource(string.Empty, string.Empty, new Dictionary<string, object?>()),
            Operation.Parse(operation),
            seed.Context);
    }

    private static async Task<List<PublishedOperation>> ReadOperationsAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        var operations = JsonSerializer.Deserialize<List<PublishedOperation>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(operations);
        return operations;
    }

    private sealed class CatalogHost : IAsyncDisposable
    {
        public required WebApplication App { get; init; }

        public HttpClient Client => App.GetTestClient();

        public static async Task<CatalogHost> StartAsync(
            Action<IServiceCollection>? configure = null,
            Action<WebApplication>? map = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(PublishedOperationCatalogTests).Assembly.FullName
            });
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(CatalogDocsController).Assembly);

            var policySet = new PolicySet(
                "v1",
                new[]
                {
                    new PolicyModel(
                        "allow-all",
                        AuthorizationResult.Allow,
                        "any operation",
                        new IAtom[]
                        {
                            new OperationMatchAtom(Operation.Parse("*"))
                        })
                });

            configure?.Invoke(builder.Services);
            builder.Services.AddAccessControl(ac => ac.UseMemoryPolicySet(policySet));

            var app = builder.Build();
            map?.Invoke(app);
            await app.StartAsync();
            return new CatalogHost { App = app };
        }

        public async ValueTask DisposeAsync()
        {
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }
}

[ApiController]
public sealed class CatalogDocsController : ControllerBase
{
    [HttpGet("/docs/{id}")]
    [AccessOperation("doc:edit")]
    public IActionResult Get(string id) => Ok(id);
}
