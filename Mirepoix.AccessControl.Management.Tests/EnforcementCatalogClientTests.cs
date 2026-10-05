using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class EnforcementCatalogClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public async Task PullAsync_returns_billing_operations_and_failed_invoices()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://billing.example/access-control/operations")
            {
                var json = JsonSerializer.Serialize(
                    new[] { new PublishedOperation("invoice:post", "invoices/{id}", "POST") },
                    JsonOptions);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            if (url == "https://invoices.example/access-control/operations")
                throw new HttpRequestException("refused");

            throw new InvalidOperationException(url);
        });

        var options = new OperationCatalogOptions();
        options.AddApp("billing", "https://billing.example");
        options.AddApp("invoices", "https://invoices.example");
        var client = new EnforcementCatalogClient(new HttpClient(handler), options);

        var pull = await client.PullAsync(CancellationToken.None);

        var billing = Assert.Single(pull.Apps);
        Assert.Equal("billing", billing.Name);
        var operation = Assert.Single(billing.Operations);
        Assert.Equal("invoice:post", operation.Operation);
        Assert.Equal("invoices/{id}", operation.RouteTemplate);
        Assert.Equal("POST", operation.HttpMethod);
        Assert.Equal(["invoices"], pull.FailedApps);
    }

    [Fact]
    public async Task PullAsync_trims_trailing_slash_and_records_non_success_null_and_invalid_json()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://billing.example/access-control/operations")
            {
                var json = JsonSerializer.Serialize(
                    new[] { new PublishedOperation("invoice:post", "", "") },
                    JsonOptions);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            if (url == "https://down.example/access-control/operations")
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            if (url == "https://null.example/access-control/operations")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("null", Encoding.UTF8, "application/json"),
                };
            }

            if (url == "https://bad.example/access-control/operations")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{", Encoding.UTF8, "application/json"),
                };
            }

            throw new InvalidOperationException(url);
        });

        var options = new OperationCatalogOptions();
        options.AddApp("billing", "https://billing.example/");
        options.AddApp("down", "https://down.example");
        options.AddApp("nulljson", "https://null.example");
        options.AddApp("bad", "https://bad.example");
        var client = new EnforcementCatalogClient(new HttpClient(handler), options);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.Equal("billing", Assert.Single(pull.Apps).Name);
        Assert.Equal(["down", "nulljson", "bad"], pull.FailedApps);
    }

    [Fact]
    public async Task PullAsync_records_timeout_and_propagates_caller_cancellation()
    {
        var handler = new StubHandler((request, cancellationToken) =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://slow.example/access-control/operations")
                throw new OperationCanceledException("timeout");

            if (url == "https://billing.example/access-control/operations")
            {
                cancellationToken.ThrowIfCancellationRequested();
                var json = JsonSerializer.Serialize(
                    new[] { new PublishedOperation("invoice:post", "", "") },
                    JsonOptions);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            throw new InvalidOperationException(url);
        });

        var timeoutOptions = new OperationCatalogOptions();
        timeoutOptions.AddApp("slow", "https://slow.example");
        var timeoutClient = new EnforcementCatalogClient(new HttpClient(handler), timeoutOptions);

        var pull = await timeoutClient.PullAsync(CancellationToken.None);

        Assert.Empty(pull.Apps);
        Assert.Equal(["slow"], pull.FailedApps);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var cancelOptions = new OperationCatalogOptions();
        cancelOptions.AddApp("billing", "https://billing.example");
        var cancelClient = new EnforcementCatalogClient(new HttpClient(handler), cancelOptions);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelClient.PullAsync(canceled.Token));
    }

    [Fact]
    public async Task PullAsync_records_non_json_content_type_and_continues()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://plain.example/access-control/operations")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("not-json", Encoding.UTF8, "text/plain"),
                };
            }

            if (url == "https://billing.example/access-control/operations")
            {
                var json = JsonSerializer.Serialize(
                    new[] { new PublishedOperation("invoice:post", "", "") },
                    JsonOptions);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            throw new InvalidOperationException(url);
        });

        var options = new OperationCatalogOptions();
        options.AddApp("plain", "https://plain.example");
        options.AddApp("billing", "https://billing.example");
        var client = new EnforcementCatalogClient(new HttpClient(handler), options);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.Equal(["plain"], pull.FailedApps);
        var billing = Assert.Single(pull.Apps);
        Assert.Equal("billing", billing.Name);
        Assert.Equal("invoice:post", Assert.Single(billing.Operations).Operation);
    }

    [Fact]
    public async Task PullAsync_records_other_exception_and_continues()
    {
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url == "https://invoices.example/access-control/operations")
                throw new InvalidOperationException("catalog builder failed");

            if (url == "https://billing.example/access-control/operations")
            {
                var json = JsonSerializer.Serialize(
                    new[] { new PublishedOperation("invoice:post", "", "") },
                    JsonOptions);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            throw new InvalidOperationException(url);
        });

        var options = new OperationCatalogOptions();
        options.AddApp("invoices", "https://invoices.example");
        options.AddApp("billing", "https://billing.example");
        var client = new EnforcementCatalogClient(new HttpClient(handler), options);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.Equal(["invoices"], pull.FailedApps);
        var billing = Assert.Single(pull.Apps);
        Assert.Equal("billing", billing.Name);
        Assert.Equal("invoice:post", Assert.Single(billing.Operations).Operation);
    }

    [Fact]
    public void AddLocalEnforcementApp_defaults_to_local_and_rejects_a_repeated_name()
    {
        var options = new OperationCatalogOptions();
        options.AddApp("billing", "https://billing.example");
        options.AddLocalEnforcementApp();

        Assert.Equal("billing", options.Apps[0].Name);
        Assert.Equal("https://billing.example", options.Apps[0].Origin);
        Assert.False(options.Apps[0].IsLocal);
        Assert.Equal("local", options.Apps[1].Name);
        Assert.Null(options.Apps[1].Origin);
        Assert.True(options.Apps[1].IsLocal);

        Assert.Throws<ArgumentException>(() => options.AddApp("billing", "https://other.example"));
        Assert.Throws<ArgumentException>(() => options.AddLocalEnforcementApp("billing"));
        Assert.Throws<ArgumentException>(() => options.AddLocalEnforcementApp("local"));
        Assert.Throws<ArgumentException>(() => options.AddLocalEnforcementApp(" "));
    }

    [Fact]
    public async Task PullAsync_reads_local_operations_without_http_and_keeps_remote_peers()
    {
        var httpCalled = false;
        var handler = new StubHandler(request =>
        {
            httpCalled = true;
            if (request.RequestUri!.AbsoluteUri == "https://billing.example/access-control/operations")
            {
                var json = JsonSerializer.Serialize(
                    new[] { new PublishedOperation("invoice:post", "invoices/{id}", "POST") },
                    JsonOptions);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
            }

            throw new InvalidOperationException(request.RequestUri.AbsoluteUri);
        });

        var services = new ServiceCollection();
        services.AddSingleton<IPublishedOperationSource>(new FixedOperationSource(
        [
            new PublishedOperation("doc:read", "", ""),
        ]));
        using var provider = services.BuildServiceProvider();

        var options = new OperationCatalogOptions();
        options.AddLocalEnforcementApp();
        options.AddApp("billing", "https://billing.example");
        var client = new EnforcementCatalogClient(new HttpClient(handler), options, provider);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.Equal(["local", "billing"], pull.Apps.Select(app => app.Name).ToArray());
        Assert.Equal("doc:read", Assert.Single(pull.Apps[0].Operations).Operation);
        Assert.Equal("invoice:post", Assert.Single(pull.Apps[1].Operations).Operation);
        Assert.Empty(pull.FailedApps);
        Assert.True(httpCalled);
    }

    [Fact]
    public async Task PullAsync_records_missing_local_source_without_http()
    {
        var httpCalled = false;
        var handler = new StubHandler(_ =>
        {
            httpCalled = true;
            throw new InvalidOperationException("no http");
        });
        var options = new OperationCatalogOptions();
        options.AddLocalEnforcementApp("gateway");
        using var provider = new ServiceCollection().BuildServiceProvider();
        var client = new EnforcementCatalogClient(new HttpClient(handler), options, provider);

        var pull = await client.PullAsync(CancellationToken.None);

        Assert.False(httpCalled);
        Assert.Empty(pull.Apps);
        Assert.Equal(["gateway"], pull.FailedApps);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.PullAsync(canceled.Token));
    }

    [Fact]
    public async Task AddAccessControlOperationCatalog_resolves_local_operation_source()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySetEditor, UnusedEditor>();
        services.AddSingleton<IPublishedOperationSource>(new FixedOperationSource(
        [
            new PublishedOperation("invoice:post", "", ""),
        ]));
        services.AddAccessControlOperationCatalog(options => options.AddLocalEnforcementApp());

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEnforcementCatalogClient>();
        var pull = await client.PullAsync(CancellationToken.None);

        Assert.Empty(pull.FailedApps);
        Assert.Equal("local", Assert.Single(pull.Apps).Name);
        Assert.Equal("invoice:post", Assert.Single(pull.Apps[0].Operations).Operation);
    }

    private sealed class FixedOperationSource(IReadOnlyList<PublishedOperation> operations) : IPublishedOperationSource
    {
        public IReadOnlyList<PublishedOperation> List() => operations;
    }

    private sealed class UnusedEditor : IPolicySetEditor
    {
        public void Replace(PolicySet set)
        {
        }

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
