using Microsoft.Extensions.DependencyInjection;
using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class ValidatingPolicySetEditorTests
{
    [Fact]
    public async Task Complete_pull_and_supported_pattern_calls_inner_and_updates_snapshot()
    {
        var inner = new RecordingEditor();
        var snapshot = new OperationCatalogSnapshot();
        var pull = new OperationCatalogPull(
            [new EnforcementAppCatalog("billing", [new PublishedOperation("invoice:post", "", "")])],
            []);
        var editor = new ValidatingPolicySetEditor(inner, snapshot, new FixedPull(pull), new OperationPatternValidator());

        await editor.ReplaceAsync(PolicyWith("invoice:post"));

        Assert.True(snapshot.HasSnapshot);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Partial_pull_keeps_previous_snapshot_and_still_calls_inner_when_pattern_passes()
    {
        var snapshot = new OperationCatalogSnapshot();
        snapshot.Apply(new OperationCatalogPull(
            [new EnforcementAppCatalog("billing", [new PublishedOperation("invoice:post", "", "")])],
            []));
        var inner = new RecordingEditor();
        var editor = new ValidatingPolicySetEditor(
            inner,
            snapshot,
            new FixedPull(new OperationCatalogPull([], ["invoices"])),
            new OperationPatternValidator());

        await editor.ReplaceAsync(PolicyWith("invoice:post"));

        Assert.Equal(["invoice:post"], snapshot.OperationValues);
        Assert.Equal(["invoices"], snapshot.LastPull!.FailedApps);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task No_snapshot_throws_and_does_not_call_inner()
    {
        var inner = new RecordingEditor();
        var editor = new ValidatingPolicySetEditor(
            inner,
            new OperationCatalogSnapshot(),
            new FixedPull(new OperationCatalogPull([], ["invoices"])),
            new OperationPatternValidator());

        await Assert.ThrowsAsync<OperationCatalogUnavailableException>(() => editor.ReplaceAsync(PolicyWith("invoice:post")));
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Bad_pattern_does_not_update_snapshot_or_call_inner()
    {
        var snapshot = new OperationCatalogSnapshot();
        var inner = new RecordingEditor();
        var pull = new OperationCatalogPull(
            [new EnforcementAppCatalog("billing", [new PublishedOperation("invoice:post", "", "")])],
            []);
        var editor = new ValidatingPolicySetEditor(inner, snapshot, new FixedPull(pull), new OperationPatternValidator());

        await Assert.ThrowsAsync<ArgumentException>(() => editor.ReplaceAsync(PolicyWith("doc:read")));
        Assert.False(snapshot.HasSnapshot);
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public void AddAccessControlOperationCatalog_twice_wraps_editor_once()
    {
        var inner = new RecordingEditor();
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySetEditor>(inner);
        services.AddAccessControlOperationCatalog(options => options.AddApp("billing", "https://billing.example"));
        services.AddAccessControlOperationCatalog(options => options.AddApp("billing", "https://billing.example"));

        using var provider = services.BuildServiceProvider();
        var wrapper = Assert.IsType<ValidatingPolicySetEditor>(provider.GetRequiredService<IPolicySetEditor>());
        Assert.Same(inner, wrapper.Inner);
    }

    [Fact]
    public void AddAccessControlOperationCatalog_requires_policy_set_editor()
    {
        var services = new ServiceCollection();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAccessControlOperationCatalog(options => options.AddApp("billing", "https://billing.example")));
        Assert.Equal("AddAccessControlOperationCatalog requires IPolicySetEditor.", ex.Message);
    }

    [Fact]
    public void AddAccessControlOperationCatalog_rejects_empty_app_list()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySetEditor>(new RecordingEditor());
        Assert.Throws<ArgumentException>(() => services.AddAccessControlOperationCatalog(_ => { }));
    }

    [Fact]
    public void AddAccessControlOperationCatalog_keeps_editor_lifetime()
    {
        var services = new ServiceCollection();
        services.AddScoped<IPolicySetEditor, RecordingEditor>();
        services.AddAccessControlOperationCatalog(options => options.AddApp("billing", "https://billing.example"));

        var descriptor = services.Last(candidate => candidate.ServiceType == typeof(IPolicySetEditor));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private static PolicySet PolicyWith(string pattern) => new(
        "v1",
        [new Policy("p", AuthorizationResult.Allow, null, [new OperationMatchAtom(Operation.Parse(pattern))])]);

    private sealed class RecordingEditor : IPolicySetEditor
    {
        public int Calls { get; private set; }

        public void Replace(PolicySet set) => ReplaceAsync(set).GetAwaiter().GetResult();

        public Task ReplaceAsync(PolicySet set, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedPull : IEnforcementCatalogClient
    {
        private readonly OperationCatalogPull _pull;

        public FixedPull(OperationCatalogPull pull) => _pull = pull;

        public Task<OperationCatalogPull> PullAsync(CancellationToken cancellationToken) => Task.FromResult(_pull);
    }
}
