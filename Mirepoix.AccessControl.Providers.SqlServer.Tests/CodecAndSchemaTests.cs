using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.SqlServer.Tests;

public class PolicySetStorageCodecTests
{
    [Fact]
    public void Round_trips_policy_set_json()
    {
        var set = new PolicySet(
            "v1",
            new[]
            {
                new AccessPolicy(
                    "p1",
                    AuthorizationResult.Allow,
                    "admins",
                    new IAtom[] { new RoleMembershipAtom(new[] { "ADMIN" }) }),
            });

        var json = PolicySetStorageCodec.ToStorageJson(set);
        var roundTrip = PolicySetStorageCodec.FromStorageJson(json);

        Assert.Equal(set.Version, roundTrip.Version);
        Assert.Single(roundTrip.Policies);
        Assert.Equal("p1", roundTrip.Policies[0].Id);
    }

    [Fact]
    public void Attribute_value_codec_round_trips_primitives()
    {
        Assert.Equal("hello", AttributeValueCodec.FromJson(AttributeValueCodec.ToJson("hello")));
        Assert.Equal(42, Convert.ToInt32(AttributeValueCodec.FromJson(AttributeValueCodec.ToJson(42))));
        Assert.Equal(true, AttributeValueCodec.FromJson(AttributeValueCodec.ToJson(true)));
        Assert.Null(AttributeValueCodec.FromJson(AttributeValueCodec.ToJson(null)));
    }
}

public class SchemaScriptTests
{
    public static TheoryData<SubjectStorageLayout, string[]> LayoutScripts => new()
    {
        {
            SubjectStorageLayout.Native,
            [
                AccessControlSchemaDialectMap.SqlServerCoreResource,
                AccessControlSchemaDialectMap.SqlServerSubjectRolesResource,
                AccessControlSchemaDialectMap.SqlServerManagementResource,
                AccessControlSchemaDialectMap.SqlServerSubjectsResource,
            ]
        },
        {
            SubjectStorageLayout.MappedLibraryRoles,
            [
                AccessControlSchemaDialectMap.SqlServerCoreResource,
                AccessControlSchemaDialectMap.SqlServerSubjectRolesResource,
                AccessControlSchemaDialectMap.SqlServerManagementResource,
            ]
        },
        {
            SubjectStorageLayout.MappedAppOwnedRoles,
            [
                AccessControlSchemaDialectMap.SqlServerCoreResource,
                AccessControlSchemaDialectMap.SqlServerManagementResource,
            ]
        },
    };

    [Fact]
    public void Embedded_init_script_loads_and_splits()
    {
        var script = AccessControlSchemaScripts.LoadSqlServerInit();
        Assert.Contains(AccessControlSchema.PolicySetTable, script, StringComparison.Ordinal);
        Assert.Contains(AccessControlSchema.SubjectTable, script, StringComparison.Ordinal);

        var batches = AccessControlSchemaScripts.SplitBatches(script, AccessControlSchemaDialect.SqlServer).ToList();
        Assert.True(batches.Count >= 5);
    }

    [Theory]
    [MemberData(nameof(LayoutScripts))]
    public void Migrator_selects_ordered_scripts_for_layout(
        SubjectStorageLayout layout,
        string[] expected)
    {
        Assert.Equal(expected, SqlServerSchemaMigrator.ResourceNames(layout));
    }
}
