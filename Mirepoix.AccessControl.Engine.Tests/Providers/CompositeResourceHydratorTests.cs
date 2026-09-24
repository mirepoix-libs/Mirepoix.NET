using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Providers;

public class CompositeResourceHydratorTests
{
    [Fact]
    public async Task Unknown_type_throws()
    {
        var hydrator = new CompositeResourceHydrator(
            new EmptyServiceProvider(),
            Array.Empty<ResourceResolverRegistration>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => hydrator.HydrateAsync(Resource("doc", "1"), CancellationToken.None));
    }

    [Fact]
    public async Task Empty_type_and_id_passthrough()
    {
        var hydrator = new CompositeResourceHydrator(
            new EmptyServiceProvider(),
            Array.Empty<ResourceResolverRegistration>());
        var partial = Resource("", "");

        var result = await hydrator.HydrateAsync(partial, CancellationToken.None);

        Assert.Same(partial, result);
    }

    [Fact]
    public async Task Empty_type_nonempty_id_throws()
    {
        var hydrator = new CompositeResourceHydrator(
            new EmptyServiceProvider(),
            Array.Empty<ResourceResolverRegistration>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => hydrator.HydrateAsync(Resource("", "1"), CancellationToken.None));
    }

    [Fact]
    public async Task Matching_type_dispatches_with_service_provider_partial_and_token()
    {
        var serviceProvider = new EmptyServiceProvider();
        var partial = Resource("doc", "1");
        using var cancellationTokenSource = new CancellationTokenSource();
        var expected = Resource("doc", "1", ("ownerId", "u1"));
        IServiceProvider? receivedServiceProvider = null;
        Resource? receivedPartial = null;
        CancellationToken receivedToken = default;
        var registration = new ResourceResolverRegistration
        {
            Type = "doc",
            ResolverServiceType = typeof(TestResourceResolver),
            Invoke = (sp, resource, cancellationToken) =>
            {
                receivedServiceProvider = sp;
                receivedPartial = resource;
                receivedToken = cancellationToken;
                return Task.FromResult(expected);
            },
        };
        var hydrator = new CompositeResourceHydrator(serviceProvider, new[] { registration });

        var result = await hydrator.HydrateAsync(partial, cancellationTokenSource.Token);

        Assert.Same(expected, result);
        Assert.Same(serviceProvider, receivedServiceProvider);
        Assert.Same(partial, receivedPartial);
        Assert.Equal(cancellationTokenSource.Token, receivedToken);
    }

    [Fact]
    public async Task Type_matching_is_ordinal_and_case_sensitive()
    {
        var registration = new ResourceResolverRegistration
        {
            Type = "doc",
            ResolverServiceType = typeof(TestResourceResolver),
            Invoke = (_, resource, _) => Task.FromResult(resource),
        };
        var hydrator = new CompositeResourceHydrator(
            new EmptyServiceProvider(),
            new[] { registration });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => hydrator.HydrateAsync(Resource("DOC", "1"), CancellationToken.None));
    }

    [Fact]
    public void Duplicate_type_throws()
    {
        var registrations = new[]
        {
            Registration("doc"),
            Registration("doc"),
        };

        Assert.Throws<InvalidOperationException>(
            () => new CompositeResourceHydrator(new EmptyServiceProvider(), registrations));
    }

    private static ResourceResolverRegistration Registration(string type) =>
        new()
        {
            Type = type,
            ResolverServiceType = typeof(TestResourceResolver),
            Invoke = (_, resource, _) => Task.FromResult(resource),
        };

    private static Resource Resource(
        string type,
        string id,
        params (string Name, object? Value)[] attributes) =>
        new(
            type,
            id,
            attributes.ToDictionary(attribute => attribute.Name, attribute => attribute.Value));

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class TestResourceResolver;
}
