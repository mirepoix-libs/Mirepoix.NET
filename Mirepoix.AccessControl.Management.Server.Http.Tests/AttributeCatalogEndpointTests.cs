using System.Net;
using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management.Server.Http.Tests;

public sealed class AttributeCatalogEndpointTests
{
    [Fact]
    public void Duplicate_name_in_the_same_list_throws()
    {
        var options = new AccessControlManagementServerHttpOptions();
        options.AddEnforcementApp("billing", BillingOrigin);
        Assert.Throws<ArgumentException>(() => options.AddEnforcementApp("billing", "http://other.example"));
        Assert.Throws<ArgumentException>(() => options.AddLocalEnforcementApp("billing"));

        options.AddProviderApp("identity", IdentityOrigin);
        Assert.Throws<ArgumentException>(() => options.AddLocalProviderApp("identity"));
        options.AddLocalProviderApp();
        Assert.Throws<ArgumentException>(() => options.AddLocalProviderApp("local"));
    }

    [Fact]
    public void Same_name_on_provider_and_enforcement_lists_is_allowed()
    {
        var options = new AccessControlManagementServerHttpOptions();
        options.AddLocalEnforcementApp();
        options.AddLocalProviderApp();
        options.AddEnforcementApp("identity", IdentityOrigin);
        options.AddProviderApp("identity", IdentityOrigin);
    }

