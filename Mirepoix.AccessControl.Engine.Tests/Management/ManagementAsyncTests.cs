using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class ManagementAsyncTests
{
    [Fact]
    public async Task AssignAsync_conflicting_roles_returns_SodConflict_and_writes_nothing()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        await mgr.SodConstraints.AddAsync(new SodConstraint("sod1", new HashSet<string> { "REQUESTOR", "APPROVER" }));
        await mgr.RoleCatalog.AddAsync(new Role("REQUESTOR", null));
        await mgr.RoleCatalog.AddAsync(new Role("APPROVER", null));
        Assert.Equal(AssignmentOutcome.Assigned, (await mgr.Assignments.AssignAsync("u1", "REQUESTOR")).Outcome);
        var second = await mgr.Assignments.AssignAsync("u1", "APPROVER");
        Assert.Equal(AssignmentOutcome.SodConflict, second.Outcome);
        Assert.Equal("sod1", second.ConstraintId);
        Assert.Equal(new HashSet<string> { "REQUESTOR", "APPROVER" }, second.Roles!.ToHashSet());
        var roles = await mgr.Assignments.GetRolesAsync("u1");
        Assert.DoesNotContain("APPROVER", roles);
    }

    [Fact]
    public async Task ReplaceAsync_swaps_active_policy_set_on_source()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        var set = new PolicySet("v2", Array.Empty<Policy>());

        await mgr.Policies.ReplaceAsync(set);

        var loaded = await mgr.PolicySource.GetPolicySetAsync(CancellationToken.None);
        Assert.Same(set, loaded);
        Assert.Equal("v2", loaded.Version);
    }

    [Fact]
    public async Task RoleCatalog_async_round_trip()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        var role = new Role("EDITOR", null);
        await mgr.RoleCatalog.AddAsync(role);
        var listed = await mgr.RoleCatalog.ListAsync();
        Assert.Single(listed);
        Assert.Equal("EDITOR", listed[0].Id);
        await mgr.RoleCatalog.RemoveAsync("EDITOR");
        Assert.Empty(await mgr.RoleCatalog.ListAsync());
    }

    [Fact]
    public async Task SodConstraintStore_async_round_trip()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        var constraint = new SodConstraint("sod1", new HashSet<string> { "A", "B" });
        await mgr.SodConstraints.AddAsync(constraint);
        var listed = await mgr.SodConstraints.ListAsync();
        Assert.Single(listed);
        Assert.Equal("sod1", listed[0].Id);
        await mgr.SodConstraints.RemoveAsync("sod1");
        Assert.Empty(await mgr.SodConstraints.ListAsync());
    }

    [Fact]
    public async Task AssignAsync_is_visible_to_subject_resolver()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        Assert.Equal(AssignmentOutcome.Assigned, (await mgr.Assignments.AssignAsync("u1", "EDITOR")).Outcome);

        var subject = await mgr.SubjectResolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Contains("EDITOR", subject.Roles);
    }

    [Fact]
    public async Task RevokeAsync_removes_role_from_assignment_store()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        await mgr.Assignments.AssignAsync("u1", "EDITOR");
        await mgr.Assignments.AssignAsync("u1", "READER");
        await mgr.Assignments.RevokeAsync("u1", "EDITOR");
        var roles = await mgr.Assignments.GetRolesAsync("u1");
        Assert.DoesNotContain("EDITOR", roles);
        Assert.Contains("READER", roles);
    }

    [Fact]
    public async Task SetOwnerAsync_is_visible_to_resource_resolver()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        await mgr.Ownership.SetOwnerAsync("doc", "1", "u1");

        var resource = await mgr.ResourceResolver.HydrateAsync(
            new Resource("doc", "1", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("u1", resource.Attributes["ownerId"]);
    }

    [Fact]
    public async Task ClearOwnerAsync_removes_ownerId()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        await mgr.Ownership.SetOwnerAsync("doc", "1", "u1");
        await mgr.Ownership.ClearOwnerAsync("doc", "1");

        var resource = await mgr.ResourceResolver.HydrateAsync(
            new Resource("doc", "1", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.False(resource.Attributes.ContainsKey("ownerId"));
    }

    [Fact]
    public async Task LabelHelper_async_round_trip()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        await mgr.Labels.SetSubjectLabelAsync("u1", "clearance", "secret");
        await mgr.Labels.SetResourceLabelAsync("doc", "1", "tier", "gold");

        var subject = await mgr.SubjectResolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);
        var resource = await mgr.ResourceResolver.HydrateAsync(
            new Resource("doc", "1", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.Equal("secret", subject.Attributes["clearance"]);
        Assert.Equal("gold", resource.Attributes["tier"]);

        await mgr.Labels.ClearSubjectLabelAsync("u1", "clearance");
        await mgr.Labels.ClearResourceLabelAsync("doc", "1", "tier");

        subject = await mgr.SubjectResolver.HydrateAsync(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);
        resource = await mgr.ResourceResolver.HydrateAsync(
            new Resource("doc", "1", new Dictionary<string, object?>()),
            CancellationToken.None);

        Assert.False(subject.Attributes.ContainsKey("clearance"));
        Assert.False(resource.Attributes.ContainsKey("tier"));
    }
}
