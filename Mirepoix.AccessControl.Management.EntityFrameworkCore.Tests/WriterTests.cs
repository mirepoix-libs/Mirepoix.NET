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
    public async Task Subject_store_returns_conflict_without_mutating_then_allows_after_revoke()
    {
        await using var db = CreateContext();
        var constraints = new EntityFrameworkSodConstraintStore(db);
        var subjects = new EntityFrameworkSubjectStore(db, constraints, SubjectStorageLayout.Native);
        await constraints.AddAsync(new SodConstraint(
            "finance-sod",
            new HashSet<string> { "REQUESTER", "APPROVER" }));

        Assert.Equal(AssignmentOutcome.Assigned, (await subjects.AssignRoleAsync("alice", "REQUESTER")).Outcome);

        var conflict = await subjects.AssignRoleAsync("alice", "APPROVER");
        Assert.Equal(AssignmentOutcome.SodConflict, conflict.Outcome);
        Assert.Equal("finance-sod", conflict.ConstraintId);
        Assert.Equal(
            new HashSet<string> { "REQUESTER", "APPROVER" },
            conflict.Roles!.ToHashSet());
        Assert.Equal(new HashSet<string> { "REQUESTER" }, await subjects.GetRolesAsync("alice"));

        await subjects.RevokeRoleAsync("alice", "REQUESTER");
        Assert.Equal(AssignmentOutcome.Assigned, (await subjects.AssignRoleAsync("alice", "APPROVER")).Outcome);
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
    public async Task Mapped_library_roles_reject_header_and_attributes_but_support_roles()
    {
        var providerOptions = new EntityFrameworkProviderOptions();
        providerOptions.MapSubject<MappedUser>(map => map.Id(user => user.Id));
        await using var db = CreateContext(providerOptions);
        var constraints = new EntityFrameworkSodConstraintStore(db);
        var subjects = new EntityFrameworkSubjectStore(
            db,
            constraints,
            SubjectStorageLayout.MappedLibraryRoles);

        var create = await Assert.ThrowsAsync<InvalidOperationException>(
            () => subjects.CreateAsync("alice"));
        var set = await Assert.ThrowsAsync<InvalidOperationException>(
            () => subjects.SetAttributeAsync("alice", "department", "finance"));

        const string message =
            "Subject headers and attributes are not managed by AccessControl for MappedLibraryRoles; the application owns that storage. Register a custom ISubjectStore or use Native layout.";
        Assert.Equal(message, create.Message);
        Assert.Equal(message, set.Message);
        Assert.Equal(
            AssignmentOutcome.Assigned,
            (await subjects.AssignRoleAsync("alice", "EDITOR")).Outcome);
        Assert.Equal(new HashSet<string> { "EDITOR" }, await subjects.GetRolesAsync("alice"));
    }

    [Fact]
    public async Task Native_subject_store_manages_headers_attributes_and_delete_cleanup()
    {
        await using var db = CreateContext();
        var constraints = new EntityFrameworkSodConstraintStore(db);
        var subjects = new EntityFrameworkSubjectStore(db, constraints, SubjectStorageLayout.Native);

        await subjects.CreateAsync("alice");
        await subjects.SetAttributeAsync("alice", "level", 7);
        await subjects.AssignRoleAsync("alice", "EDITOR");

        Assert.True(await subjects.ExistsAsync("alice"));
        Assert.Equal(new[] { "alice" }, await subjects.ListIdsAsync());
        Assert.Equal(7, Convert.ToInt32((await subjects.GetAttributesAsync("alice"))["level"]));

        await subjects.DeleteAsync("alice");

        Assert.False(await subjects.ExistsAsync("alice"));
        Assert.Empty(await db.SubjectAttributes.ToListAsync());
        Assert.Empty(await db.SubjectRoles.ToListAsync());
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
