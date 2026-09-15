using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

/// <summary>
/// Runs only when MIREPOIX_POSTGRES is set to a PostgreSQL connection string.
/// </summary>
public class PostgresIntegrationTests
{
    private static readonly string? ConnectionString =
        Environment.GetEnvironmentVariable("MIREPOIX_POSTGRES");

    [Fact]
    public async Task Package_driven_postgres_schema_and_policy_read()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            return;

        var services = new ServiceCollection();
        services.AddAccessControlProviders(o =>
        {
            o.ConfigureDb = db => db.UseNpgsql(ConnectionString);
        });

        await using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<AccessControlSchemaApplier>().ApplyAsync();

        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AccessControlDbContext>();
            db.PolicySets.RemoveRange(db.PolicySets);
            await db.SaveChangesAsync();

            var set = new PolicySet(
                "v-pg",
                new[]
                {
                    new AccessPolicy(
                        "p1",
                        AuthorizationResult.Allow,
                        null,
                        new IAtom[] { new RoleMembershipAtom(new[] { "X" }) }),
                });

            db.PolicySets.Add(new PolicySetEntity
            {
                Id = AccessControlSchema.PolicySetSingletonId,
                Version = set.Version,
                PayloadJson = PolicySetStorageCodec.ToStorageJson(set),
                UpdatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var loaded = await sp.GetRequiredService<IPolicySource>().GetPolicySetAsync(CancellationToken.None);
        Assert.Equal("v-pg", loaded.Version);
    }
}
