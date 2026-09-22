using System.Text.Json;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Protocol.Http.Tests;

public sealed class WireJsonRoundTripTests
{
    [Fact]
    public void AuthorizationRequestDto_RoundTrips_PartialRequest()
    {
        var domain = new AuthorizationRequest(
            new Subject("alice", new HashSet<string> { "editor" }, new Dictionary<string, object?> { ["dept"] = "eng" }),
            new Resource("document", "doc-1", new Dictionary<string, object?>()),
            Operation.Parse("document:read"),
            new AccessContext(
                new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.Zero),
                new Dictionary<string, object?> { ["sub"] = "alice" },
                new Dictionary<string, object?>()));

        var json = JsonSerializer.Serialize(AuthorizationRequestDto.FromDomain(domain), AccessControlHttpJson.DefaultOptions);
        var dto = JsonSerializer.Deserialize<AuthorizationRequestDto>(json, AccessControlHttpJson.DefaultOptions)!;
        var back = dto.ToDomain();

        Assert.Equal("alice", back.Subject.Id);
        Assert.Contains("editor", back.Subject.Roles);
        Assert.Equal("eng", back.Subject.Attributes["dept"]);
        Assert.Equal("document:read", back.Operation.Value);
    }

    [Fact]
    public void AccessDecisionDto_RoundTrips_HydrationFailed()
    {
        var domain = new AccessDecision(
            AuthorizationResult.Deny,
            Array.Empty<PolicyHit>(),
            DecisionStatus.HydrationFailed,
            "v1");

        var json = JsonSerializer.Serialize(AccessDecisionDto.FromDomain(domain), AccessControlHttpJson.DefaultOptions);
        Assert.Contains("HydrationFailed", json);
        var back = JsonSerializer.Deserialize<AccessDecisionDto>(json, AccessControlHttpJson.DefaultOptions)!.ToDomain();
        Assert.Equal(DecisionStatus.HydrationFailed, back.Status);
        Assert.Equal("v1", back.PolicySetVersion);
    }
}
