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

    [Fact]
    public async Task Unknown_attribute_throws_and_does_not_apply_either_snapshot()
    {
        var inner = new RecordingEditor();
        var (provider, editor) = Catalogs(
            inner,
            operations: CompleteOperations(),
            attributes: Attributes(
                providerApps: [new AttributeAppCatalog("identity", [Dept])],
                enforcementApps: []));

        await Assert.ThrowsAsync<ArgumentException>(() => editor.ReplaceAsync(PolicyWithSubject("title")));

        Assert.Equal(0, inner.Calls);
        Assert.False(provider.GetRequiredService<OperationCatalogSnapshot>().HasSnapshot);
        Assert.False(provider.GetRequiredService<AttributeCatalogSnapshot>().HasSnapshot);
    }

    [Fact]
    public async Task Known_attribute_and_operation_apply_both_snapshots_and_call_inner()
    {
        var inner = new RecordingEditor();
        var (provider, editor) = Catalogs(
            inner,
            operations: CompleteOperations(),
            attributes: Attributes(
                providerApps: [new AttributeAppCatalog("identity", [Dept])],
                enforcementApps: [new AttributeAppCatalog("billing", [Time])]));

        await editor.ReplaceAsync(new PolicySet(
            "v1",
            [new Policy("p", AuthorizationResult.Allow, null,
            [
                new OperationMatchAtom(Operation.Parse("invoice:post")),
                new AttributeValueAtom(AttributeTarget.Subject, "subject", "dept", ComparisonOperator.Equals, "eng"),
                new AttributeValueAtom(AttributeTarget.Context, null, "time", ComparisonOperator.GreaterThan, 0),
            ])]));

        Assert.Equal(1, inner.Calls);
        Assert.True(provider.GetRequiredService<OperationCatalogSnapshot>().HasSnapshot);
        Assert.True(provider.GetRequiredService<AttributeCatalogSnapshot>().HasSnapshot);
    }

    [Fact]
    public async Task Empty_provider_list_leaves_subject_ungated_and_still_gates_context()
    {
        var inner = new RecordingEditor();
        var (_, editor) = Catalogs(
            inner,
            operations: null,
            attributes: Attributes(
                providerApps: [],
                enforcementApps: [new AttributeAppCatalog("billing", [Time])],
                configure: options => options.AddEnforcementApp("billing", "https://billing.example")));

        await editor.ReplaceAsync(PolicyWithSubject("title"));
        Assert.Equal(1, inner.Calls);

        await Assert.ThrowsAsync<ArgumentException>(() => editor.ReplaceAsync(PolicyWithContext("tenant")));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Empty_enforcement_attribute_list_leaves_context_ungated_and_still_gates_subject()
    {
        var inner = new RecordingEditor();
        var (_, editor) = Catalogs(
            inner,
            operations: null,
            attributes: Attributes(
                providerApps: [new AttributeAppCatalog("identity", [Dept])],
                enforcementApps: [],
                configure: options => options.AddProviderApp("identity", "https://identity.example")));

        await editor.ReplaceAsync(PolicyWithContext("tenant"));
        Assert.Equal(1, inner.Calls);

        await Assert.ThrowsAsync<ArgumentException>(() => editor.ReplaceAsync(PolicyWithSubject("title")));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task Empty_attribute_lists_skip_the_attribute_pull()
    {
        var inner = new RecordingEditor();
        var attributes = new FixedAttributePull(new AttributeCatalogPull([], [], ["identity"]));
        var (provider, editor) = Catalogs(
            inner,
            operations: CompleteOperations(),
            attributes: new PreparedAttributes(attributes, _ => { }));

        await editor.ReplaceAsync(PolicyWithSubject("title"));

        Assert.Equal(0, attributes.Calls);
        Assert.Equal(1, inner.Calls);
        Assert.False(provider.GetRequiredService<AttributeCatalogSnapshot>().HasSnapshot);
    }

    [Fact]
    public async Task Missing_attribute_snapshot_throws_and_does_not_call_inner()
    {
        var inner = new RecordingEditor();
        var (provider, editor) = Catalogs(
            inner,
            operations: CompleteOperations(),
            attributes: Attributes(
                providerApps: [],
                enforcementApps: [],
                failed: ["identity"]));

        await Assert.ThrowsAsync<AttributeCatalogUnavailableException>(() => editor.ReplaceAsync(PolicyWithSubject("dept")));
        Assert.Equal(0, inner.Calls);
        Assert.False(provider.GetRequiredService<OperationCatalogSnapshot>().HasSnapshot);
        Assert.False(provider.GetRequiredService<AttributeCatalogSnapshot>().HasSnapshot);
    }

    [Fact]
    public async Task Missing_operation_and_attribute_snapshots_throw_the_operation_exception()
    {
        var inner = new RecordingEditor();
        var (_, editor) = Catalogs(
            inner,
            operations: new FixedPull(new OperationCatalogPull([], ["billing"])),
            attributes: Attributes(
                providerApps: [],
                enforcementApps: [],
                failed: ["identity"]));

        await Assert.ThrowsAsync<OperationCatalogUnavailableException>(() => editor.ReplaceAsync(PolicyWith("invoice:post")));
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Partial_attribute_pull_keeps_the_stored_union_and_calls_inner()
    {
        var inner = new RecordingEditor();
        var client = new ScriptedAttributePull(
            new AttributeCatalogPull(
                [new AttributeAppCatalog("identity", [Dept])],
                [],
                []),
            new AttributeCatalogPull([], [], ["identity"]));
        var (provider, editor) = Catalogs(
            inner,
            operations: null,
            attributes: new PreparedAttributes(client, options => options.AddProviderApp("identity", "https://identity.example")));

        await editor.ReplaceAsync(PolicyWithSubject("dept"));
        await editor.ReplaceAsync(PolicyWithSubject("dept"));

        var snapshot = provider.GetRequiredService<AttributeCatalogSnapshot>();
        Assert.Equal(2, inner.Calls);
        Assert.Equal(["dept"], snapshot.Attributes.Select(row => row.Key).ToArray());
        Assert.Equal(["identity"], snapshot.LastPull!.FailedApps);
    }

    [Fact]
    public async Task Attribute_catalog_alone_leaves_operation_patterns_ungated()
    {
        var inner = new RecordingEditor();
        var (_, editor) = Catalogs(
            inner,
            operations: null,
            attributes: Attributes(
                providerApps: [new AttributeAppCatalog("identity", [Dept])],
                enforcementApps: []));

        await editor.ReplaceAsync(PolicyWith("doc:read"));
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public void Operation_then_attribute_catalog_wraps_the_editor_once()
    {
        var inner = new RecordingEditor();
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySetEditor>(inner);
        services.AddAccessControlOperationCatalog(options => options.AddApp("billing", "https://billing.example"));
        services.AddAccessControlAttributeCatalog(options => options.AddProviderApp("identity", "https://identity.example"));

        using var provider = services.BuildServiceProvider();
        var wrapper = Assert.IsType<ValidatingPolicySetEditor>(provider.GetRequiredService<IPolicySetEditor>());
        Assert.Same(inner, wrapper.Inner);
        Assert.IsAssignableFrom<IEnforcementCatalogClient>(provider.GetRequiredService<IEnforcementCatalogClient>());
        Assert.IsAssignableFrom<IAttributeCatalogClient>(provider.GetRequiredService<IAttributeCatalogClient>());
    }

    [Fact]
    public void Attribute_then_operation_catalog_wraps_the_editor_once()
    {
        var inner = new RecordingEditor();
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySetEditor>(inner);
        services.AddAccessControlAttributeCatalog(options => options.AddEnforcementApp("billing", "https://billing.example"));
        services.AddAccessControlOperationCatalog(options => options.AddApp("billing", "https://billing.example"));

        using var provider = services.BuildServiceProvider();
        var wrapper = Assert.IsType<ValidatingPolicySetEditor>(provider.GetRequiredService<IPolicySetEditor>());
        Assert.Same(inner, wrapper.Inner);
        var editors = services.Where(descriptor => descriptor.ServiceType == typeof(IPolicySetEditor)).ToArray();
        Assert.Single(editors);
        Assert.IsAssignableFrom<IEnforcementCatalogClient>(provider.GetRequiredService<IEnforcementCatalogClient>());
        Assert.IsAssignableFrom<IAttributeCatalogClient>(provider.GetRequiredService<IAttributeCatalogClient>());
    }

    private static readonly PublishedAttribute Dept = new(AttributeTarget.Subject, "subject", "dept");

    private static readonly PublishedAttribute Time = new(AttributeTarget.Context, null, "time");

    private static PolicySet PolicyWithSubject(string key) => new(
        "v1",
        [new Policy("p", AuthorizationResult.Allow, null,
            [new AttributeValueAtom(AttributeTarget.Subject, "subject", key, ComparisonOperator.Equals, "x")])]);

    private static PolicySet PolicyWithContext(string key) => new(
        "v1",
        [new Policy("p", AuthorizationResult.Allow, null,
            [new AttributeValueAtom(AttributeTarget.Context, null, key, ComparisonOperator.GreaterThan, 0)])]);

    private static FixedPull CompleteOperations() => new(new OperationCatalogPull(
        [new EnforcementAppCatalog("billing", [new PublishedOperation("invoice:post", "", "")])],
        []));

    private static PreparedAttributes Attributes(
        IReadOnlyList<AttributeAppCatalog> providerApps,
        IReadOnlyList<AttributeAppCatalog> enforcementApps,
        IReadOnlyList<string>? failed = null,
        Action<AttributeCatalogOptions>? configure = null)
    {
        configure ??= options =>
        {
            options.AddProviderApp("identity", "https://identity.example");
            options.AddEnforcementApp("billing", "https://billing.example");
        };
        return new PreparedAttributes(
            new FixedAttributePull(new AttributeCatalogPull(providerApps, enforcementApps, failed ?? [])),
            configure);
    }

    private static (ServiceProvider Provider, IPolicySetEditor Editor) Catalogs(
        RecordingEditor inner,
        IEnforcementCatalogClient? operations,
        PreparedAttributes attributes)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPolicySetEditor>(inner);
        if (operations is not null)
            services.AddAccessControlOperationCatalog(options => options.AddApp("billing", "https://billing.example"));

        services.AddAccessControlAttributeCatalog(attributes.Configure);
        if (operations is not null)
            services.AddSingleton(operations);
        services.AddSingleton<IAttributeCatalogClient>(attributes.Client);

        var provider = services.BuildServiceProvider();
        return (provider, provider.GetRequiredService<IPolicySetEditor>());
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

    private sealed record PreparedAttributes(IAttributeCatalogClient Client, Action<AttributeCatalogOptions> Configure);

    private sealed class FixedAttributePull : IAttributeCatalogClient
    {
        private readonly AttributeCatalogPull _pull;

        public FixedAttributePull(AttributeCatalogPull pull) => _pull = pull;

        public int Calls { get; private set; }

        public Task<AttributeCatalogPull> PullAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_pull);
        }
    }

    private sealed class ScriptedAttributePull : IAttributeCatalogClient
    {
        private readonly Queue<AttributeCatalogPull> _pulls;

        public ScriptedAttributePull(params AttributeCatalogPull[] pulls) => _pulls = new Queue<AttributeCatalogPull>(pulls);

        public Task<AttributeCatalogPull> PullAsync(CancellationToken cancellationToken) => Task.FromResult(_pulls.Dequeue());
    }
}
