using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Providers;

public class CompositeBundleHydratorTests
{
    [Fact]
    public async Task Composite_hydrator_fills_subject_from_resolver()
    {
        var subjects = new InMemorySubjectResolver(new Dictionary<string, Subject>
        {
            ["u1"] = new Subject("u1", new HashSet<string> { "EDITOR" }, new Dictionary<string, object?>())
        });
        var hydrator = new CompositeBundleHydrator(subjects, null, null);
        var request = new AuthorizationRequest(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:edit"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
        var bundle = await hydrator.HydrateAsync(request, CancellationToken.None);
        Assert.Contains("EDITOR", bundle.Subject.Roles);
    }

    [Fact]
    public async Task Composite_hydrator_passes_through_when_resolvers_omitted()
    {
        var hydrator = new CompositeBundleHydrator();
        var request = Request(
            new Subject("u1", new HashSet<string> { "VIEWER" }, new Dictionary<string, object?> { ["dept"] = "ops" }),
            new Resource("doc", "1", new Dictionary<string, object?> { ["owner"] = "u1" }),
            new AccessContext(null, new Dictionary<string, object?> { ["aud"] = "app" }, new Dictionary<string, object?>()));

        var bundle = await hydrator.HydrateAsync(request, CancellationToken.None);

        Assert.Equal(request.Subject, bundle.Subject);
        Assert.Equal(request.Resource, bundle.Resource);
        Assert.Equal(request.Context, bundle.Context);
        Assert.Equal(request.Operation.Value, bundle.Operation.Value);
    }

    [Fact]
    public async Task Composite_hydrator_fills_resource_from_resolver()
    {
        var resources = new TestResourceHydrator(new Dictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>>
        {
            [("doc", "1")] = new Dictionary<string, object?> { ["owner"] = "u1" }
        });
        var hydrator = new CompositeBundleHydrator(null, resources, null);
        var request = Request(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()));

        var bundle = await hydrator.HydrateAsync(request, CancellationToken.None);

        Assert.Equal("u1", bundle.Resource.Attributes["owner"]);
        Assert.Equal("doc", bundle.Resource.Type);
        Assert.Equal("1", bundle.Resource.Id);
    }

    [Fact]
    public async Task Composite_hydrator_fills_context_from_resolver()
    {
        var context = new PassThroughContextResolver();
        var hydrator = new CompositeBundleHydrator(null, null, context);
        var request = Request(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            new AccessContext(
                DateTimeOffset.Parse("2026-09-10T00:00:00Z"),
                new Dictionary<string, object?> { ["aud"] = "app" },
                new Dictionary<string, object?> { ["ip"] = "1.1.1.1" }));

        var bundle = await hydrator.HydrateAsync(request, CancellationToken.None);

        Assert.Equal(request.Context, bundle.Context);
    }

    [Fact]
    public async Task InMemorySubjectResolver_missing_id_throws_KeyNotFoundException()
    {
        var subjects = new InMemorySubjectResolver(new Dictionary<string, Subject>());
        var hydrator = new CompositeBundleHydrator(subjects, null, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => hydrator.HydrateAsync(Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Resource_hydrator_missing_key_throws_KeyNotFoundException()
    {
        var resources = new TestResourceHydrator(
            new Dictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>>());
        var hydrator = new CompositeBundleHydrator(null, resources, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => hydrator.HydrateAsync(Request(), CancellationToken.None));
    }

    private static AuthorizationRequest Request(
        Subject? subject = null,
        Resource? resource = null,
        AccessContext? context = null) =>
        new(
            subject ?? new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            resource ?? new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:edit"),
            context ?? new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
}
