using System.Net;
using System.Text;
using System.Text.Json;
using Mirepoix.AccessControl.Engine.Client.Http;
using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Engine.Client.Http.Tests;

public sealed class HttpAccessCheckerTests
{
    [Fact]
    public async Task CheckAsync_PostsRequest_AndMapsDecision()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/access-control/check", request.RequestUri!.AbsolutePath);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);

            var decision = AccessDecisionDto.FromDomain(new AccessDecision(
                AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1"));

            return JsonResponse(decision);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var checker = new HttpAccessChecker(http);

        var result = await checker.CheckAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, result.Result);
        Assert.Equal(DecisionStatus.Success, result.Status);
        Assert.Equal("v1", result.PolicySetVersion);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"policyHits\":[]}")]
    [InlineData("{\"result\":\"Allow\"}")]
    [InlineData("{\"status\":\"Success\"}")]
    [InlineData("{\"result\":null,\"status\":\"Success\"}")]
    [InlineData("{\"Result\":\"Allow\",\"Status\":\"Success\"}")]
    public async Task CheckAsync_Throws_WhenResultOrStatusMissing(string json)
    {
        var http = new HttpClient(new StubHandler(_ => RawJson(json)))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var checker = new HttpAccessChecker(http);

        var exception = await Assert.ThrowsAsync<JsonException>(() =>
            checker.CheckAsync(SampleRequest(), CancellationToken.None));

        Assert.Equal("PDP response must include result and status.", exception.Message);
    }

    [Fact]
    public async Task CheckAsync_MapsExplicitDeny()
    {
        var http = new HttpClient(new StubHandler(_ => RawJson("{\"result\":\"Deny\",\"status\":\"Defaulted\",\"policyHits\":[]}")))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var checker = new HttpAccessChecker(http);

        var decision = await checker.CheckAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.Defaulted, decision.Status);
    }

    [Fact]
    public async Task CheckAsync_MapsNumericZeroResultAsAllow()
    {
        var http = new HttpClient(new StubHandler(_ => RawJson("{\"result\":0,\"status\":0,\"policyHits\":[]}")))
        {
            BaseAddress = new Uri("http://localhost/"),
        };
        var checker = new HttpAccessChecker(http);

        var decision = await checker.CheckAsync(SampleRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
    }

    [Fact]
    public async Task CheckAsync_PostsCompositeResourceKey_AndMapsDecision()
    {
        var handler = new AsyncStubHandler(async request =>
        {
            var json = await request.Content!.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);
            var resource = document.RootElement.GetProperty("resource");
            var key = resource.GetProperty("key");
            Assert.Equal("document", resource.GetProperty("type").GetString());
            Assert.Equal("acme", key.GetProperty("tenantId").GetString());
            Assert.Equal("1", key.GetProperty("documentId").GetString());
            Assert.False(resource.TryGetProperty("id", out _));

            var decision = AccessDecisionDto.FromDomain(new AccessDecision(
                AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1"));

            return JsonResponse(decision);
        });

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var checker = new HttpAccessChecker(http);

        var result = await checker.CheckAsync(CompositeSampleRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, result.Result);
        Assert.Equal(DecisionStatus.Success, result.Status);
    }

    private static AuthorizationRequest SampleRequest() =>
        new(
            new Subject("alice", new HashSet<string> { "editor" }, new Dictionary<string, object?> { ["dept"] = "eng" }),
            new Resource("document", ResourceKey.Single("doc-1"), new Dictionary<string, object?>()),
            Operation.Parse("document:read"),
            new AccessContext(
                new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
                new Dictionary<string, object?> { ["sub"] = "alice" },
                new Dictionary<string, object?>()));

    private static AuthorizationRequest CompositeSampleRequest() =>
        new(
            new Subject("alice", new HashSet<string> { "editor" }, new Dictionary<string, object?> { ["dept"] = "eng" }),
            new Resource(
                "document",
                ResourceKey.From(("tenantId", "acme"), ("documentId", "1")),
                new Dictionary<string, object?>()),
            Operation.Parse("document:read"),
            new AccessContext(
                new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
                new Dictionary<string, object?> { ["sub"] = "alice" },
                new Dictionary<string, object?>()));

    private static HttpResponseMessage JsonResponse(AccessDecisionDto decision)
    {
        var json = JsonSerializer.Serialize(decision, AccessControlHttpJson.DefaultOptions);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage RawJson(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }

    private sealed class AsyncStubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public AsyncStubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _handler(request);
    }
}
