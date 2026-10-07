using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Engine.Tests;

public sealed class ResourceKeyTests
{
    [Fact]
    public void Empty_is_empty()
    {
        Assert.True(ResourceKey.Empty.IsEmpty);
        Assert.Empty(ResourceKey.Empty.Parts);
        Assert.Equal(string.Empty, ResourceKey.Empty.Canonical());
    }

    [Fact]
    public void Single_is_id_part()
    {
        var key = ResourceKey.Single("doc-1");
        Assert.False(key.IsEmpty);
        Assert.True(key.TryGet("id", out var id));
        Assert.Equal("doc-1", id);
        Assert.Equal("id=doc-1", key.Canonical());
    }

    [Fact]
    public void From_sorts_parts_and_equality_ignores_input_order()
    {
        var a = ResourceKey.From(("tenantId", "acme"), ("documentId", "doc-1"));
        var b = ResourceKey.From(("documentId", "doc-1"), ("tenantId", "acme"));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal("documentId=doc-1;tenantId=acme", a.Canonical());
        Assert.Equal("documentId", a.Parts[0].Name);
        Assert.Equal("tenantId", a.Parts[1].Name);
    }

    [Fact]
    public void From_rejects_blank_or_duplicate_names()
    {
        Assert.Throws<ArgumentException>(() => ResourceKey.From(("", "x")));
        Assert.Throws<ArgumentException>(() => ResourceKey.From(("id", "")));
        Assert.Throws<ArgumentException>(() =>
            ResourceKey.From(("id", "1"), ("id", "2")));
    }

    [Fact]
    public void GetRequired_throws_when_missing()
    {
        var key = ResourceKey.Single("1");
        var ex = Assert.Throws<KeyNotFoundException>(() => key.GetRequired("tenantId"));
        Assert.Contains("tenantId", ex.Message);
    }
}
