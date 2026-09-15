using System.Security.Claims;
using Mirepoix.AccessControl.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

public class AccessAuthorizationHandlerTests
{
    [Fact]
    public async Task Handler_succeeds_on_allow_success()
    {
        var http = PipelineHttp.Authenticated("u1", "EDITOR", "doc:edit");
        var handler = new AccessAuthorizationHandler(
            new DefaultClaimsPrincipalMapper(),
            new StubChecker(AllowSuccess()));

        var auth = new AuthorizationHandlerContext(
            new[] { new AccessRequirement() },
            http.User,
            http);

        await handler.HandleAsync(auth);

        Assert.True(auth.HasSucceeded);
    }

    [Fact]
    public async Task Handler_fails_on_defaulted()
    {
        var http = PipelineHttp.Authenticated("u1", "USER", "doc:edit");
        var handler = new AccessAuthorizationHandler(
            new DefaultClaimsPrincipalMapper(),
            new StubChecker(new AccessDecision(
                Mirepoix.AccessControl.AuthorizationResult.Deny,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Defaulted,
                "v1")));

        var auth = new AuthorizationHandlerContext(
            new[] { new AccessRequirement() },
            http.User,
            http);

        await handler.HandleAsync(auth);

        Assert.True(auth.HasFailed);
    }

    private static AccessDecision AllowSuccess() =>
        new(Mirepoix.AccessControl.AuthorizationResult.Allow, Array.Empty<PolicyHit>(), DecisionStatus.Success, "v1");

    private sealed class StubChecker(AccessDecision decision) : IAccessChecker
    {
        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(decision);
    }
}

public class AccessEndpointFilterTests
{
    [Fact]
    public async Task Filter_returns_forbid_when_denied()
    {
        var http = PipelineHttp.Authenticated("u1", "USER", "doc:edit");
        var filter = new AccessEndpointFilter(
            new DefaultClaimsPrincipalMapper(),
            new StubChecker(new AccessDecision(
                Mirepoix.AccessControl.AuthorizationResult.Deny,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Defaulted,
                "v1")));

        var context = new DefaultEndpointFilterInvocationContext(http);
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("ok"));

        Assert.IsAssignableFrom<IResult>(result);
        var forbid = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult>(result);
        Assert.NotNull(forbid);
    }

    [Fact]
    public async Task Filter_invokes_next_when_allowed()
    {
        var http = PipelineHttp.Authenticated("u1", "EDITOR", "doc:edit");
        var filter = new AccessEndpointFilter(
            new DefaultClaimsPrincipalMapper(),
            new StubChecker(new AccessDecision(
                Mirepoix.AccessControl.AuthorizationResult.Allow,
                Array.Empty<PolicyHit>(),
                DecisionStatus.Success,
                "v1")));

        var context = new DefaultEndpointFilterInvocationContext(http);
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("ok"));

        Assert.Equal("ok", result);
    }

    private sealed class StubChecker(AccessDecision decision) : IAccessChecker
    {
        public Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(decision);
    }
}

internal static class PipelineHttp
{
    public static DefaultHttpContext Authenticated(string userId, string role, string operation)
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

        var metadata = new EndpointMetadataCollection(new AccessOperationAttribute(operation));
        var endpoint = new Endpoint(_ => Task.CompletedTask, metadata, "test");
        http.Features.Set<IEndpointFeature>(new Feature(endpoint));
        return http;
    }

    private sealed class Feature(Endpoint endpoint) : IEndpointFeature
    {
        public Endpoint? Endpoint { get; set; } = endpoint;
    }
}
