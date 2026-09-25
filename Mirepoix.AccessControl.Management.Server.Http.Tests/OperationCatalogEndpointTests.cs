using System.Net;
using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl.Authorization.AspNetCore;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Mirepoix.AccessControl.Management.Server.Http.Tests;

public sealed class OperationCatalogEndpointTests
{
    [Fact]
    public async Task Get_returns_billing_operations_without_replacing_policy()
    {
        await using var billing = await StartBillingAsync();
        var editor = new RecordingEditor();
        await using var admin = await StartAdminAsync(editor, billing, ("billing", BillingOrigin));

        using var client = admin.GetTestClient();
        var response = await client.GetAsync("/access-control/operations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var apps = document.RootElement.GetProperty("apps");
        Assert.Equal("billing", apps[0].GetProperty("name").GetString());
        Assert.Equal("invoice:post", apps[0].GetProperty("operations")[0].GetProperty("operation").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("failedApps").GetArrayLength());
        Assert.Equal(0, editor.Calls);
    }

    [Fact]
    public async Task Put_supported_operation_returns_204_and_records_set()
    {
        await using var billing = await StartBillingAsync();
        var editor = new RecordingEditor();
        await using var admin = await StartAdminAsync(editor, billing, ("billing", BillingOrigin));

        using var client = admin.GetTestClient();
        var response = await PutPolicyAsync(client, "invoice:post");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, editor.Calls);
        var atom = Assert.IsType<OperationMatchAtom>(Assert.Single(Assert.Single(editor.Last!.Policies).Atoms));
        Assert.Equal("invoice:post", atom.Pattern.Value);
    }

    [Fact]
    public async Task Put_returns_failed_apps_after_billing_snapshot_is_seeded()
    {
        await using var billing = await StartBillingAsync();
        var seedEditor = new RecordingEditor();
        await using var seed = await StartAdminAsync(seedEditor, billing, ("billing", BillingOrigin));
        using (var seedClient = seed.GetTestClient())
        {
            var seeded = await PutPolicyAsync(seedClient, "invoice:post");
            Assert.Equal(HttpStatusCode.NoContent, seeded.StatusCode);
        }

        var snapshot = seed.Services.GetRequiredService<OperationCatalogSnapshot>();
        var editor = new RecordingEditor();
        await using var admin = await StartAdminAsync(
            editor,
            billing,
            [("billing", BillingOrigin), ("invoices", DeadOrigin)],
            snapshot);

        using var client = admin.GetTestClient();
        var response = await PutPolicyAsync(client, "invoice:post");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("invoices", document.RootElement.GetProperty("failedApps")[0].GetString());
        Assert.Equal(1, editor.Calls);
    }

    [Fact]
    public async Task Put_returns_503_when_only_origin_is_dead_and_does_not_replace()
    {
        await using var billing = await StartBillingAsync();
        var editor = new RecordingEditor();
        await using var admin = await StartAdminAsync(editor, billing, ("invoices", DeadOrigin));

        using var client = admin.GetTestClient();
        var response = await PutPolicyAsync(client, "invoice:post");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, editor.Calls);
    }

    [Fact]
    public async Task Put_unsupported_operation_returns_400_and_does_not_replace()
    {
        await using var billing = await StartBillingAsync();
        var editor = new RecordingEditor();
        await using var admin = await StartAdminAsync(editor, billing, ("billing", BillingOrigin));

        using var client = admin.GetTestClient();
        var response = await PutPolicyAsync(client, "doc:read");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, editor.Calls);
    }

    private const string BillingOrigin = "http://billing.example";
    private const string DeadOrigin = "http://invoices.example";

    private static async Task<WebApplication> StartBillingAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAccessControlOperation("invoice:post");
        builder.Services.AddSingleton<PublishedOperationSource>();
        var app = builder.Build();
        app.MapAccessControlOperations();
        await app.StartAsync();
        return app;
    }

    private static async Task<WebApplication> StartAdminAsync(
        RecordingEditor editor,
        WebApplication billing,
        params (string Name, string Origin)[] apps)
    {
        return await StartAdminAsync(editor, billing, apps, snapshot: null);
    }

    private static async Task<WebApplication> StartAdminAsync(
        RecordingEditor editor,
        WebApplication billing,
        (string Name, string Origin)[] apps,
        OperationCatalogSnapshot? snapshot)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IPolicySetEditor>(editor);
        builder.Services.AddAccessControlManagementServerHttp(options =>
        {
            options.AddPolicySet();
            foreach (var app in apps)
                options.AddEnforcementApp(app.Name, app.Origin);
        });
        if (snapshot is not null)
            builder.Services.AddSingleton(snapshot);

        builder.Services.AddHttpClient(OperationCatalogServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new OriginRouter(billing.GetTestServer().CreateHandler()));

        var app = builder.Build();
        app.MapAccessControlManagement();
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> PutPolicyAsync(HttpClient client, string operation)
    {
        var set = new PolicySet(
            "v1",
            [new PolicyModel("p", AuthorizationResult.Allow, null, [new OperationMatchAtom(Operation.Parse(operation))])]);
        var json = PolicySerializers.ToJson(set);
        return client.PutAsync(
            "/access-control/policy-set",
            new StringContent(json, Encoding.UTF8, "application/json"));
    }

    private sealed class RecordingEditor : IPolicySetEditor
    {
        public int Calls { get; private set; }

        public PolicySet? Last { get; private set; }

        public void Replace(PolicySet set) => ReplaceAsync(set).GetAwaiter().GetResult();

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
        {
            Calls++;
            Last = set;
            return Task.CompletedTask;
        }
    }

    private sealed class OriginRouter(HttpMessageHandler billing) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (string.Equals(request.RequestUri?.Host, "billing.example", StringComparison.OrdinalIgnoreCase))
            {
                var invoker = new HttpMessageInvoker(billing, disposeHandler: false);
                return invoker.SendAsync(request, cancellationToken);
            }

            throw new HttpRequestException("No connection could be made.");
        }
    }
}
