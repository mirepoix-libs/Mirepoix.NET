using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

public class PackageDrivenIntegrationTests
{
    // Env: MIREPOIX_SQLSERVER — optional override. Default tries LocalDB.

    private static readonly string? ConnectionString = ResolveSqlServer();

    [Fact]
    public async Task Package_driven_sqlserver_round_trip()
    {
        if (ConnectionString is null)
            return;

        var services = new ServiceCollection();
        services.AddAccessControlProviders(o =>
        {
            o.ConfigureDb = db => db.UseSqlServer(ConnectionString);
        });

        await using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<AccessControlSchemaApplier>().ApplyAsync();

        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();
            await WipeAsync(db);

            var set = new PolicySet(
                "v-ef",
                new[]
                {
                    new AccessPolicy(
                        "allow-editor",
                        AuthorizationResult.Allow,
                        null,
                        new IAtom[] { new RoleMembershipAtom(new[] { "EDITOR" }) }),
                });

            db.PolicySets.Add(new PolicySetEntity
            {
                Id = AccessControlSchema.PolicySetSingletonId,
                Version = set.Version,
                PayloadJson = PolicySetStorageCodec.ToStorageJson(set),
                UpdatedUtc = DateTime.UtcNow,
            });
            db.Subjects.Add(new SubjectEntity
            {
                SubjectId = "user-1",
                Roles = { new SubjectRoleEntity { SubjectId = "user-1", Role = "EDITOR" } },
                Attributes =
                {
                    new SubjectAttributeEntity
                    {
                        SubjectId = "user-1",
                        Name = "dept",
                        ValueJson = AttributeValueCodec.ToJson("finance"),
                    },
                },
            });
            db.ResourceAttributes.Add(new ResourceAttributeEntity
            {
                ResourceType = "doc",
                ResourceId = "42",
                Name = "ownerId",
                ValueJson = AttributeValueCodec.ToJson("user-1"),
            });
            await db.SaveChangesAsync();
        }

        var source = sp.GetRequiredService<IPolicySource>();
        var loaded = await source.GetPolicySetAsync(CancellationToken.None);
        Assert.Equal("v-ef", loaded.Version);

        var subjects = sp.GetRequiredService<ISubjectResolver>();
        var subject = await subjects.HydrateAsync(
            new Subject("user-1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Contains("EDITOR", subject.Roles);
        Assert.Equal("finance", subject.Attributes["dept"]);

        var resources = sp.GetRequiredService<IResourceResolver>();
        var resource = await resources.HydrateAsync(
            new Resource("doc", "42", new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Equal("user-1", resource.Attributes["ownerId"]);

        var checker = new LocalAccessChecker(source, sp.GetRequiredService<IBundleHydrator>());
        var decision = await checker.CheckAsync(
            new AuthorizationRequest(
                new Subject("user-1", new HashSet<string>(), new Dictionary<string, object?>()),
                new Resource("doc", "42", new Dictionary<string, object?>()),
                Operation.Parse("doc:edit"),
                new AccessContext(
                    DateTimeOffset.UtcNow,
                    new Dictionary<string, object?>(),
                    new Dictionary<string, object?>())),
            CancellationToken.None);
        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
    }

    private static async Task WipeAsync(AccessControlDbContext db)
    {
        db.SubjectAttributes.RemoveRange(db.SubjectAttributes);
        db.SubjectRoles.RemoveRange(db.SubjectRoles);
        db.Subjects.RemoveRange(db.Subjects);
        db.ResourceAttributes.RemoveRange(db.ResourceAttributes);
        db.PolicySets.RemoveRange(db.PolicySets);
        await db.SaveChangesAsync();
    }

    private static string? ResolveSqlServer()
    {
        var fromEnv = Environment.GetEnvironmentVariable("MIREPOIX_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv;

        var localDb =
            "Server=(localdb)\\mssqllocaldb;Database=MirepoixAccessControl_EF_Tests;Trusted_Connection=True;TrustServerCertificate=True";
        try
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(localDb);
            connection.Open();
            return localDb;
        }
        catch
        {
            return null;
        }
    }
}
