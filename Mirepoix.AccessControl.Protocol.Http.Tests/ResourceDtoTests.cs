using System.Text.Json;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Protocol.Http;

namespace Mirepoix.AccessControl.Protocol.Http.Tests;

public sealed class ResourceDtoTests
{
    [Fact]
    public void FromDomain_writes_key_not_id()
    {
        var resource = new Resource(
            "document",
            ResourceKey.From(("tenantId", "acme"), ("documentId", "doc-1")),
            new Dictionary<string, object?>());

        var json = JsonSerializer.Serialize(ResourceDto.FromDomain(resource), AccessControlHttpJson.DefaultOptions);

        Assert.Contains("\"key\"", json);
        Assert.Contains("\"tenantId\":\"acme\"", json);
        Assert.Contains("\"documentId\":\"doc-1\"", json);
        Assert.DoesNotContain("\"id\":", json);
    }

    [Fact]
    public void ToDomain_reads_legacy_id_when_key_absent()
    {
        var json = """{"type":"document","id":"doc-1","attributes":{}}""";
        var dto = JsonSerializer.Deserialize<ResourceDto>(json, AccessControlHttpJson.DefaultOptions)!;
        var resource = dto.ToDomain();

        Assert.Equal("document", resource.Type);
        Assert.Equal(ResourceKey.Single("doc-1"), resource.Key);
    }

    [Fact]
    public void ToDomain_key_wins_over_id()
    {
        var json = """{"type":"document","id":"legacy","key":{"id":"new"},"attributes":{}}""";
        var dto = JsonSerializer.Deserialize<ResourceDto>(json, AccessControlHttpJson.DefaultOptions)!;
        Assert.Equal(ResourceKey.Single("new"), dto.ToDomain().Key);
    }

    [Fact]
    public void AuthorizationRequestDto_round_trips_composite_key()
    {
        var domain = new AuthorizationRequest(
            new Subject("alice", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource(
                "document",
                ResourceKey.From(("tenantId", "acme"), ("documentId", "doc-1")),
                new Dictionary<string, object?>()),
            Operation.Parse("document:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

        var json = JsonSerializer.Serialize(AuthorizationRequestDto.FromDomain(domain), AccessControlHttpJson.DefaultOptions);
        var back = JsonSerializer.Deserialize<AuthorizationRequestDto>(json, AccessControlHttpJson.DefaultOptions)!.ToDomain();

        Assert.Equal(domain.Resource.Key, back.Resource.Key);
        Assert.Equal("document", back.Resource.Type);
    }
}
