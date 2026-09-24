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