    [Fact]
    public async Task Get_returns_provider_and_enforcement_apps_and_applies_snapshot()
    {
        await using var identity = await StartAttributesHostAsync(
            [new PublishedAttribute(AttributeTarget.Subject, "subject", "dept")]);
        await using var billing = await StartAttributesHostAsync(
            [new PublishedAttribute(AttributeTarget.Context, null, "time")]);
        await using var admin = await StartRemoteAdminAsync(identity, billing);

        using var client = ManagementTestCaller.Client(admin);
        var response = await client.GetAsync("/access-control/attributes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var provider = document.RootElement.GetProperty("providerApps")[0];
        var enforcement = document.RootElement.GetProperty("enforcementApps")[0];
        Assert.Equal("identity", provider.GetProperty("name").GetString());
        Assert.Equal("subject", provider.GetProperty("attributes")[0].GetProperty("target").GetString());
        Assert.Equal("subject", provider.GetProperty("attributes")[0].GetProperty("type").GetString());
        Assert.Equal("dept", provider.GetProperty("attributes")[0].GetProperty("key").GetString());
        Assert.Equal("billing", enforcement.GetProperty("name").GetString());
        Assert.Equal("context", enforcement.GetProperty("attributes")[0].GetProperty("target").GetString());
        Assert.Equal("time", enforcement.GetProperty("attributes")[0].GetProperty("key").GetString());
        Assert.False(enforcement.GetProperty("attributes")[0].TryGetProperty("type", out _));
        Assert.Equal(0, document.RootElement.GetProperty("failedApps").GetArrayLength());

        var snapshot = admin.Services.GetRequiredService<AttributeCatalogSnapshot>();
        Assert.True(snapshot.HasSnapshot);
        Assert.Contains(snapshot.Attributes, row => row.Key == "dept" && row.Target == AttributeTarget.Subject);
        Assert.Contains(snapshot.Attributes, row => row.Key == "time" && row.Target == AttributeTarget.Context);
    }

    [Fact]
    public async Task Get_does_not_apply_or_fill_failed_app_from_previous_snapshot()
    {
        await using var identity = await StartAttributesHostAsync(
            [new PublishedAttribute(AttributeTarget.Subject, "subject", "dept")]);
        await using var billing = await StartAttributesHostAsync(
            [new PublishedAttribute(AttributeTarget.Context, null, "time")]);
        var gate = new FailGate();
        await using var admin = await StartRemoteAdminAsync(identity, billing, gate);

        using var client = ManagementTestCaller.Client(admin);
        var seeded = await client.GetAsync("/access-control/attributes");
        Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);

        gate.Identity = true;
        var response = await client.GetAsync("/access-control/attributes");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var snapshot = admin.Services.GetRequiredService<AttributeCatalogSnapshot>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, document.RootElement.GetProperty("providerApps").GetArrayLength());
        Assert.Equal("billing", document.RootElement.GetProperty("enforcementApps")[0].GetProperty("name").GetString());
        Assert.Equal("identity", document.RootElement.GetProperty("failedApps")[0].GetString());
        Assert.True(snapshot.HasSnapshot);
        Assert.Empty(snapshot.LastPull!.FailedApps);
        Assert.Contains(snapshot.Attributes, row => row.Key == "dept");
    }

    [Fact]
    public async Task Get_includes_local_apps_without_http()
    {
        var operationCalls = new CountingHandler();
        var attributeCalls = new CountingHandler();
        var builder = AdminBuilder(new RecordingEditor());
        builder.Services.AddSingleton<IPublishedProviderAttributeSource>(new FixedProviderAttributes(
            [new PublishedAttribute(AttributeTarget.Subject, "subject", "dept")]));
        builder.Services.AddSingleton<IPublishedEnforcementAttributeSource>(new FixedEnforcementAttributes(
            [new PublishedAttribute(AttributeTarget.Context, null, "time")]));
        builder.Services.AddAccessControlManagementServerHttp(options =>
        {
            options.AddLocalProviderApp();
            options.AddLocalEnforcementApp();
        });
        builder.Services.AddHttpClient(OperationCatalogServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => operationCalls);
        builder.Services.AddHttpClient(AttributeCatalogServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => attributeCalls);

        await using var admin = builder.Build();
        ManagementTestCaller.Use(admin);
        admin.MapAccessControlManagement();
        await admin.StartAsync();

        using var client = ManagementTestCaller.Client(admin);
        var response = await client.GetAsync("/access-control/attributes");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("local", document.RootElement.GetProperty("providerApps")[0].GetProperty("name").GetString());
        Assert.Equal("dept", document.RootElement.GetProperty("providerApps")[0].GetProperty("attributes")[0].GetProperty("key").GetString());
        Assert.Equal("local", document.RootElement.GetProperty("enforcementApps")[0].GetProperty("name").GetString());
        Assert.Equal("time", document.RootElement.GetProperty("enforcementApps")[0].GetProperty("attributes")[0].GetProperty("key").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("failedApps").GetArrayLength());
        Assert.Equal(0, operationCalls.Calls);
        Assert.Equal(0, attributeCalls.Calls);
    }

    [Fact]
    public async Task Anonymous_attributes_get_is_401()
    {
        var checker = new ScriptedChecker(AuthorizationResult.Allow);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        ManagementTestCaller.Register(builder.Services, checker);
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddLocalProviderApp());
        await using var admin = builder.Build();
        ManagementTestCaller.Use(admin);
        admin.MapAccessControlManagement();
        await admin.StartAsync();

        using var client = admin.GetTestClient();
        var response = await client.GetAsync("/access-control/attributes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, checker.Calls);
    }

    [Fact]
    public async Task Denied_attributes_get_checks_attributes_read_on_empty_resource()
    {
        var checker = new RecordingChecker();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        ManagementTestCaller.Register(builder.Services, checker);
        builder.Services.AddAccessControlManagementServerHttp(options => options.AddLocalProviderApp());
        await using var admin = builder.Build();
        ManagementTestCaller.Use(admin);
        admin.MapAccessControlManagement();
        await admin.StartAsync();

        using var client = admin.GetTestClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", ManagementTestCaller.HeaderValue);
        var response = await client.GetAsync("/access-control/attributes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("access-control:attributes:read", checker.Operation);
        Assert.Equal(string.Empty, checker.ResourceType);
        Assert.Equal(string.Empty, checker.ResourceId);
    }

    [Fact]
    public async Task Put_returns_503_when_attribute_snapshot_is_missing()
    {
        var editor = new RecordingEditor();
        var builder = AdminBuilder(editor);
        builder.Services.AddSingleton<IPolicySource, UnusedPolicySource>();
        builder.Services.AddAccessControlManagementServerHttp(options =>
        {
            options.AddPolicySet();
            options.AddProviderApp("identity", IdentityOrigin);
        });
        builder.Services.AddHttpClient(AttributeCatalogServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new CountingHandler());

        await using var admin = builder.Build();
        ManagementTestCaller.Use(admin);
        admin.MapAccessControlManagement();
        await admin.StartAsync();

        using var client = ManagementTestCaller.Client(admin);
        var response = await PutAsync(
            client,
            new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "eng"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, editor.Calls);
    }

    [Fact]
    public async Task Put_merges_failed_apps_from_operation_and_attribute_pulls()
    {
        await using var billing = await StartAttributesHostAsync(
            [new PublishedAttribute(AttributeTarget.Context, null, "time")],
            operationsFail: true);
        var editor = new RecordingEditor();
        var builder = AdminBuilder(editor);
        builder.Services.AddSingleton<IPolicySource, UnusedPolicySource>();
        builder.Services.AddAccessControlManagementServerHttp(options =>
        {
            options.AddPolicySet();
            options.AddEnforcementApp("billing", BillingOrigin);
            options.AddProviderApp("identity", IdentityOrigin);
        });
        var operationSnapshot = new OperationCatalogSnapshot();
        operationSnapshot.Apply(new OperationCatalogPull(
            [new EnforcementAppCatalog("billing", [new PublishedOperation("invoice:post", "/invoices", "POST")])],
            []));
        var attributeSnapshot = new AttributeCatalogSnapshot();
        attributeSnapshot.Apply(new AttributeCatalogPull(
            [new AttributeAppCatalog("identity", [new PublishedAttribute(AttributeTarget.Subject, "subject", "dept")])],
            [new AttributeAppCatalog("billing", [new PublishedAttribute(AttributeTarget.Context, null, "time")])],
            []));
        builder.Services.AddSingleton(operationSnapshot);
        builder.Services.AddSingleton(attributeSnapshot);
        RouteCatalogClients(builder.Services, billing.GetTestServer().CreateHandler(), identity: null, new FailGate());

        await using var admin = builder.Build();
        ManagementTestCaller.Use(admin);
        admin.MapAccessControlManagement();
        await admin.StartAsync();

        using var client = ManagementTestCaller.Client(admin);
        var response = await PutAsync(
            client,
            new OperationMatchAtom(Operation.Parse("invoice:post")),
            new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "eng"));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var names = document.RootElement.GetProperty("failedApps").EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, names.Length);
        Assert.Contains("billing", names);
        Assert.Contains("identity", names);
        Assert.Equal(1, editor.Calls);
    }

    private const string BillingOrigin = "http://billing.example";
    private const string IdentityOrigin = "http://identity.example";

    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static WebApplicationBuilder AdminBuilder(RecordingEditor editor)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPolicySetEditor>(editor);
        ManagementTestCaller.Register(builder.Services, new ScriptedChecker(AuthorizationResult.Allow));
        return builder;
    }

    private static async Task<WebApplication> StartRemoteAdminAsync(
        WebApplication identity,
        WebApplication billing,
        FailGate? gate = null)
    {
        gate ??= new FailGate();
        var builder = AdminBuilder(new RecordingEditor());
        builder.Services.AddAccessControlManagementServerHttp(options =>
        {
            options.AddProviderApp("identity", IdentityOrigin);
            options.AddEnforcementApp("billing", BillingOrigin);
        });
        RouteCatalogClients(
            builder.Services,
            billing.GetTestServer().CreateHandler(),
            identity.GetTestServer().CreateHandler(),
            gate);
        var admin = builder.Build();
        ManagementTestCaller.Use(admin);
        admin.MapAccessControlManagement();
        await admin.StartAsync();
        return admin;
    }

    private static void RouteCatalogClients(
        IServiceCollection services,
        HttpMessageHandler billing,
        HttpMessageHandler? identity,
        FailGate gate)
    {
        services.AddHttpClient(OperationCatalogServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HostRouter(billing, identity, gate));
        services.AddHttpClient(AttributeCatalogServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new HostRouter(billing, identity, gate));
    }

    private static async Task<WebApplication> StartAttributesHostAsync(
        PublishedAttribute[] rows,
        bool operationsFail = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapGet("/access-control/attributes", () => Results.Json(rows, CamelCase));
        if (operationsFail)
        {
            app.MapGet("/access-control/operations", () => Results.StatusCode(StatusCodes.Status500InternalServerError));
        }

        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> PutAsync(HttpClient client, params IAtom[] atoms)
    {
        var set = new PolicySet("v1", [new PolicyModel("p", AuthorizationResult.Allow, null, atoms)]);
        var json = PolicySerializers.ToJson(set);
        return client.PutAsync(
            "/access-control/policy-set",
            new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private sealed class UnusedPolicySource : IPolicySource
    {
        public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Catalog tests do not read the policy set.");
    }

    private sealed class RecordingEditor : IPolicySetEditor
    {
        public int Calls { get; private set; }

        public void Replace(PolicySet set) => ReplaceAsync(set).GetAwaiter().GetResult();

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingChecker : IAccessChecker
    {
        public string? Operation { get; private set; }

        public string? ResourceType { get; private set; }

        public string? ResourceId { get; private set; }

        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken)
        {
            Operation = request.Operation.Value;
            ResourceType = request.Resource.Type;
            ResourceId = request.Resource.Id;
            return Task.FromResult(new AccessDecision(
                AuthorizationResult.Deny,
                [],
                DecisionStatus.Defaulted,
                "v"));
        }
    }

    private sealed class FixedProviderAttributes(IReadOnlyList<PublishedAttribute> rows) : IPublishedProviderAttributeSource
    {
        public IReadOnlyList<PublishedAttribute> List() => rows;
    }

    private sealed class FixedEnforcementAttributes(IReadOnlyList<PublishedAttribute> rows) : IPublishedEnforcementAttributeSource
    {
        public IReadOnlyList<PublishedAttribute> List() => rows;
    }

    private sealed class FailGate
    {
        public bool Identity { get; set; }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            throw new HttpRequestException("No connection could be made.");
        }
    }

    private sealed class HostRouter(HttpMessageHandler billing, HttpMessageHandler? identity, FailGate gate) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var host = request.RequestUri?.Host;
            if (string.Equals(host, "billing.example", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpMessageInvoker(billing, disposeHandler: false).SendAsync(request, cancellationToken);
            }

            if (string.Equals(host, "identity.example", StringComparison.OrdinalIgnoreCase))
            {
                if (gate.Identity || identity is null)
                    throw new HttpRequestException("No connection could be made.");

                return new HttpMessageInvoker(identity, disposeHandler: false).SendAsync(request, cancellationToken);
            }

            throw new HttpRequestException("No connection could be made.");
        }
    }
}
