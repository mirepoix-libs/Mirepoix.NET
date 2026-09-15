using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl.Policy;
using AccessPolicy = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Providers.EntityFrameworkCore.Tests;

public class DiRegistrationTests
{
    [Fact]
    public void Package_driven_requires_ConfigureDb()
    {
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlPolicyProviders());

        Assert.Contains("ConfigureDb", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Package_driven_registers_context_seams_and_applier()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(o =>
        {
            o.ConfigureDb = db => db.UseInMemoryDatabase("di-package");
            o.PolicyCacheTtl = TimeSpan.FromMinutes(1);
        });

        using var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<AccessControlDbContext>());
        Assert.IsType<EntityFrameworkPolicySource>(sp.GetRequiredService<IPolicySource>());
        Assert.IsType<EntityFrameworkSubjectResolver>(sp.GetRequiredService<ISubjectResolver>());
        Assert.IsType<EntityFrameworkResourceResolver>(sp.GetRequiredService<IResourceResolver>());
        Assert.IsType<CompositeBundleHydrator>(sp.GetRequiredService<IBundleHydrator>());
        Assert.NotNull(sp.GetRequiredService<AccessControlSchemaApplier>());
        Assert.Equal(TimeSpan.FromMinutes(1), sp.GetRequiredService<EntityFrameworkProviderOptions>().PolicyCacheTtl);
    }

    [Fact]
    public void Slices_share_options_and_context_once()
    {
        var services = new ServiceCollection();
        services.AddAccessControlPolicyProviders(o =>
            o.ConfigureDb = db => db.UseInMemoryDatabase("slices"));
        services.AddAccessControlSubjectProviders();
        services.AddAccessControlResourceProviders();

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(EntityFrameworkProviderOptions)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(AccessControlSchemaApplier)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IBundleHydrator)));

        using var sp = services.BuildServiceProvider();
        Assert.IsType<EntityFrameworkPolicySource>(sp.GetRequiredService<IPolicySource>());
        Assert.IsType<EntityFrameworkSubjectResolver>(sp.GetRequiredService<ISubjectResolver>());
        Assert.IsType<EntityFrameworkResourceResolver>(sp.GetRequiredService<IResourceResolver>());
    }

    [Fact]
    public void Policy_slice_twice_throws()
    {
        var services = new ServiceCollection();
        services.AddAccessControlPolicyProviders(o =>
            o.ConfigureDb = db => db.UseInMemoryDatabase("twice"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlPolicyProviders());

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void App_owned_registers_seams_without_schema_applier()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppOwnedDbContext>(db => db.UseInMemoryDatabase("di-app"));
        services.AddAccessControlProviders<AppOwnedDbContext>();

        using var sp = services.BuildServiceProvider();

        Assert.IsType<EntityFrameworkPolicySource>(sp.GetRequiredService<IPolicySource>());
        Assert.Null(sp.GetService<AccessControlSchemaApplier>());
        Assert.Null(sp.GetService<AccessControlDbContext>());
    }

    [Fact]
    public void Package_driven_throws_when_foreign_policy_source_registered()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySource>(new MemoryPolicySource(new PolicySet("v", Array.Empty<AccessPolicy>())));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlPolicyProviders(o =>
                o.ConfigureDb = db => db.UseInMemoryDatabase("conflict")));

        Assert.Contains("MemoryPolicySource", ex.Message, StringComparison.Ordinal);
        Assert.Contains("AddAccessControlPolicyProviders", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Package_driven_throws_when_unified_called_twice()
    {
        var services = new ServiceCollection();
        services.AddAccessControlProviders(o =>
            o.ConfigureDb = db => db.UseInMemoryDatabase("once"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlProviders(o =>
                o.ConfigureDb = db => db.UseInMemoryDatabase("twice")));

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
