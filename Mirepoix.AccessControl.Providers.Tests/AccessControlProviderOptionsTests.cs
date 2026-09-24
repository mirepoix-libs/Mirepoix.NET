namespace Mirepoix.AccessControl.Providers.Tests;

public sealed class AccessControlProviderOptionsTests
{
    private sealed class TestOptions : AccessControlProviderOptions<TestOptions>;

    [Fact]
    public void MapResource_AddsMap_AndImpliesResourceHydrator()
    {
        var o = new TestOptions();
        o.MapResource<Doc>(m => m
            .Type("doc")
            .Id(x => x.Id)
            .Load((id, _) => Task.FromResult<Doc?>(new Doc { Id = id })));

        Assert.True(o.ResourceMapping.HasMaps);
        Assert.True(o.ResourceHydratorImplied);
        Assert.Equal("doc", Assert.Single(o.ResourceMapping.Maps).Type);
    }

    [Fact]
    public void AddResourceHydrator_IsIdempotent()
    {
        var o = new TestOptions();
        o.AddResourceHydrator().AddResourceHydrator();
        Assert.True(o.ResourceHydratorRequested);
    }

    [Fact]
    public void MapSubject_DelegatesToSubjectMapping()
    {
        var o = new TestOptions();
        o.MapSubject<User>(m => m.Id(x => x.Id));
        Assert.True(o.SubjectMapping.HasMaps);
    }

    [Fact]
    public void MapResource_DuplicateType_throws()
    {
        var o = new TestOptions();
        o.MapResource<Doc>(m => m
            .Type("doc")
            .Id(x => x.Id)
            .Load((id, _) => Task.FromResult<Doc?>(new Doc { Id = id })));

        var ex = Assert.Throws<InvalidOperationException>(() => o.MapResource<OtherDoc>(m => m
            .Type("doc")
            .Id(x => x.Id)
            .Load((id, _) => Task.FromResult<OtherDoc?>(new OtherDoc { Id = id }))));

        Assert.Contains("Duplicate resource type value", ex.Message, StringComparison.Ordinal);
    }

    private sealed class Doc { public string Id { get; set; } = ""; }
    private sealed class OtherDoc { public string Id { get; set; } = ""; }
    private sealed class User { public string Id { get; set; } = ""; }
}
