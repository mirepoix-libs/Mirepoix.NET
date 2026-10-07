using System.Security.Claims;
using Mirepoix.AccessControl.Authorization.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

public class AccessCheckPipelineTests
{
    [Fact]
    public async Task Evaluate_allows_when_checker_returns_allow_success()
    {
        var http = AuthenticatedContext("u1", "EDITOR", operation: "doc:edit");
        var checker = new StubChecker(
            new AccessDecision(
                AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1"));

        var (outcome, decision) = await AccessCheckPipeline.EvaluateAsync(
            http,
            new DefaultClaimsPrincipalMapper(),
            checker,
            CancellationToken.None);

        Assert.Equal(AccessCheckPipeline.Outcome.Allow, outcome);
        Assert.Equal(AuthorizationResult.Allow, decision!.Result);
    }

    [Fact]
    public async Task Evaluate_forbids_when_defaulted()
    {
        var http = AuthenticatedContext("u1", "USER", operation: "doc:edit");
        var checker = new StubChecker(
            new AccessDecision(
                AuthorizationResult.Deny,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Defaulted,
                "v1"));

        var (outcome, _) = await AccessCheckPipeline.EvaluateAsync(
            http,
            new DefaultClaimsPrincipalMapper(),
            checker,
            CancellationToken.None);

        Assert.Equal(AccessCheckPipeline.Outcome.Forbid, outcome);
    }

    [Fact]
    public async Task Evaluate_forbids_when_operation_metadata_missing()
    {
        var http = AuthenticatedContext("u1", "EDITOR", operation: null);

        var (outcome, decision) = await AccessCheckPipeline.EvaluateAsync(
            http,
            new DefaultClaimsPrincipalMapper(),
            new StubChecker(
                new AccessDecision(
                    AuthorizationResult.Allow,
                    Array.Empty<PolicyHit>(),
                    DecisionStatus.Success,
                    "v1")),
            CancellationToken.None);

        Assert.Equal(AccessCheckPipeline.Outcome.Forbid, outcome);
        Assert.Null(decision);
    }

    [Fact]
    public async Task Evaluate_challenges_when_unauthenticated()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var (outcome, _) = await AccessCheckPipeline.EvaluateAsync(
            http,
            new DefaultClaimsPrincipalMapper(),
            new StubChecker(
                new AccessDecision(
                    AuthorizationResult.Allow,
                    Array.Empty<PolicyHit>(),
                    DecisionStatus.Success,
                    "v1")),
            CancellationToken.None);

        Assert.Equal(AccessCheckPipeline.Outcome.Challenge, outcome);
    }

    [Fact]
    public async Task Evaluate_builds_resource_from_route_values()
    {
        AuthorizationRequest? captured = null;
        var http = AuthenticatedContext("u1", "EDITOR", operation: "doc:edit", resourceType: "doc", routeId: "42");
        var checker = new CapturingChecker(d =>
        {
            captured = d;
            return new AccessDecision(
                AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1");
        });

        await AccessCheckPipeline.EvaluateAsync(
            http,
            new DefaultClaimsPrincipalMapper(),
            checker,
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("doc", captured!.Resource.Type);
        Assert.Equal(ResourceKey.Single("42"), captured.Resource.Key);
        Assert.Equal("doc:edit", captured.Operation.Value);
        Assert.Equal("u1", captured.Subject.Id);
    }

    [Fact]
    public async Task Evaluate_whitespace_route_id_yields_empty_resource_key()
    {
        AuthorizationRequest? captured = null;
        var http = AuthenticatedContext("u1", "EDITOR", operation: "doc:edit", resourceType: "doc", routeId: "   ");
        var checker = new CapturingChecker(d =>
        {
            captured = d;
            return new AccessDecision(
                AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1");
        });

        await AccessCheckPipeline.EvaluateAsync(
            http,
            new DefaultClaimsPrincipalMapper(),
            checker,
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(ResourceKey.Empty, captured!.Resource.Key);
    }

    [Fact]
    public async Task Evaluate_builds_composite_resource_from_key_bindings()
    {
        AuthorizationRequest? captured = null;
        var http = AuthenticatedContext("u1", "EDITOR", operation: "document:read");
        http.Request.RouteValues["tenantId"] = "acme";
        http.Request.RouteValues["documentId"] = "doc-1";

        var endpoint = new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new AccessOperationAttribute("document:read"),
                new AccessResourceAttribute("document", new[] { "tenantId", "documentId" })),
            "test");
        http.Features.Set<IEndpointFeature>(new EndpointFeature(endpoint));

        await AccessCheckPipeline.EvaluateAsync(
            http,
            new DefaultClaimsPrincipalMapper(),
            new CapturingChecker(d =>
            {
                captured = d;
                return new AccessDecision(
                    AuthorizationResult.Allow,
                    Array.Empty<PolicyHit>(),
                    DecisionStatus.Success,
                    "v1");
            }),
            CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(
            ResourceKey.From(("tenantId", "acme"), ("documentId", "doc-1")),
            captured!.Resource.Key);
    }

    private static DefaultHttpContext AuthenticatedContext(
        string userId,
        string role,
        string? operation,
        string? resourceType = null,
        string? routeId = null)
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Role, role)
                },
                authenticationType: "test"))
        };

        if (routeId is not null)
            http.Request.RouteValues["id"] = routeId;

        var metadata = new EndpointMetadataCollection(
            operation is null
                ? Array.Empty<object>()
                : resourceType is null
                    ? new object[] { new AccessOperationAttribute(operation) }
                    : new object[]
                    {
                        new AccessOperationAttribute(operation),
                        new AccessResourceAttribute(resourceType)
                    });

        var endpoint = new Endpoint(_ => Task.CompletedTask, metadata, "test");
        http.Features.Set<IEndpointFeature>(new EndpointFeature(endpoint));
        return http;
    }

    private sealed class EndpointFeature(Endpoint endpoint) : IEndpointFeature
    {
        public Endpoint? Endpoint { get; set; } = endpoint;
    }

    private sealed class StubChecker(AccessDecision decision) : IAccessChecker
    {
        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(decision);
    }

    private sealed class CapturingChecker(Func<AuthorizationRequest, AccessDecision> factory) : IAccessChecker
    {
        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(factory(request));
    }
}
