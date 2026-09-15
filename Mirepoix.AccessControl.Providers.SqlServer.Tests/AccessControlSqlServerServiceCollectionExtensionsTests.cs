using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Providers.SqlServer.Tests;

public class AccessControlSqlServerServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAccessControlSqlServer_registers_source_resolvers_and_hydrator()
    {
        var services = new ServiceCollection();

        services.AddAccessControlProviders(new SqlServerProviderOptions
        {
            ConnectionString = "Server=.;Database=unused;Trusted_Connection=True;",
            PolicyCacheTtl = TimeSpan.FromMinutes(1),
        });

        using var sp = services.BuildServiceProvider();

        Assert.IsType<SqlServerPolicySource>(sp.GetRequiredService<IPolicySource>());
        Assert.IsType<SqlServerSubjectResolver>(sp.GetRequiredService<ISubjectResolver>());
        Assert.IsType<SqlServerResourceResolver>(sp.GetRequiredService<IResourceResolver>());
        Assert.IsType<CompositeBundleHydrator>(sp.GetRequiredService<IBundleHydrator>());
        Assert.NotNull(sp.GetRequiredService<SqlServerSchemaMigrator>());
        Assert.Equal(
            "Server=.;Database=unused;Trusted_Connection=True;",
            sp.GetRequiredService<SqlServerProviderOptions>().ConnectionString);
    }

    [Fact]
    public void AddAccessControlSqlServer_configure_overload_binds_options()
    {
        var services = new ServiceCollection();

        services.AddAccessControlProviders(o =>
        {
            o.ConnectionString = "Server=configure;";
            o.PolicyCacheTtl = TimeSpan.FromSeconds(30);
        });

        using var sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<SqlServerProviderOptions>();
        Assert.Equal("Server=configure;", options.ConnectionString);
        Assert.Equal(TimeSpan.FromSeconds(30), options.PolicyCacheTtl);
    }

    [Fact]
    public void Slices_share_options_and_migrator_once()
    {
        var services = new ServiceCollection();
        services.AddAccessControlPolicyProviders(o => o.ConnectionString = "Server=.;");
        services.AddAccessControlSubjectProviders();
        services.AddAccessControlResourceProviders();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(SqlServerProviderOptions)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(SqlServerSchemaMigrator)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IBundleHydrator)));
    }

    [Fact]
    public void AddAccessControlSqlServer_throws_when_foreign_policy_source_registered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySource>(new MemoryPolicySource(new PolicySet("v", Array.Empty<AccessPolicy>())));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlPolicyProviders(o => o.ConnectionString = "Server=.;"));

        Assert.Contains("MemoryPolicySource", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AddAccessControlPolicyProviders", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddAccessControlSqlServer_throws_when_called_twice()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(o => o.ConnectionString = "Server=.;");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlProviders(o => o.ConnectionString = "Server=.;"));

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
