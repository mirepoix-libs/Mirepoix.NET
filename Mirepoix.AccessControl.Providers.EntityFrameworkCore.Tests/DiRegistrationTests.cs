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
        Assert.Null(sp.GetService<IResourceHydrator>());
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

        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(EntityFrameworkProviderOptions)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(AccessControlSchemaApplier)));
        Assert.Equal(1, services.Count(d => d.ServiceType == typeof(IBundleHydrator)));

        using var sp = services.BuildServiceProvider();
        Assert.IsType<EntityFrameworkPolicySource>(sp.GetRequiredService<IPolicySource>());
        Assert.IsType<EntityFrameworkSubjectResolver>(sp.GetRequiredService<ISubjectResolver>());
        Assert.Null(sp.GetService<IResourceHydrator>());
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

    [Fact]
    public async Task Composite_hydrator_uses_app_registered_resource_hydrator()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IResourceHydrator, TestResourceHydrator>();
        services.AddAccessControlPolicyProviders(o =>
            o.ConfigureDb = db => db.UseInMemoryDatabase("custom-resource"));

        using var sp = services.BuildServiceProvider();
        var bundle = await sp.GetRequiredService<IBundleHydrator>().HydrateAsync(
            new AuthorizationRequest(
                new Subject("alice", new HashSet<string>(), new Dictionary<string, object?>()),
                new Resource("doc", "1", new Dictionary<string, object?>()),
                Operation.Parse("doc:read"),
                new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>())),
            CancellationToken.None);

        Assert.Equal("domain", bundle.Resource.Attributes["source"]);
    }

    [Fact]
    public async Task Composite_hydrator_resolves_scoped_resource_hydrator_per_call()
    {
        var services = new ServiceCollection();
        services.AddAccessControlPolicyProviders(o =>
            o.ConfigureDb = db => db.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        services.AddScoped<ScopeProbe>();
        services.AddAccessControlResourceResolver<ScopedProbeResolver>();

        await using var root = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
        });

        var rootResolve = Assert.Throws<InvalidOperationException>(
            () => root.GetRequiredService<IResourceHydrator>());
        Assert.Contains("scoped", rootResolve.Message, StringComparison.OrdinalIgnoreCase);

        var hydrator = root.GetRequiredService<IBundleHydrator>();
        var request = new AuthorizationRequest(
            new Subject("alice", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("document", "1", new Dictionary<string, object?>()),
            Operation.Parse("document:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

        var first = await hydrator.HydrateAsync(request, CancellationToken.None);
        var second = await hydrator.HydrateAsync(request, CancellationToken.None);

        Assert.NotEqual(first.Resource.Attributes["scopeId"], second.Resource.Attributes["scopeId"]);
    }

    private sealed class ScopeProbe
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
    }

    private sealed class ProbeDocument;

    [AccessResourceType("document")]
    private sealed class ScopedProbeResolver : IResourceResolver<ProbeDocument>
    {
        private readonly ScopeProbe _probe;

        public ScopedProbeResolver(ScopeProbe probe) => _probe = probe;

        public Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken) =>
            Task.FromResult(partial with
            {
                Attributes = new Dictionary<string, object?> { ["scopeId"] = _probe.Id },
            });
    }

    private sealed class TestResourceHydrator : IResourceHydrator
    {
        public Task<Resource> HydrateAsync(
            Resource partial,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                new Resource(
                    partial.Type,
                    partial.Id,
                    new Dictionary<string, object?> { ["source"] = "domain" }));
    }
}
