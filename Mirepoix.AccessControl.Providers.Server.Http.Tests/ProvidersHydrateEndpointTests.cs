using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Protocol.Http;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Server.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddSubject();
            options.AuthorizationPolicy = "Caller";
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
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
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddSubject();
            options.AuthorizationPolicy = "Caller";
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath,
            new { id = "missing", roles = Array.Empty<string>(), attributes = new { } },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("missing", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubjectHydrate_StoreFailure_DoesNotReturnExceptionText()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISubjectResolver>(new ThrowingResolver());
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddSubject();
            options.AuthorizationPolicy = "Caller";
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath,
            new { id = "alice", roles = Array.Empty<string>(), attributes = new { } },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Hydration failed.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("sql.internal", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubjectHydrate_MalformedJson_DoesNotReturnParserText()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISubjectResolver>(
            new InMemorySubjectResolver(new Dictionary<string, Subject>()));
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddSubject();
            options.AuthorizationPolicy = "Caller";
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsync(
            AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath,
            new StringContent("{", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("The hydrate request is invalid.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Path", body, StringComparison.Ordinal);
    }

    [Fact]
    public void MapAccessControlProviders_Throws_WhenNoSlicesEnabled()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlProvidersServerHttp(options => options.AuthorizationPolicy = "Caller");
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Contains("slice", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapAccessControlProviders_Throws_WhenSubjectEnabledButResolverMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddSubject();
            options.AuthorizationPolicy = "Caller";
        });
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Contains("ISubjectResolver", exception.Message);
    }

    [Fact]
    public void MapAccessControlProviders_Throws_WhenResourceEnabledButHydratorMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddResource();
            options.AuthorizationPolicy = "Caller";
        });
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Contains("IResourceHydrator", exception.Message);
    }

    [Fact]
    public void MapAccessControlProviders_ThrowsWhenAuthorizationPolicyMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<ISubjectResolver>(new InMemorySubjectResolver(new Dictionary<string, Subject>()));
        builder.Services.AddAccessControlProvidersServerHttp(options => options.AddSubject());
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Equal("Access-control HTTP routes require an authorization policy.", exception.Message);
    }

    [Fact]
    public async Task SubjectHydrate_WithoutCaller_DoesNotReturnAttributes()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ISubjectResolver>(
            new InMemorySubjectResolver(new Dictionary<string, Subject>
            {
                ["alice"] = new Subject(
                    "alice",
                    new HashSet<string> { "editor" },
                    new Dictionary<string, object?>()),
            }));
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderTestHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("Caller", policy =>
        {
            policy.AddAuthenticationSchemes("Test");
            policy.RequireAuthenticatedUser();
        }));
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddSubject();
            options.AuthorizationPolicy = "Caller";
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            AccessControlHttpRoutes.AbsoluteProvidersSubjectHydratePath,
            new { id = "alice", roles = Array.Empty<string>(), attributes = new { } },
            AccessControlHttpJson.DefaultOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("editor", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AttributeCatalog_ReturnsCamelCaseSubjectAndResourceRows()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPublishedProviderAttributeSource>(new FixedAttributeSource());
        UseCallerPolicy(builder);
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddAttributeCatalog();
            options.AuthorizationPolicy = "Caller";
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync(PublishedAttribute.CatalogPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """[{"target":"subject","type":"subject","key":"dept"},{"target":"resource","type":"document","key":"ownerId"}]""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AttributeCatalog_StaysOnCatalogPath_WhenRoutePrefixChanges()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPublishedProviderAttributeSource>(new FixedAttributeSource());
        builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderTestHandler>("Test", _ => { });
        builder.Services.AddAuthorization(options => options.AddPolicy("Caller", policy =>
        {
            policy.AddAuthenticationSchemes("Test");
            policy.RequireAuthenticatedUser();
        }));
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.RoutePrefix = "/pip";
            options.AddAttributeCatalog();
            options.AuthorizationPolicy = "Caller";
        });
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapAccessControlProviders();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var anonymous = await client.GetAsync(PublishedAttribute.CatalogPath);
        using var authorized = new HttpRequestMessage(HttpMethod.Get, PublishedAttribute.CatalogPath);
        authorized.Headers.TryAddWithoutValidation("Authorization", "Bearer test");
        var catalog = await client.SendAsync(authorized);
        var underPrefix = await client.GetAsync("/pip/attributes");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, underPrefix.StatusCode);
    }

    [Fact]
    public void MapAccessControlProviders_Throws_WhenAttributeCatalogEnabledButSourceMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAccessControlProvidersServerHttp(options =>
        {
            options.AddAttributeCatalog();
            options.AuthorizationPolicy = "Caller";
        });
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Contains("IPublishedProviderAttributeSource", exception.Message);
    }

    [Fact]
    public void MapAccessControlProviders_Throws_WhenAttributeCatalogMissingAuthorizationPolicy()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IPublishedProviderAttributeSource>(new FixedAttributeSource());
        builder.Services.AddAccessControlProvidersServerHttp(options => options.AddAttributeCatalog());
        using var app = builder.Build();

        var exception = Assert.Throws<InvalidOperationException>(() => app.MapAccessControlProviders());

        Assert.Equal("Access-control HTTP routes require an authorization policy.", exception.Message);
    }

    private sealed class FixedAttributeSource : IPublishedProviderAttributeSource
    {
        public IReadOnlyList<PublishedAttribute> List() =>
        [
            new(AttributeTarget.Subject, "subject", "dept"),
            new(AttributeTarget.Resource, "document", "ownerId"),
        ];
    }

    private sealed class ThrowingResolver : ISubjectResolver
    {
        public Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Login failed for user 'sa' on server 'sql.internal'.");
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
}
