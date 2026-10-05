using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class AttributeCatalogClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task PullAsync_gets_provider_and_enforcement_attributes_and_records_failures()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://identity.example/access-control/attributes")
            {
                var json = JsonSerializer.Serialize(
                    new[]
                    {
                        new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
                        new PublishedAttribute(AttributeTarget.Context, null, "time"),
                    },
                    JsonOptions);
                return Json(json);
            }

            if (url == "https://billing.example/access-control/attributes")
            {
                var json = JsonSerializer.Serialize(
                    new[]
                    {
                        new PublishedAttribute(AttributeTarget.Context, null, "time"),
                        new PublishedAttribute(AttributeTarget.Resource, "invoice", "status"),
                    },
                    JsonOptions);
                return Json(json);
            }

            if (url == "https://invoices.example/access-control/attributes")
                throw new HttpRequestException("refused");

            throw new InvalidOperationException(url);
        });

        var options = new AttributeCatalogOptions();
        options.AddProviderApp("identity", "https://identity.example/");
        options.AddProviderApp("invoices", "https://invoices.example");
        options.AddEnforcementApp("billing", "https://billing.example");
        var client = new AttributeCatalogClient(new HttpClient(handler), options);

        var pull = await client.PullAsync(CancellationToken.None);

        var identity = Assert.Single(pull.ProviderApps);
        Assert.Equal("identity", identity.Name);
        var dept = Assert.Single(identity.Attributes);
        Assert.Equal(AttributeTarget.Subject, dept.Target);
        Assert.Equal("subject", dept.Type);
        Assert.Equal("dept", dept.Key);
        Assert.Equal(["invoices"], pull.FailedApps);
        var billing = Assert.Single(pull.EnforcementApps);
        Assert.Equal("billing", billing.Name);
        var time = Assert.Single(billing.Attributes);
        Assert.Equal(AttributeTarget.Context, time.Target);
        Assert.Null(time.Type);
        Assert.Equal("time", time.Key);
    }

    [Fact]
    public async Task PullAsync_records_non_success_null_invalid_json_and_continues()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://down.example/access-control/attributes")
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            if (url == "https://null.example/access-control/attributes")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", Encoding.UTF8, "application/json"),
                };

            if (url == "https://bad.example/access-control/attributes")
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{", Encoding.UTF8, "application/json"),
                };

            if (url == "https://billing.example/access-control/attributes")
                return Json(JsonSerializer.Serialize(
                    new[] { new PublishedAttribute(AttributeTarget.Context, null, "time") },
                    JsonOptions));

            throw new InvalidOperationException(url);
        });

        var options = new AttributeCatalogOptions();
        options.AddProviderApp("down", "https://down.example");
        options.AddProviderApp("nulljson", "https://null.example");
        options.AddEnforcementApp("bad", "https://bad.example");
        options.AddEnforcementApp("billing", "https://billing.example");
        var client = new AttributeCatalogClient(new HttpClient(handler), options);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.Empty(pull.ProviderApps);
        Assert.Equal("billing", Assert.Single(pull.EnforcementApps).Name);
        Assert.Equal(["down", "nulljson", "bad"], pull.FailedApps);
    }

    [Fact]
    public async Task PullAsync_reads_local_sources_without_http_and_keeps_remote_peers()
    {
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == "https://identity.example/access-control/attributes")
            {
                return Json(JsonSerializer.Serialize(
                    new[] { new PublishedAttribute(AttributeTarget.Resource, "invoice", "status") },
                    JsonOptions));
            }

            throw new InvalidOperationException(request.RequestUri.AbsoluteUri);
        });

        var services = new ServiceCollection();
        services.AddSingleton<IPublishedProviderAttributeSource>(new FixedProvider(
        [
            new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
            new PublishedAttribute(AttributeTarget.Context, null, "time"),
        ]));
        services.AddSingleton<IPublishedEnforcementAttributeSource>(new FixedEnforcement(
        [
            new PublishedAttribute(AttributeTarget.Context, "ignored", "tenant"),
            new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
        ]));
        using var provider = services.BuildServiceProvider();

        var options = new AttributeCatalogOptions();
        options.AddLocalProviderApp();
        options.AddProviderApp("identity", "https://identity.example");
        options.AddLocalEnforcementApp("billing");
        var client = new AttributeCatalogClient(new HttpClient(handler), options, provider);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.Equal(["local", "identity"], pull.ProviderApps.Select(app => app.Name).ToArray());
        var local = pull.ProviderApps[0].Attributes;
        Assert.Equal(AttributeTarget.Subject, Assert.Single(local).Target);
        Assert.Equal("dept", local[0].Key);
        var billing = Assert.Single(pull.EnforcementApps);
        Assert.Equal("billing", billing.Name);
        Assert.Equal("tenant", Assert.Single(billing.Attributes).Key);
        Assert.Null(billing.Attributes[0].Type);
        Assert.Empty(pull.FailedApps);
    }

    [Fact]
    public async Task PullAsync_records_timeout_as_failure_and_propagates_caller_cancellation_on_remote()
    {
        var handler = new StubHandler((request, cancellationToken) =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://slow.example/access-control/attributes")
                throw new OperationCanceledException("timeout");

            if (url == "https://identity.example/access-control/attributes")
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Json(JsonSerializer.Serialize(
                    new[] { new PublishedAttribute(AttributeTarget.Subject, "subject", "dept") },
                    JsonOptions));
            }

            throw new InvalidOperationException(url);
        });

        var timeoutOptions = new AttributeCatalogOptions();
        timeoutOptions.AddProviderApp("slow", "https://slow.example");
        var timeoutClient = new AttributeCatalogClient(new HttpClient(handler), timeoutOptions);

        var pull = await timeoutClient.PullAsync(CancellationToken.None);

        Assert.Empty(pull.ProviderApps);
        Assert.Equal(["slow"], pull.FailedApps);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var cancelOptions = new AttributeCatalogOptions();
        cancelOptions.AddProviderApp("identity", "https://identity.example");
        var cancelClient = new AttributeCatalogClient(new HttpClient(handler), cancelOptions);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelClient.PullAsync(canceled.Token));
    }

    [Fact]
    public async Task PullAsync_records_missing_local_source_and_propagates_caller_cancellation()
    {
        var httpCalled = false;
        var handler = new StubHandler(_ =>
        {
            httpCalled = true;
            throw new InvalidOperationException("no http");
        });
        var options = new AttributeCatalogOptions();
        options.AddLocalProviderApp("identity");
        using var provider = new ServiceCollection().BuildServiceProvider();
        var client = new AttributeCatalogClient(new HttpClient(handler), options, provider);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.False(httpCalled);
        Assert.Empty(pull.ProviderApps);
        Assert.Equal(["identity"], pull.FailedApps);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PullAsync(canceled.Token));
    }

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private sealed class FixedProvider(IReadOnlyList<PublishedAttribute> rows) : IPublishedProviderAttributeSource
    {
        public IReadOnlyList<PublishedAttribute> List() => rows;
    }

    private sealed class FixedEnforcement(IReadOnlyList<PublishedAttribute> rows) : IPublishedEnforcementAttributeSource
    {
        public IReadOnlyList<PublishedAttribute> List() => rows;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            : this((request, _) => handler(request))
        {
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request, cancellationToken));
    }
}
