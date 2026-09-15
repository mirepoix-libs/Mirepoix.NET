using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Providers;

public class OwnershipHelperTests
{
    [Fact]
    public async Task SetOwner_is_visible_to_resource_resolver_used_by_hydrator()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.Ownership.SetOwner("doc", "1", "u1");

        var hydrator = new CompositeBundleHydrator(null, mgr.ResourceResolver, null);
        var request = new AuthorizationRequest(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

        var bundle = await hydrator.HydrateAsync(request, CancellationToken.None);

        Assert.Equal("u1", bundle.Resource.Attributes["ownerId"]);
    }

    [Fact]
    public async Task SetSubjectLabel_is_visible_to_subject_resolver()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.Labels.SetSubjectLabel("u1", "clearance", "secret");

        var subject = await mgr.SubjectResolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("secret", subject.Attributes["clearance"]);
    }

    [Fact]
    public async Task SetResourceLabel_is_visible_to_resource_resolver()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.Labels.SetResourceLabel("doc", "1", "tier", "gold");

        var resource = await mgr.ResourceResolver.HydrateAsync(
            new Resource("doc", "1", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("gold", resource.Attributes["tier"]);
    }

    [Fact]
    public async Task ClearOwner_removes_ownerId_and_keeps_other_attributes()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.Ownership.SetOwner("doc", "1", "u1");
        mgr.Labels.SetResourceLabel("doc", "1", "status", "draft");

        mgr.Ownership.ClearOwner("doc", "1");

        var resource = await mgr.ResourceResolver.HydrateAsync(
            new Resource("doc", "1", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.False(resource.Attributes.ContainsKey("ownerId"));
        Assert.Equal("draft", resource.Attributes["status"]);
    }

    [Fact]
    public void ClearOwner_unknown_resource_does_not_throw()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.Ownership.ClearOwner("doc", "missing");
    }

    [Fact]
    public void ClearOwner_copies_non_dictionary_map_then_removes_ownerId()
    {
        IReadOnlyDictionary<string, object?> frozen = new System.Collections.ObjectModel.ReadOnlyDictionary<string, object?>(
            new Dictionary<string, object?>
            {
                ["ownerId"] = "u1",
                ["status"] = "draft",
            });
        var resources = new Dictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>>
        {
            [("doc", "1")] = frozen,
        };

        var helper = new InMemoryOwnershipHelper(resources);
        helper.ClearOwner("doc", "1");

        Assert.False(resources[("doc", "1")].ContainsKey("ownerId"));
        Assert.Equal("draft", resources[("doc", "1")]["status"]);
        Assert.NotSame(frozen, resources[("doc", "1")]);
    }
}
