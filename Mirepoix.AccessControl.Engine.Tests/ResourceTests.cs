using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Engine.Tests;

public sealed class ResourceTests
{
    [Fact]
    public void Id_returns_single_id_part()
    {
        var resource = new Resource(
            "doc",
            ResourceKey.Single("42"),
            new Dictionary<string, object?>());

#pragma warning disable CS0618 // Intentional: tests obsolete Id bridge
        Assert.Equal("42", resource.Id);
#pragma warning restore CS0618
        Assert.Equal(ResourceKey.Single("42"), resource.Key);
    }

    [Fact]
    public void Id_returns_canonical_for_composite()
    {
        var key = ResourceKey.From(("tenantId", "acme"), ("documentId", "1"));
        var resource = new Resource("document", key, new Dictionary<string, object?>());
#pragma warning disable CS0618 // Intentional: tests obsolete Id bridge
        Assert.Equal(key.Canonical(), resource.Id);
#pragma warning restore CS0618
    }
}
