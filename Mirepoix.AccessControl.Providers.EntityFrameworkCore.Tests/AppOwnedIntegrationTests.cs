using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers.Codec;
using Mirepoix.AccessControl.Providers.Entities;
using Mirepoix.AccessControl.Providers.Schema;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

public class AppOwnedIntegrationTests
{
    [Fact]
    public async Task App_owned_inmemory_resolvers_read_seeded_rows()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppOwnedDbContext>(db => db.UseInMemoryDatabase(dbName));
        services.AddAccessControlProviders<AppOwnedDbContext>();

        await using var sp = services.BuildServiceProvider();

        await using (var scope = sp.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppOwnedDbContext>();
            var set = new PolicySet(
                "v-app",
                new[]
                {
                    new AccessPolicy(
                        "p1",
                        AuthorizationResult.Allow,
                        null,
                        new IAtom[] { new RoleMembershipAtom(new[] { "ADMIN" }) }),
                });

            db.Set<PolicySetEntity>().Add(new PolicySetEntity
            {
                Id = AccessControlSchema.PolicySetSingletonId,
                Version = set.Version,
                PayloadJson = PolicySetStorageCodec.ToStorageJson(set),
                UpdatedUtc = DateTime.UtcNow,
            });
            db.Set<SubjectEntity>().Add(new SubjectEntity
            {
                SubjectId = "admin-1",
                Roles = { new SubjectRoleEntity { SubjectId = "admin-1", Role = "ADMIN" } },
            });
            await db.SaveChangesAsync();
        }

        var source = sp.GetRequiredService<IPolicySource>();
        Assert.Equal("v-app", (await source.GetPolicySetAsync(CancellationToken.None)).Version);

        var subject = await sp.GetRequiredService<ISubjectResolver>().HydrateAsync(
            new Subject("admin-1", new HashSet<string>(), new Dictionary<string, object?>()),
            CancellationToken.None);
        Assert.Contains("ADMIN", subject.Roles);
    }
}
