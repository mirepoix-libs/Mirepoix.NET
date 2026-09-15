using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Evaluation;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

public class LocalAccessCheckerTests
{
    [Fact]
    public async Task Check_hydration_failure_is_fail_closed()
    {
        var failing = new ThrowingHydrator();
        var checker = new LocalAccessChecker(
            new MemoryPolicySource(new PolicySet("v1", Array.Empty<Policy>())),
            failing);

        var decision = await checker.CheckAsync(MinimalRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.HydrationFailed, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
        Assert.Empty(decision.PolicyHits);
    }

    [Fact]
    public async Task Check_policy_source_failure_is_fail_closed()
    {
        var checker = new LocalAccessChecker(new ThrowingPolicySource(), new PassThroughHydrator());

        var decision = await checker.CheckAsync(MinimalRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.PolicySourceFailed, decision.Status);
        Assert.Null(decision.PolicySetVersion);
        Assert.Empty(decision.PolicyHits);
    }

    [Fact]
    public async Task Check_happy_path_success()
    {
        var set = new PolicySet("v1", new[]
        {
            new Policy("p1", AuthorizationResult.Allow, "admins", new IAtom[]
            {
                new RoleMembershipAtom(new[] { "ADMIN" })
            })
        });
        var hydrator = new CompositeBundleHydrator(
            new InMemorySubjectResolver(new Dictionary<string, Subject>
            {
                ["u1"] = new Subject("u1", new HashSet<string> { "ADMIN" }, new Dictionary<string, object?>())
            }));
        var checker = new LocalAccessChecker(new MemoryPolicySource(set), hydrator);

        var decision = await checker.CheckAsync(MinimalRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
        var hit = Assert.Single(decision.PolicyHits);
        Assert.Equal("p1", hit.PolicyId);
        Assert.Equal(AuthorizationResult.Allow, hit.Effect);
    }

    [Fact]
    public async Task Check_kernel_defaulted_when_no_policy_hits()
    {
        var checker = new LocalAccessChecker(
            new MemoryPolicySource(new PolicySet("v1", Array.Empty<Policy>())),
            new PassThroughHydrator());

        var decision = await checker.CheckAsync(MinimalRequest(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.Defaulted, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
        Assert.Empty(decision.PolicyHits);
    }

    [Fact]
    public async Task Check_defaults_to_deny_overrides_strategy()
    {
        var set = new PolicySet("v1", new[]
        {
            new Policy("allow", AuthorizationResult.Allow, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            }),
            new Policy("deny", AuthorizationResult.Deny, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            })
        });
        var request = new AuthorizationRequest(
            new Subject("u1", new HashSet<string> { "USER" }, new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
        var checker = new LocalAccessChecker(new MemoryPolicySource(set), new PassThroughHydrator());

        var decision = await checker.CheckAsync(request, CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
    }

    [Fact]
    public async Task Check_uses_supplied_combination_strategy()
    {
        var set = new PolicySet("v1", new[]
        {
            new Policy("deny", AuthorizationResult.Deny, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            }),
            new Policy("allow", AuthorizationResult.Allow, null, new IAtom[]
            {
                new RoleMembershipAtom(new[] { "USER" })
            })
        });
        var request = new AuthorizationRequest(
            new Subject("u1", new HashSet<string> { "USER" }, new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));
        var checker = new LocalAccessChecker(
            new MemoryPolicySource(set),
            new PassThroughHydrator(),
            new PermitOverridesStrategy());

        var decision = await checker.CheckAsync(request, CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
    }

    [Fact]
    public async Task Check_policy_source_cancellation_propagates()
    {
        var checker = new LocalAccessChecker(
            new CancelingPolicySource(),
            new PassThroughHydrator());

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => checker.CheckAsync(MinimalRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Check_hydration_cancellation_propagates()
    {
        var checker = new LocalAccessChecker(
            new MemoryPolicySource(new PolicySet("v1", Array.Empty<Policy>())),
            new TaskCancelingHydrator());

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => checker.CheckAsync(MinimalRequest(), CancellationToken.None));
    }

    private static AuthorizationRequest MinimalRequest() =>
        new(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:read"),
            new AccessContext(null, new Dictionary<string, object?>(), new Dictionary<string, object?>()));

    private sealed class ThrowingHydrator : IBundleHydrator
    {
        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("hydration failed");
    }

    private sealed class ThrowingPolicySource : IPolicySource
    {
        public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("policy source failed");
    }

    private sealed class CancelingPolicySource : IPolicySource
    {
        public Task<PolicySet> GetPolicySetAsync(CancellationToken cancellationToken) =>
            throw new OperationCanceledException(cancellationToken);
    }

    private sealed class TaskCancelingHydrator : IBundleHydrator
    {
        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException();
    }

    private sealed class PassThroughHydrator : IBundleHydrator
    {
        public Task<AuthorizationBundle> HydrateAsync(
            AuthorizationRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new AuthorizationBundle(
                request.Subject,
                request.Resource,
                request.Operation,
                request.Context));
    }
}
