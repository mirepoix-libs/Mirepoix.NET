using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

public class ModelConfigurationTests
{
    private sealed class MappedUser
    {
        public string Id { get; set; } = "";
        public string Role { get; set; } = "";
    }

    [Fact]
    public void AccessControlDbContext_maps_ac_tables()
    {
        var options = new DbContextOptionsBuilder<AccessControlDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new AccessControlDbContext(options);
        var entityTypes = db.Model.GetEntityTypes().ToDictionary(e => e.GetTableName()!);

        Assert.Contains("ac_policy_set", entityTypes.Keys);
        Assert.Contains("ac_subject", entityTypes.Keys);
        Assert.Contains("ac_subject_role", entityTypes.Keys);
        Assert.Contains("ac_subject_attribute", entityTypes.Keys);
        Assert.DoesNotContain("ac_resource_attribute", entityTypes.Keys);
        Assert.Contains("ac_role", entityTypes.Keys);
        Assert.Contains("ac_sod_constraint", entityTypes.Keys);
        Assert.Contains("ac_sod_constraint_role", entityTypes.Keys);

        var policy = entityTypes["ac_policy_set"];
        Assert.Contains(policy.GetProperties(), p => p.GetColumnName() == "payload_json");

        var subjectRole = entityTypes["ac_subject_role"];
        Assert.Empty(subjectRole.GetForeignKeys());
    }

    [Fact]
    public void ApplyAccessControl_works_on_app_context()
    {
        var options = new DbContextOptionsBuilder<AppOwnedDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new AppOwnedDbContext(options);
        Assert.NotNull(db.Model.FindEntityType(typeof(PolicySetEntity)));
    }

    [Fact]
    public void ApplyAccessControl_mapped_library_roles_maps_only_subject_roles()
    {
        var options = new DbContextOptionsBuilder<MappedLibraryRolesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new MappedLibraryRolesDbContext(options);

        Assert.Null(db.Model.FindEntityType(typeof(SubjectEntity)));
        Assert.Null(db.Model.FindEntityType(typeof(SubjectAttributeEntity)));
        Assert.NotNull(db.Model.FindEntityType(typeof(SubjectRoleEntity)));
    }

    [Fact]
    public void ApplyAccessControl_mapped_app_owned_roles_omits_subject_entities()
    {
        var options = new DbContextOptionsBuilder<MappedAppOwnedRolesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new MappedAppOwnedRolesDbContext(options);

        Assert.Null(db.Model.FindEntityType(typeof(SubjectEntity)));
        Assert.Null(db.Model.FindEntityType(typeof(SubjectAttributeEntity)));
        Assert.Null(db.Model.FindEntityType(typeof(SubjectRoleEntity)));
    }

    [Theory]
    [InlineData(SubjectStorageLayout.Native, 5)]
    [InlineData(SubjectStorageLayout.MappedLibraryRoles, 4)]
    [InlineData(SubjectStorageLayout.MappedAppOwnedRoles, 3)]
    public void Schema_applier_selects_ordered_scripts_for_layout(
        SubjectStorageLayout layout,
        int expectedCount)
    {
        var resources = AccessControlSchemaApplier.ResourceNames(
            AccessControlSchemaDialect.SqlServer,
            layout);

        Assert.Equal(expectedCount, resources.Count);
        Assert.EndsWith("001_init.sql", resources[0], StringComparison.Ordinal);
        var managementIndex = layout == SubjectStorageLayout.MappedAppOwnedRoles ? 1 : 2;
        Assert.EndsWith("002_management.sql", resources[managementIndex], StringComparison.Ordinal);
        if (layout != SubjectStorageLayout.MappedAppOwnedRoles)
            Assert.EndsWith("001b_subject_roles.sql", resources[1], StringComparison.Ordinal);
        if (layout == SubjectStorageLayout.Native)
            Assert.EndsWith("002b_subjects.sql", resources[3], StringComparison.Ordinal);
        Assert.EndsWith("003_drop_resource_attribute.sql", resources[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void Mapped_app_owned_roles_selected_ddl_omits_subject_role_table()
    {
        var resources = AccessControlSchemaApplier.ResourceNames(
            AccessControlSchemaDialect.SqlServer,
            SubjectStorageLayout.MappedAppOwnedRoles);
        var selectedDdl = string.Join(
            Environment.NewLine,
            resources.Select(AccessControlSchemaScripts.Load));

        Assert.DoesNotContain(
            $"CREATE TABLE dbo.{AccessControlSchema.SubjectRoleTable}",
            selectedDdl,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Package_context_with_mapped_library_roles_maps_only_subject_roles()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(options =>
        {
            options.ConfigureDb = db => db.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
            options.MapSubject<MappedUser>(map => map.Id(user => user.Id));
        });

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();

        Assert.Null(db.Model.FindEntityType(typeof(SubjectEntity)));
        Assert.Null(db.Model.FindEntityType(typeof(SubjectAttributeEntity)));
        Assert.NotNull(db.Model.FindEntityType(typeof(SubjectRoleEntity)));
    }

    [Fact]
    public async Task Package_context_with_mapped_app_owned_roles_omits_subject_entities()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(options =>
        {
            options.ConfigureDb = db => db.UseInMemoryDatabase(Guid.NewGuid().ToString("N"));
            options.MapSubject<MappedUser>(map => map
                .Id(user => user.Id)
                .Roles(user => user.Role));
        });

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();

        Assert.Null(db.Model.FindEntityType(typeof(SubjectEntity)));
        Assert.Null(db.Model.FindEntityType(typeof(SubjectAttributeEntity)));
        Assert.Null(db.Model.FindEntityType(typeof(SubjectRoleEntity)));
    }
}

public sealed class AppOwnedDbContext : DbContext
{
    public AppOwnedDbContext(DbContextOptions<AppOwnedDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAccessControl();
    }
}

public sealed class MappedLibraryRolesDbContext : DbContext
{
    public MappedLibraryRolesDbContext(DbContextOptions<MappedLibraryRolesDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAccessControl(SubjectStorageLayout.MappedLibraryRoles);
    }
}

public sealed class MappedAppOwnedRolesDbContext : DbContext
{
    public MappedAppOwnedRolesDbContext(DbContextOptions<MappedAppOwnedRolesDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyAccessControl(SubjectStorageLayout.MappedAppOwnedRoles);
    }
}
