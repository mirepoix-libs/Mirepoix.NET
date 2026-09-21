using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Management.EntityFrameworkCore.Tests;

public sealed class WriterTests
{
    [Fact]
    public async Task Role_catalog_add_replace_list_and_remove_are_durable()
    {
        await using var db = CreateContext();
        var catalog = new EntityFrameworkRoleCatalog(db);

        await catalog.AddAsync(new Role("ADMIN", "first"));
        await catalog.AddAsync(new Role("ADMIN", "updated"));

        var role = Assert.Single(await catalog.ListAsync());
        Assert.Equal(new Role("ADMIN", "updated"), role);

        await catalog.RemoveAsync("ADMIN");
        Assert.Empty(await catalog.ListAsync());
    }

    [Fact]
    public async Task Role_assignment_returns_conflict_without_mutating_then_allows_after_revoke()
    {
        await using var db = CreateContext();
        var constraints = new EntityFrameworkSodConstraintStore(db);
        var assignments = new EntityFrameworkRoleAssignmentStore(db, constraints);
        await constraints.AddAsync(new SodConstraint(
            "finance-sod",
            new HashSet<string> { "REQUESTER", "APPROVER" }));

        Assert.Equal(AssignmentOutcome.Assigned, (await assignments.AssignAsync("alice", "REQUESTER")).Outcome);

        var conflict = await assignments.AssignAsync("alice", "APPROVER");
        Assert.Equal(AssignmentOutcome.SodConflict, conflict.Outcome);
        Assert.Equal("finance-sod", conflict.ConstraintId);
        Assert.Equal(
            new HashSet<string> { "REQUESTER", "APPROVER" },
            conflict.Roles!.ToHashSet());
        Assert.Equal(new HashSet<string> { "REQUESTER" }, await assignments.GetRolesAsync("alice"));

        await assignments.RevokeAsync("alice", "REQUESTER");
        Assert.Equal(AssignmentOutcome.Assigned, (await assignments.AssignAsync("alice", "APPROVER")).Outcome);
    }

    [Fact]
    public async Task Sod_constraint_replace_retains_overlapping_roles_without_tracking_conflict()
    {
        await using var db = CreateContext();
        var constraints = new EntityFrameworkSodConstraintStore(db);
        await constraints.AddAsync(new SodConstraint(
            "finance-sod",
            new HashSet<string> { "REQUESTER", "APPROVER" }));
        var retainedRole = db.Set<SodConstraintRoleEntity>().Local
            .Single(x => x.RoleId == "REQUESTER");

        await constraints.AddAsync(new SodConstraint(
            "finance-sod",
            new HashSet<string> { "REQUESTER", "AUDITOR" }));

        Assert.Same(
            retainedRole,
            db.Set<SodConstraintRoleEntity>().Local.Single(x => x.RoleId == "REQUESTER"));
        var constraint = Assert.Single(await constraints.ListAsync());
        Assert.Equal(
            new HashSet<string> { "REQUESTER", "AUDITOR" },
            constraint.MutuallyExclusiveRoles);
    }

    [Fact]
    public async Task Policy_replace_upserts_singleton_and_round_trips_through_policy_source()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = ContextOptions(databaseName);
        await using (var db = new AccessControlDbContext(options))
        {
            var editor = new EntityFrameworkPolicySetEditor(db);
            await editor.ReplaceAsync(new PolicySet("v1", Array.Empty<Policy.Policy>()));
            await editor.ReplaceAsync(new PolicySet("v2", Array.Empty<Policy.Policy>()));

            var row = Assert.Single(await db.PolicySets.AsNoTracking().ToListAsync());
            Assert.Equal(AccessControlSchema.PolicySetSingletonId, row.Id);
            Assert.Equal("v2", row.Version);
            Assert.Equal("v2", PolicySetStorageCodec.FromStorageJson(row.PayloadJson).Version);
            Assert.NotEqual(default, row.UpdatedUtc);
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => new AccessControlDbContext(options));
        services.AddSingleton(new EntityFrameworkProviderOptions
        {
            ContextType = typeof(AccessControlDbContext),
            PolicyCacheTtl = TimeSpan.Zero,
        });
        services.AddSingleton<EntityFrameworkPolicySource>();
        await using var provider = services.BuildServiceProvider();

        var loaded = await provider.GetRequiredService<EntityFrameworkPolicySource>()
            .GetPolicySetAsync(CancellationToken.None);
        Assert.Equal("v2", loaded.Version);
    }

    [Fact]
    public async Task Subject_labels_throw_when_subject_layout_is_mapped()
    {
        var providerOptions = new EntityFrameworkProviderOptions();
        providerOptions.MapSubject<MappedUser>(map => map.Id(user => user.Id));
        await using var db = CreateContext(providerOptions);
        var labels = new EntityFrameworkLabelHelper(db, providerOptions);

        var set = await Assert.ThrowsAsync<InvalidOperationException>(
            () => labels.SetSubjectLabelAsync("alice", "department", "finance"));
        var clear = await Assert.ThrowsAsync<InvalidOperationException>(
            () => labels.ClearSubjectLabelAsync("alice", "department"));

        const string message =
            "Subject labels are not managed by AccessControl when subject entity maps are configured; the application owns mapped subject storage.";
        Assert.Equal(message, set.Message);
        Assert.Equal(message, clear.Message);
    }

    [Fact]
    public async Task Ownership_and_labels_upsert_and_clear_encoded_attributes()
    {
        await using var db = CreateContext();
        var ownership = new EntityFrameworkOwnershipHelper(db);
        var labels = new EntityFrameworkLabelHelper(db, new EntityFrameworkProviderOptions());

        await ownership.SetOwnerAsync("invoice", "42", "alice");
        await labels.SetResourceLabelAsync("invoice", "42", "region", "west");
        await labels.SetSubjectLabelAsync("alice", "level", 7);

        var owner = await db.ResourceAttributes.SingleAsync(x => x.Name == "ownerId");
        Assert.Equal("alice", AttributeValueCodec.FromJson(owner.ValueJson));
        Assert.Equal(2, await db.ResourceAttributes.CountAsync());
        Assert.Equal(
            7,
            Convert.ToInt32(AttributeValueCodec.FromJson(
                (await db.SubjectAttributes.SingleAsync()).ValueJson)));

        await ownership.ClearOwnerAsync("invoice", "42");
        await labels.ClearResourceLabelAsync("invoice", "42", "region");
        await labels.ClearSubjectLabelAsync("alice", "level");
        Assert.Empty(await db.ResourceAttributes.ToListAsync());
        Assert.Empty(await db.SubjectAttributes.ToListAsync());
    }

    private static AccessControlDbContext CreateContext(
        EntityFrameworkProviderOptions? providerOptions = null) =>
        new(ContextOptions(Guid.NewGuid().ToString()), providerOptions);

    private static DbContextOptions<AccessControlDbContext> ContextOptions(string databaseName) =>
        new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

    private sealed class MappedUser
    {
        public string Id { get; set; } = "";
    }
}
