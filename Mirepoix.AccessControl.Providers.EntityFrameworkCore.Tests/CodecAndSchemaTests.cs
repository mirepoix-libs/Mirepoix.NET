using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

public class CodecTests
{
    [Fact]
    public void Policy_set_round_trips()
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
        Assert.Equal("v1", roundTrip.Version);
        Assert.Equal("p1", roundTrip.Policies[0].Id);
    }

    [Fact]
    public void Attribute_codec_round_trips_primitives()
    {
        Assert.Equal("hello", AttributeValueCodec.FromJson(AttributeValueCodec.ToJson("hello")));
        Assert.Equal(42, Convert.ToInt32(AttributeValueCodec.FromJson(AttributeValueCodec.ToJson(42))));
        Assert.Equal(true, AttributeValueCodec.FromJson(AttributeValueCodec.ToJson(true)));
        Assert.Null(AttributeValueCodec.FromJson(AttributeValueCodec.ToJson(null)));
    }
}

public class SchemaScriptTests
{
    [Fact]
    public void Dialect_map_resolves_known_providers()
    {
        Assert.Equal(
            AccessControlSchemaDialect.SqlServer,
            AccessControlSchemaDialectMap.Resolve(AccessControlSchemaDialectMap.SqlServerProviderName));
        Assert.Equal(
            AccessControlSchemaDialect.PostgreSql,
            AccessControlSchemaDialectMap.Resolve(AccessControlSchemaDialectMap.PostgreSqlProviderName));
    }

    [Fact]
    public void Dialect_map_rejects_unknown_provider()
    {
        Assert.Throws<NotSupportedException>(() =>
            AccessControlSchemaDialectMap.Resolve("Microsoft.EntityFrameworkCore.Sqlite"));
    }

    [Fact]
    public void Embedded_scripts_load_and_split()
    {
        var sqlServer = AccessControlSchemaScripts.LoadSqlServerInit();
        Assert.Contains(AccessControlSchema.PolicySetTable, sqlServer, StringComparison.Ordinal);
        Assert.True(AccessControlSchemaScripts.SplitBatches(sqlServer, AccessControlSchemaDialect.SqlServer).Count() >= 5);

        var postgres = AccessControlSchemaScripts.LoadPostgreSqlInit();
        Assert.Contains(AccessControlSchema.PolicySetTable, postgres, StringComparison.Ordinal);
        Assert.Single(AccessControlSchemaScripts.SplitBatches(postgres, AccessControlSchemaDialect.PostgreSql));
    }
}
