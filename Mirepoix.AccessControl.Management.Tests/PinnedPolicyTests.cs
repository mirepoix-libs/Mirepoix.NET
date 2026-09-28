using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;
using Microsoft.Extensions.DependencyInjection;
using PolicyModel = Mirepoix.AccessControl.Policy.Policy;

namespace Mirepoix.AccessControl.Management.Tests;

public sealed class PinnedPolicyTests
{
    [Fact]
    public void Add_with_no_policy_throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySource>(new Store());
        services.AddSingleton<IPolicySetEditor>(new Store());

        var exception = Assert.Throws<ArgumentException>(() =>
            services.AddAccessControlPinnedPolicies(_ => { }));

        Assert.Equal("At least one pinned policy is required.", exception.Message);
    }

    [Fact]
    public void Add_twice_throws()
    {
        var services = Registered();
        services.AddAccessControlPinnedPolicies(pins => pins.Add(Pin()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlPinnedPolicies(pins => pins.Add(Pin())));

        Assert.Equal("Access-control pinned policies are already registered.", exception.Message);
    }

    [Fact]
    public void Add_without_source_throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySetEditor>(new Store());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlPinnedPolicies(pins => pins.Add(Pin())));

        Assert.Equal("Pinned policies require IPolicySource and IPolicySetEditor.", exception.Message);
    }

    [Fact]
    public async Task Get_appends_pin_and_drops_stored_copy()
    {
        var stored = new PolicyModel("builtin.policy-admin", AuthorizationResult.Deny, "stale", [new RoleMembershipAtom(["Other"])]);
        var services = Registered(new PolicySet("v3", [stored, Editable()]));
        services.AddAccessControlPinnedPolicies(pins => pins.Add(Pin()));
        var source = services.BuildServiceProvider().GetRequiredService<IPolicySource>();

        var set = await source.GetPolicySetAsync(CancellationToken.None);

        Assert.Equal("v3", set.Version);
        Assert.Equal(new[] { "editable", "builtin.policy-admin" }, set.Policies.Select(policy => policy.Id));
        Assert.Equal(AuthorizationResult.Allow, set.Policies[1].Effect);
    }

    [Fact]
    public async Task ReplaceAsync_drops_exact_pin_and_rejects_a_changed_pin()
    {
        var store = new Store(new PolicySet("v3", [Editable()]));
        var services = Registered(store);
        services.AddAccessControlPinnedPolicies(pins => pins.Add(Pin()));
        var editor = services.BuildServiceProvider().GetRequiredService<IPolicySetEditor>();

        await editor.ReplaceAsync(new PolicySet("v4", [Editable(), Pin()]), CancellationToken.None);

        Assert.Equal(new[] { "editable" }, store.Current.Policies.Select(policy => policy.Id));
        Assert.Equal("v4", store.Current.Version);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            editor.ReplaceAsync(new PolicySet("v5", [ChangedPin()]), CancellationToken.None));
        Assert.Equal("Policy 'builtin.policy-admin' is pinned.", exception.Message);
        Assert.Equal("v4", store.Current.Version);
    }

    [Fact]
    public void Replace_rejects_a_changed_pin()
    {
        var services = Registered(new PolicySet("v3", [Editable()]));
        services.AddAccessControlPinnedPolicies(pins => pins.Add(Pin()));
        var editor = services.BuildServiceProvider().GetRequiredService<IPolicySetEditor>();

        var exception = Assert.Throws<ArgumentException>(() =>
            editor.Replace(new PolicySet("v9", [ChangedPin()])));

        Assert.Equal("Policy 'builtin.policy-admin' is pinned.", exception.Message);
    }

    private static ServiceCollection Registered(Store? store = null)
    {
        store ??= new Store(new PolicySet("v3", []));
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySource>(store);
        services.AddSingleton<IPolicySetEditor>(store);
        return services;
    }

    private static ServiceCollection Registered(PolicySet set) => Registered(new Store(set));

    private static PolicyModel Pin() => new(
        "builtin.policy-admin",
        AuthorizationResult.Allow,
        "Policy admins may read and replace the policy set.",
        [new RoleMembershipAtom(["PolicyAdmin"]), new OperationMatchAtom(Operation.Parse("access-control:policy-set:*"))]);

    private static PolicyModel ChangedPin() => new(
        "builtin.policy-admin",
        AuthorizationResult.Deny,
        "Policy admins may read and replace the policy set.",
        [new RoleMembershipAtom(["PolicyAdmin"])]);

    private static PolicyModel Editable() => new(
        "editable",
        AuthorizationResult.Deny,
        null,
        [new RoleMembershipAtom(["guest"])]);

    private sealed class Store : IPolicySource, IPolicySetEditor
    {
        public Store(PolicySet? set = null) => Current = set ?? new PolicySet("v0", []);

        public PolicySet Current { get; private set; }

        public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public void Replace(PolicySet set) => Current = set;

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
        {
            Current = set;
            return Task.CompletedTask;
        }
    }
}
