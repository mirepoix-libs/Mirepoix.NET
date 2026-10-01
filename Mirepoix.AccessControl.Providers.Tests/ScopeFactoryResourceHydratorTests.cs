using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl;

namespace Mirepoix.AccessControl.Providers.Tests;

public class ScopeFactoryResourceHydratorTests
{
    [Fact]
    public async Task Empty_resource_passes_through_when_no_resolver_is_registered()
    {
        var hydrator = new ScopeFactoryResourceHydrator(EmptyScopeFactory());
        var partial = new Resource("", "", new Dictionary<string, object?> { ["ownerId"] = "ada" });

        var result = await hydrator.HydrateAsync(partial, CancellationToken.None);

        Assert.Same(partial, result);
    }

    [Fact]
    public async Task Typed_resource_throws_when_no_resolver_is_registered()
    {
        var hydrator = new ScopeFactoryResourceHydrator(EmptyScopeFactory());
        var partial = new Resource("document", "1", new Dictionary<string, object?> { ["ownerId"] = "ada" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hydrator.HydrateAsync(partial, CancellationToken.None));

        Assert.Contains("document", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Id_without_type_throws_when_no_resolver_is_registered()
    {
        var hydrator = new ScopeFactoryResourceHydrator(EmptyScopeFactory());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            hydrator.HydrateAsync(
                new Resource("", "1", new Dictionary<string, object?>()),
                CancellationToken.None));

        Assert.Equal("A resource id requires a resource type.", exception.Message);
    }

    private static IServiceScopeFactory EmptyScopeFactory()
    {
        var services = new ServiceCollection();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
