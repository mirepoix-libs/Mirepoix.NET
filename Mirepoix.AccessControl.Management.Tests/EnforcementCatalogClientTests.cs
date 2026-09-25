using System.Net;
using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;

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
