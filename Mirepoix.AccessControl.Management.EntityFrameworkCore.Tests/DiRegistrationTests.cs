using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Management.EntityFrameworkCore.Tests;

public sealed class DiRegistrationTests
{
    [Fact]
    public void Registration_without_providers_throws_with_setup_guidance()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddAccessControlManagement());

        Assert.Contains("AddAccessControlProviders", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Mapped_app_owned_roles_omit_role_assignment_store()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(db => db.UseInMemoryDatabase("management-di-mapped"));
        services.AddAccessControlProviders<AppDbContext>(options =>
            options.MapSubject<AppUser>(map => map
                .Id(user => user.Id)
                .Roles(user => user.Role)));

        services.AddAccessControlManagement<AppDbContext>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Null(scope.ServiceProvider.GetService<IRoleAssignmentStore>());
        AssertAlwaysRegistered(scope.ServiceProvider);
    }

    [Fact]
    public void Package_driven_native_layout_registers_all_management_stores()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(options =>
            options.ConfigureDb = db => db.UseInMemoryDatabase("management-di-native"));

        services.AddAccessControlManagement();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        AssertAlwaysRegistered(scope.ServiceProvider);
        Assert.IsType<EntityFrameworkRoleAssignmentStore>(
            scope.ServiceProvider.GetRequiredService<IRoleAssignmentStore>());
    }

    [Fact]
    public void Generic_registration_constructs_stores_with_app_context()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(db => db.UseInMemoryDatabase("management-di-app"));
        services.AddAccessControlProviders<AppDbContext>();

        services.AddAccessControlManagement<AppDbContext>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        AssertAlwaysRegistered(scope.ServiceProvider);
        Assert.IsType<EntityFrameworkRoleAssignmentStore>(
            scope.ServiceProvider.GetRequiredService<IRoleAssignmentStore>());
    }

    private static void AssertAlwaysRegistered(IServiceProvider provider)
    {
        Assert.IsType<EntityFrameworkRoleCatalog>(provider.GetRequiredService<IRoleCatalog>());
        Assert.IsType<EntityFrameworkSodConstraintStore>(provider.GetRequiredService<ISodConstraintStore>());
        Assert.IsType<EntityFrameworkOwnershipHelper>(provider.GetRequiredService<IOwnershipHelper>());
        Assert.IsType<EntityFrameworkLabelHelper>(provider.GetRequiredService<ILabelHelper>());
        Assert.IsType<EntityFrameworkPolicySetEditor>(provider.GetRequiredService<IPolicySetEditor>());
    }

    private sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyAccessControl();
            modelBuilder.Entity<AppUser>().HasKey(user => user.Id);
        }
    }

    private sealed class AppUser
    {
        public string Id { get; set; } = "";

        public string Role { get; set; } = "";
    }
}
