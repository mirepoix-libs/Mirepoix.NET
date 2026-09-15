using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.Data.SqlClient;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.SqlServer.Tests;

public class SqlServerIntegrationTests
{
    private static readonly string? ConnectionString = ResolveConnectionString();

    public static bool SqlAvailable => ConnectionString is not null;

    [Fact]
    public async Task Migrator_policy_source_and_resolvers_round_trip()
    {
        if (!SqlAvailable)
            return; // LocalDB / env not available — pure unit tests still cover codec

        var options = new SqlServerProviderOptions { ConnectionString = ConnectionString! };
        var migrator = new SqlServerSchemaMigrator(options);
        await migrator.ApplyAsync();

        await using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await WipeDataAsync(connection);

            var set = new PolicySet(
                "v-int",
                new[]
                {
                    new AccessPolicy(
                        "allow-editor",
                        AuthorizationResult.Allow,
                        null,
                        new IAtom[] { new RoleMembershipAtom(new[] { "EDITOR" }) }),
                });

            await using (var insertPolicy = connection.CreateCommand())
            {
                insertPolicy.CommandText =
                    $"""
                     INSERT INTO dbo.{AccessControlSchema.PolicySetTable} (id, version, payload_json, updated_utc)
                     VALUES (@id, @version, @json, SYSUTCDATETIME())
                     """;
                insertPolicy.Parameters.AddWithValue("@id", AccessControlSchema.PolicySetSingletonId);
                insertPolicy.Parameters.AddWithValue("@version", set.Version);
                insertPolicy.Parameters.AddWithValue("@json", PolicySetStorageCodec.ToStorageJson(set));
                await insertPolicy.ExecuteNonQueryAsync();
            }

            await using (var insertSubject = connection.CreateCommand())
            {
                insertSubject.CommandText =
                    $"""
                     INSERT INTO dbo.{AccessControlSchema.SubjectTable} (subject_id) VALUES (@id);
                     INSERT INTO dbo.{AccessControlSchema.SubjectRoleTable} (subject_id, role) VALUES (@id, @role);
                     INSERT INTO dbo.{AccessControlSchema.SubjectAttributeTable} (subject_id, name, value_json)
                     VALUES (@id, @name, @value);
                     """;
                insertSubject.Parameters.AddWithValue("@id", "user-1");
                insertSubject.Parameters.AddWithValue("@role", "EDITOR");
                insertSubject.Parameters.AddWithValue("@name", "dept");
                insertSubject.Parameters.AddWithValue("@value", AttributeValueCodec.ToJson("finance")!);
                await insertSubject.ExecuteNonQueryAsync();
            }

            await using (var insertResource = connection.CreateCommand())
            {
                insertResource.CommandText =
                    $"""
                     INSERT INTO dbo.{AccessControlSchema.ResourceAttributeTable}
                         (resource_type, resource_id, name, value_json)
                     VALUES (@type, @id, @name, @value)
                     """;
                insertResource.Parameters.AddWithValue("@type", "doc");
                insertResource.Parameters.AddWithValue("@id", "42");
                insertResource.Parameters.AddWithValue("@name", "ownerId");
                insertResource.Parameters.AddWithValue("@value", AttributeValueCodec.ToJson("user-1")!);
                await insertResource.ExecuteNonQueryAsync();
            }
        }

        var source = new SqlServerPolicySource(options);
        var loaded = await source.GetPolicySetAsync(CancellationToken.None);
        Assert.Equal("v-int", loaded.Version);
        Assert.Single(loaded.Policies);

        var subjects = new SqlServerSubjectResolver(options);
        var subject = await subjects.HydrateAsync(
            new Subject("user-1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Contains("EDITOR", subject.Roles);
        Assert.Equal("finance", subject.Attributes["dept"]);

        var resources = new SqlServerResourceResolver(options);
        var resource = await resources.HydrateAsync(
            new Resource("doc", "42", new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Equal("user-1", resource.Attributes["ownerId"]);
    }

    private static async Task WipeDataAsync(SqlConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"""
             DELETE FROM dbo.{AccessControlSchema.SubjectAttributeTable};
             DELETE FROM dbo.{AccessControlSchema.SubjectRoleTable};
             DELETE FROM dbo.{AccessControlSchema.SubjectTable};
             DELETE FROM dbo.{AccessControlSchema.ResourceAttributeTable};
             DELETE FROM dbo.{AccessControlSchema.PolicySetTable};
             """;
        await cmd.ExecuteNonQueryAsync();
    }

    private static string? ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MIREPOIX_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var localDb =
            "Server=(localdb)\\mssqllocaldb;Database=MirepoixAccessControl_Providers_Tests;Trusted_Connection=True;TrustServerCertificate=True";

        try
        {
            using var connection = new SqlConnection(localDb);
            connection.Open();
            return localDb;
        }
        catch
        {
            return null;
        }
    }
}
