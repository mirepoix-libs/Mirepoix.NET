using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Providers.Tests;

public sealed class AccessControlProviderServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AgnosticAddAccessControlProviders_MapResource_WiresHydrator()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(o => o.MapResource<Doc>(m => m
            .Type("doc")
            .Id(x => x.Id)
            .Attribute(x => x.Status, "status")
            .Load((id, _) => Task.FromResult<Doc?>(new Doc { Id = id, Status = "ok" }))));

        await using var sp = services.BuildServiceProvider();
        await using var scope = sp.CreateAsyncScope();
        var resource = await scope.ServiceProvider.GetRequiredService<IResourceHydrator>()
            .HydrateAsync(new Resource("doc", "1", new Dictionary<string, object?>()), CancellationToken.None);

        Assert.Equal("ok", resource.Attributes["status"]);
    }

    [Fact]
    public void AgnosticAddAccessControlProviders_MapSubject_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlProviders(o => o.MapSubject<User>(m => m.Id(x => x.Id))));
        Assert.Contains("subject", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AgnosticAddAccessControlProviders_AddSubjectResolver_Throws()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlProviders(o => o.AddSubjectResolver()));
        Assert.Contains("subject", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Doc
    {
        public string Id { get; set; } = "";

        public string Status { get; set; } = "";
    }

    private sealed class User
    {
        public string Id { get; set; } = "";
    }
}
