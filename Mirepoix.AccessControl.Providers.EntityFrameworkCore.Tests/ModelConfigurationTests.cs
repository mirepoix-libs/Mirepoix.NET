using Microsoft.EntityFrameworkCore;
using Mirepoix.AccessControl.Providers.Entities;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

public class ModelConfigurationTests
{
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
        Assert.Contains("ac_resource_attribute", entityTypes.Keys);

        var policy = entityTypes["ac_policy_set"];
        Assert.Contains(policy.GetProperties(), p => p.GetColumnName() == "payload_json");
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
