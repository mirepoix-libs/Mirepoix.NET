using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;

public class OperationCatalogSnapshotTests
{
    [Fact]
    public void Apply_complete_pull_sets_union_and_failed_pull_updates_only_last_pull()
    {
        var snapshot = new OperationCatalogSnapshot();
        var complete = new OperationCatalogPull(
            [
                new EnforcementAppCatalog("billing", [new PublishedOperation("a:b", "", "")]),
                new EnforcementAppCatalog("invoices", [new PublishedOperation("a:b:c", "invoices/{id}", "POST")]),
            ],
            []);

        snapshot.Apply(complete);

        Assert.True(snapshot.HasSnapshot);
        Assert.Equal(3, snapshot.MaxSegments);
        Assert.Equal(["a:b", "a:b:c"], snapshot.OperationValues);
        Assert.Same(complete, snapshot.LastPull);

        var failed = new OperationCatalogPull(
            [new EnforcementAppCatalog("billing", [new PublishedOperation("a:b:c:d", "", "")])],
            ["invoices"]);

        snapshot.Apply(failed);

        Assert.True(snapshot.HasSnapshot);
        Assert.Equal(3, snapshot.MaxSegments);
        Assert.Equal(["a:b", "a:b:c"], snapshot.OperationValues);
        Assert.Same(failed, snapshot.LastPull);
    }

    [Fact]
    public void Apply_empty_union_sets_max_segments_to_zero()
    {
        var snapshot = new OperationCatalogSnapshot();

        snapshot.Apply(new OperationCatalogPull([], []));

        Assert.True(snapshot.HasSnapshot);
        Assert.Equal(0, snapshot.MaxSegments);
        Assert.Empty(snapshot.OperationValues);
    }

    [Fact]
    public void Apply_with_failures_before_a_snapshot_stores_last_pull_only()
    {
        var snapshot = new OperationCatalogSnapshot();
        var pull = new OperationCatalogPull(
            [new EnforcementAppCatalog("billing", [new PublishedOperation("a:b", "", "")])],
            ["invoices"]);

        snapshot.Apply(pull);

        Assert.False(snapshot.HasSnapshot);
        Assert.Equal(0, snapshot.MaxSegments);
        Assert.Empty(snapshot.OperationValues);
        Assert.Same(pull, snapshot.LastPull);
    }
}
