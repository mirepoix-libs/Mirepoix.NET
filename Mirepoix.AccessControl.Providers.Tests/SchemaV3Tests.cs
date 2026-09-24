using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.Tests;

public class SubjectStorageLayoutResolverTests
{
    private sealed class User
    {
        public string Id { get; set; } = "";

        public string Role { get; set; } = "";
    }

    [Fact]
    public void No_maps_use_native_subject_storage()
    {
        var mapping = new SubjectMappingOptions();

        var layout = SubjectStorageLayoutResolver.Resolve(mapping);

        Assert.Equal(SubjectStorageLayout.Native, layout);
    }

    [Fact]
    public void Maps_without_role_members_use_library_role_storage()
    {
        var mapping = new SubjectMappingOptions()
            .MapEntity<User>(map => map.Id(user => user.Id));

        var layout = SubjectStorageLayoutResolver.Resolve(mapping);

        Assert.Equal(SubjectStorageLayout.MappedLibraryRoles, layout);
    }

    [Fact]
    public void Type_as_role_does_not_select_app_owned_role_storage()
    {
        var mapping = new SubjectMappingOptions()
            .MapEntity<User>(map => map.Id(user => user.Id).Type("user").TypeAsRole());

        var layout = SubjectStorageLayoutResolver.Resolve(mapping);

        Assert.Equal(SubjectStorageLayout.MappedLibraryRoles, layout);
    }

    [Fact]
    public void Any_map_with_role_members_uses_app_owned_role_storage()
    {
        var mapping = new SubjectMappingOptions()
            .MapEntity<User>(map => map.Id(user => user.Id))
            .MapEntity<User>(map => map.Id(user => user.Id).Roles(user => user.Role));

        var layout = SubjectStorageLayoutResolver.Resolve(mapping);

        Assert.Equal(SubjectStorageLayout.MappedAppOwnedRoles, layout);
    }

    [Fact]
    public void Null_mapping_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => SubjectStorageLayoutResolver.Resolve(null!));
    }
}

public class SchemaV3Tests
{
    [Fact]
    public void Schema_contract_reports_version_three()
    {
        Assert.Equal(3, AccessControlSchema.SchemaVersion);
    }

    [Theory]
    [InlineData(AccessControlSchemaDialect.SqlServer)]
    [InlineData(AccessControlSchemaDialect.PostgreSql)]
    public void Ordered_schema_resources_include_resource_attribute_drop_last(
        AccessControlSchemaDialect dialect)
    {
        var resourceNames = AccessControlSchemaDialectMap.ResourceNames(dialect);
        var scripts = AccessControlSchemaScripts.Load(dialect);

        Assert.Equal(5, resourceNames.Count);
        Assert.Equal(5, scripts.Count);
        Assert.EndsWith("001_init.sql", resourceNames[0], StringComparison.Ordinal);
        Assert.EndsWith("001b_subject_roles.sql", resourceNames[1], StringComparison.Ordinal);
        Assert.EndsWith("002_management.sql", resourceNames[2], StringComparison.Ordinal);
        Assert.EndsWith("002b_subjects.sql", resourceNames[3], StringComparison.Ordinal);
        Assert.EndsWith("003_drop_resource_attribute.sql", resourceNames[4], StringComparison.Ordinal);
        Assert.All(scripts, script => Assert.False(string.IsNullOrWhiteSpace(script)));
        Assert.Contains(AccessControlSchema.PolicySetTable, scripts[0], StringComparison.Ordinal);
        Assert.DoesNotContain("ac_resource_attribute", scripts[0], StringComparison.Ordinal);
        Assert.Contains(AccessControlSchema.SubjectRoleTable, scripts[1], StringComparison.Ordinal);
        Assert.Contains(AccessControlSchema.RoleTable, scripts[2], StringComparison.Ordinal);
        Assert.Contains(AccessControlSchema.SodConstraintTable, scripts[2], StringComparison.Ordinal);
        Assert.Contains(AccessControlSchema.SubjectTable, scripts[3], StringComparison.Ordinal);
        Assert.Contains(AccessControlSchema.SubjectAttributeTable, scripts[3], StringComparison.Ordinal);
        Assert.Contains("ac_resource_attribute", scripts[4], StringComparison.Ordinal);
        Assert.Contains("DROP", scripts[4], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(AccessControlSchemaDialect.SqlServer)]
    [InlineData(AccessControlSchemaDialect.PostgreSql)]
    public void Every_layout_includes_resource_attribute_drop(
        AccessControlSchemaDialect dialect)
    {
        foreach (var layout in Enum.GetValues<SubjectStorageLayout>())
        {
            Assert.EndsWith(
                "003_drop_resource_attribute.sql",
                AccessControlSchemaDialectMap.ResourceNames(dialect, layout)[^1],
                StringComparison.Ordinal);
        }
    }
}
