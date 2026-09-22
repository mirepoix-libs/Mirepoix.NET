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

    private static AuthorizationRequest SampleRequest() =>
        new(
            new Subject("alice", new HashSet<string> { "editor" }, new Dictionary<string, object?> { ["dept"] = "eng" }),
            new Resource("document", "doc-1", new Dictionary<string, object?>()),
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

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
