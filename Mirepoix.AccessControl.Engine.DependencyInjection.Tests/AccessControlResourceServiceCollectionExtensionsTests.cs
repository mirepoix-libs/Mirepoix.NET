using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Engine.DependencyInjection.Tests;

public sealed class AccessControlResourceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAccessControlResourceResolver_RequiresResourceTypeAttribute()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAccessControlResourceResolver<UnattributedResolver>());

        Assert.Contains(nameof(AccessResourceTypeAttribute), exception.Message);
    }

    [Fact]
    public async Task HydrateAsync_ThrowsForUnknownResourceType()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceResolver<DocumentResolver>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var hydrator = scope.ServiceProvider.GetRequiredService<IResourceHydrator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => hydrator.HydrateAsync(
                new Resource("invoice", "42", new Dictionary<string, object?>()),
                CancellationToken.None));

        Assert.Contains("invoice", exception.Message);
    }

    [Fact]
    public async Task AddAccessControlResourceResolver_RegistersScopedResolverAndHydrates()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceResolver<DocumentResolver>();
        await using var provider = services.BuildServiceProvider();

        await using var firstScope = provider.CreateAsyncScope();
        var concrete = firstScope.ServiceProvider.GetRequiredService<DocumentResolver>();
        var typed = firstScope.ServiceProvider.GetRequiredService<IResourceResolver<Document>>();
        var hydrator = firstScope.ServiceProvider.GetRequiredService<IResourceHydrator>();
        var hydrated = await hydrator.HydrateAsync(
            new Resource("document", "42", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Same(concrete, typed);
        Assert.Equal("42", hydrated.Id);
        Assert.Equal("owned", hydrated.Attributes["status"]);

        await using var secondScope = provider.CreateAsyncScope();
        Assert.NotSame(
            concrete,
            secondScope.ServiceProvider.GetRequiredService<DocumentResolver>());
    }

    [Fact]
    public void AddAccessControlResourceResolver_RejectsDuplicateType()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceResolver<DocumentResolver>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAccessControlResourceResolver<DuplicateDocumentResolver>());

        Assert.Contains("document", exception.Message);
    }

    [Fact]
    public void AddAccessControlResourceResolver_PreservesCustomHydrator()
    {
        var services = new ServiceCollection();
        services.AddScoped<IResourceHydrator, CustomHydrator>();

        services.AddAccessControlResourceResolver<DocumentResolver>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType<CustomHydrator>(
            scope.ServiceProvider.GetRequiredService<IResourceHydrator>());
    }

    [Fact]
    public async Task AddAccessControlResourceMap_MapsOwnerToOwnerIdAndOtherPropertiesByDefault()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceMap<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Owner(document => document.CreatedByUserId)
            .Load((id, _) => Task.FromResult<Document?>(new Document
            {
                Id = id,
                CreatedByUserId = "user-7",
                Status = "draft",
            })));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var resource = await scope.ServiceProvider
            .GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource("doc", "42", new Dictionary<string, object?>()),
                CancellationToken.None);

        Assert.Equal("doc", resource.Type);
        Assert.Equal("42", resource.Id);
        Assert.Equal("user-7", resource.Attributes["ownerId"]);
        Assert.Equal("draft", resource.Attributes["Status"]);
        Assert.DoesNotContain(nameof(Document.CreatedByUserId), resource.Attributes);
        Assert.DoesNotContain(nameof(Document.Id), resource.Attributes);
    }

    [Fact]
    public void AddAccessControlResourceMap_RequiresLoad()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAccessControlResourceMap<Document>(map => map
                .Type("doc")
                .Id(document => document.Id)));

        Assert.Contains("Load", exception.Message);
    }

    [Fact]
    public void AddAccessControlResourceMap_RejectsTypeRegisteredByHandRolledResolver()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceResolver<DocumentResolver>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAccessControlResourceMap<Document>(map => map
                .Type("document")
                .Id(document => document.Id)
                .Load((_, _) => Task.FromResult<Document?>(new Document()))));

        Assert.Contains("document", exception.Message);
    }

    [Fact]
    public async Task AddAccessControlResourceMap_ThrowsWhenLoadCannotFindEntity()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceMap<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Load((_, _) => Task.FromResult<Document?>(null)));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => scope.ServiceProvider
                .GetRequiredService<IResourceHydrator>()
                .HydrateAsync(
                    new Resource("doc", "missing", new Dictionary<string, object?>()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task AddAccessControlResourceMap_AttributeUsesIncludeOnlyModeAndRename()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceMap<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Attribute(document => document.Status, "state")
            .Load((id, _) => Task.FromResult<Document?>(new Document
            {
                Id = id,
                Status = "draft",
                InternalNote = "hidden",
            })));
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var resource = await scope.ServiceProvider
            .GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource("doc", "42", new Dictionary<string, object?>()),
                CancellationToken.None);

        var attribute = Assert.Single(resource.Attributes);
        Assert.Equal("state", attribute.Key);
        Assert.Equal("draft", attribute.Value);
    }

    private sealed class Document
    {
        public string Id { get; init; } = "";

        public string CreatedByUserId { get; init; } = "";

        public string Status { get; init; } = "";

        public string InternalNote { get; init; } = "";
    }

    private sealed class Invoice;

    private sealed class UnattributedResolver : IResourceResolver<Document>
    {
        public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
            Task.FromResult(partial);
    }

    [AccessResourceType("document")]
    private sealed class DocumentResolver : IResourceResolver<Document>
    {
        public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
            Task.FromResult(partial with
            {
                Attributes = new Dictionary<string, object?> { ["status"] = "owned" }
            });
    }

    [AccessResourceType("document")]
    private sealed class DuplicateDocumentResolver : IResourceResolver<Invoice>
    {
        public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
            Task.FromResult(partial);
    }

    private sealed class CustomHydrator : IResourceHydrator
    {
        public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
            Task.FromResult(partial);
    }
}
