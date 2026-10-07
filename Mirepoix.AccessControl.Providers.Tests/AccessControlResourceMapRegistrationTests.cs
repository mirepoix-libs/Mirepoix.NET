using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Providers.Tests;

public sealed class AccessControlResourceMapRegistrationTests
{
    [Fact]
    public async Task Apply_RegistersHydrator_FromOptions()
    {
        var services = new ServiceCollection();
        var options = new TestOptions();
        options.MapResource<Doc>(m => m
            .Type("doc")
            .Id(x => x.Id)
            .Attribute(x => x.Status, "status")
            .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Doc?>(new Doc { Id = id, Status = "ok" });
            }));

        AccessControlResourceMapRegistration.Apply(services, options.ResourceMapping);

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var resource = await scope.ServiceProvider.GetRequiredService<IResourceHydrator>()
            .HydrateAsync(new Resource("doc", ResourceKey.Single("1"), new Dictionary<string, object?>()), default);

        Assert.Equal("ok", resource.Attributes["status"]);
    }

    [Fact]
    public async Task Apply_MapsOwnerToOwnerIdAndOtherPropertiesByDefault()
    {
        var services = new ServiceCollection();
        var mapping = new ResourceMappingOptions();
        mapping.MapEntity<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Owner(document => document.CreatedByUserId)
            .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Document?>(new Document
                {
                    Id = id,
                    CreatedByUserId = "user-7",
                    Status = "draft",
                });
            }));

        AccessControlResourceMapRegistration.Apply(services, mapping);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var resource = await scope.ServiceProvider
            .GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource("doc", ResourceKey.Single("42"), new Dictionary<string, object?>()),
                CancellationToken.None);

        Assert.Equal("doc", resource.Type);
        Assert.Equal(ResourceKey.Single("42"), resource.Key);
        Assert.Equal("user-7", resource.Attributes["ownerId"]);
        Assert.Equal("draft", resource.Attributes["Status"]);
        Assert.DoesNotContain(nameof(Document.CreatedByUserId), resource.Attributes);
        Assert.DoesNotContain(nameof(Document.Id), resource.Attributes);
    }

    [Fact]
    public void MapEntity_RequiresLoad()
    {
        var mapping = new ResourceMappingOptions();

        var exception = Assert.Throws<InvalidOperationException>(
            () => mapping.MapEntity<Document>(map => map
                .Type("doc")
                .Id(document => document.Id)));

        Assert.Contains("Load", exception.Message);
    }

    [Fact]
    public void Apply_RejectsTypeRegisteredByHandRolledResolver()
    {
        var services = new ServiceCollection();
        services.AddAccessControlResourceResolver<DocumentResolver>();

        var mapping = new ResourceMappingOptions();
        mapping.MapEntity<Document>(map => map
            .Type("document")
            .Id(document => document.Id)
            .Load((_, _) => Task.FromResult<Document?>(new Document())));

        var exception = Assert.Throws<InvalidOperationException>(
            () => AccessControlResourceMapRegistration.Apply(services, mapping));

        Assert.Contains("document", exception.Message);
    }

    [Fact]
    public async Task Apply_ThrowsWhenLoadCannotFindEntity()
    {
        var services = new ServiceCollection();
        var mapping = new ResourceMappingOptions();
        mapping.MapEntity<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Load((_, _) => Task.FromResult<Document?>(null)));

        AccessControlResourceMapRegistration.Apply(services, mapping);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => scope.ServiceProvider
                .GetRequiredService<IResourceHydrator>()
                .HydrateAsync(
                    new Resource("doc", ResourceKey.Single("missing"), new Dictionary<string, object?>()),
                    CancellationToken.None));
    }

    [Fact]
    public async Task Apply_AttributeUsesIncludeOnlyModeAndRename()
    {
        var services = new ServiceCollection();
        var mapping = new ResourceMappingOptions();
        mapping.MapEntity<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Attribute(document => document.Status, "state")
            .Load((key, _) =>
            {
                var id = key.GetRequired("id");
                return Task.FromResult<Document?>(new Document
                {
                    Id = id,
                    Status = "draft",
                    InternalNote = "hidden",
                });
            }));

        AccessControlResourceMapRegistration.Apply(services, mapping);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var resource = await scope.ServiceProvider
            .GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource("doc", ResourceKey.Single("42"), new Dictionary<string, object?>()),
                CancellationToken.None);

        var attribute = Assert.Single(resource.Attributes);
        Assert.Equal("state", attribute.Key);
        Assert.Equal("draft", attribute.Value);
    }

    [Fact]
    public async Task Apply_LoadTypedService_ResolvesFromCurrentScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<DocumentStore>();
        var mapping = new ResourceMappingOptions();
        mapping.MapEntity<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Attribute(document => document.Status, "status")
            .Load<DocumentStore>((store, key, ct) => store.FindAsync(key.GetRequired("id"), ct)));

        AccessControlResourceMapRegistration.Apply(services, mapping);
        await using var provider = services.BuildServiceProvider();

        await using var firstScope = provider.CreateAsyncScope();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<DocumentStore>();
        var first = await firstScope.ServiceProvider
            .GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource("doc", ResourceKey.Single("1"), new Dictionary<string, object?>()),
                CancellationToken.None);

        Assert.Equal("from-store", first.Attributes["status"]);
        Assert.Equal(1, firstStore.FindCount);

        await using var secondScope = provider.CreateAsyncScope();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<DocumentStore>();
        await secondScope.ServiceProvider
            .GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource("doc", ResourceKey.Single("2"), new Dictionary<string, object?>()),
                CancellationToken.None);

        Assert.Equal(1, firstStore.FindCount);
        Assert.Equal(1, secondStore.FindCount);
        Assert.NotSame(firstStore, secondStore);
    }

    [Fact]
    public async Task Apply_LoadServiceProvider_ResolvesMultipleServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DocumentLookup("singleton-status"));
        services.AddScoped<DocumentStore>();
        var mapping = new ResourceMappingOptions();
        mapping.MapEntity<Document>(map => map
            .Type("doc")
            .Id(document => document.Id)
            .Attribute(document => document.Status, "status")
            .Load(async (sp, key, ct) =>
            {
                var id = key.GetRequired("id");
                var lookup = sp.GetRequiredService<DocumentLookup>();
                var store = sp.GetRequiredService<DocumentStore>();
                var document = await store.FindAsync(id, ct).ConfigureAwait(false);
                return document is null
                    ? null
                    : new Document { Id = id, Status = lookup.Status };
            }));

        AccessControlResourceMapRegistration.Apply(services, mapping);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var resource = await scope.ServiceProvider
            .GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource("doc", ResourceKey.Single("9"), new Dictionary<string, object?>()),
                CancellationToken.None);

        Assert.Equal("singleton-status", resource.Attributes["status"]);
    }

    [Fact]
    public async Task Apply_Hydrates_CompositeKey()
    {
        var services = new ServiceCollection();
        var options = new TestOptions();
        options.MapResource<TenantDoc>(m => m
            .Type("document")
            .Key("tenantId", x => x.TenantId)
            .Key("documentId", x => x.DocumentId)
            .Attribute(x => x.Status, "status")
            .Load((key, _) =>
            {
                Assert.Equal("acme", key.GetRequired("tenantId"));
                Assert.Equal("doc-1", key.GetRequired("documentId"));
                return Task.FromResult<TenantDoc?>(new TenantDoc
                {
                    TenantId = "acme",
                    DocumentId = "doc-1",
                    Status = "ok",
                });
            }));

        AccessControlResourceMapRegistration.Apply(services, options.ResourceMapping);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var resource = await scope.ServiceProvider.GetRequiredService<IResourceHydrator>()
            .HydrateAsync(
                new Resource(
                    "document",
                    ResourceKey.From(("tenantId", "acme"), ("documentId", "doc-1")),
                    new Dictionary<string, object?>()),
                default);

        Assert.Equal("ok", resource.Attributes["status"]);
        Assert.Equal(
            ResourceKey.From(("tenantId", "acme"), ("documentId", "doc-1")),
            resource.Key);
    }

    [Fact]
    public async Task Apply_Missing_KeyPart_Throws()
    {
        var services = new ServiceCollection();
        var mapping = new ResourceMappingOptions();
        mapping.MapEntity<TenantDoc>(map => map
            .Type("document")
            .Key("tenantId", x => x.TenantId)
            .Key("documentId", x => x.DocumentId)
            .Load((_, _) => Task.FromResult<TenantDoc?>(new TenantDoc())));

        AccessControlResourceMapRegistration.Apply(services, mapping);
        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            scope.ServiceProvider.GetRequiredService<IResourceHydrator>()
                .HydrateAsync(
                    new Resource("document", ResourceKey.Single("doc-1"), new Dictionary<string, object?>()),
                    default));

        Assert.Contains("tenantId", ex.Message);
    }

    [Fact]
    public void Key_RejectsDuplicatePartName()
    {
        var mapping = new ResourceMappingOptions();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            mapping.MapEntity<TenantDoc>(map => map
                .Type("document")
                .Key("tenantId", x => x.TenantId)
                .Key("tenantId", x => x.DocumentId)
                .Load((_, _) => Task.FromResult<TenantDoc?>(null))));

        Assert.Contains("tenantId", ex.Message);
    }

    private sealed class TestOptions : AccessControlProviderOptions<TestOptions>;

    private sealed class Doc
    {
        public string Id { get; set; } = "";

        public string Status { get; set; } = "";
    }

    private sealed class Document
    {
        public string Id { get; init; } = "";

        public string CreatedByUserId { get; init; } = "";

        public string Status { get; init; } = "";

        public string InternalNote { get; init; } = "";
    }

    private sealed class TenantDoc
    {
        public string TenantId { get; set; } = "";

        public string DocumentId { get; set; } = "";

        public string Status { get; set; } = "";
    }

    private sealed class DocumentLookup(string status)
    {
        public string Status { get; } = status;
    }

    private sealed class DocumentStore
    {
        public int FindCount { get; private set; }

        public Task<Document?> FindAsync(string id, CancellationToken cancellationToken)
        {
            FindCount++;
            return Task.FromResult<Document?>(new Document
            {
                Id = id,
                Status = "from-store",
            });
        }
    }

    [AccessResourceType("document")]
    private sealed class DocumentResolver : IResourceResolver<Document>
    {
        public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
            Task.FromResult(partial);
    }
}
