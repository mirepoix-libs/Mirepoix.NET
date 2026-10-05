using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;

public class AttributeCatalogSnapshotTests
{
    [Fact]
    public void Apply_complete_pull_unions_provider_and_context_rows_and_failed_pull_keeps_union()
    {
        var snapshot = new AttributeCatalogSnapshot();
        var complete = new AttributeCatalogPull(
            [
                new AttributeAppCatalog("identity",
                [
                    new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
                    new PublishedAttribute(AttributeTarget.Subject, "employee", "dept"),
                    new PublishedAttribute(AttributeTarget.Context, null, "time"),
                    new PublishedAttribute(AttributeTarget.Resource, "invoice", "status"),
                ]),
                new AttributeAppCatalog("directory",
                [
                    new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
                    new PublishedAttribute(AttributeTarget.Resource, "invoice", "ownerId"),
                ]),
            ],
            [
                new AttributeAppCatalog("billing",
                [
                    new PublishedAttribute(AttributeTarget.Context, "ignored", "time"),
                    new PublishedAttribute(AttributeTarget.Context, null, "tenant"),
                    new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
                ]),
            ],
            []);

        snapshot.Apply(complete);

        Assert.True(snapshot.HasSnapshot);
        Assert.Equal(
            [
                new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
                new PublishedAttribute(AttributeTarget.Subject, "employee", "dept"),
                new PublishedAttribute(AttributeTarget.Resource, "invoice", "status"),
                new PublishedAttribute(AttributeTarget.Resource, "invoice", "ownerId"),
                new PublishedAttribute(AttributeTarget.Context, null, "time"),
                new PublishedAttribute(AttributeTarget.Context, null, "tenant"),
            ],
            snapshot.Attributes);
        Assert.Same(complete, snapshot.LastPull);

        var failed = new AttributeCatalogPull(
            [new AttributeAppCatalog("identity", [new PublishedAttribute(AttributeTarget.Subject, "subject", "title")])],
            [],
            ["billing"]);

        snapshot.Apply(failed);

        Assert.True(snapshot.HasSnapshot);
        Assert.Equal("dept", snapshot.Attributes[0].Key);
        Assert.DoesNotContain(snapshot.Attributes, row => row.Key == "title");
        Assert.Same(failed, snapshot.LastPull);
    }

    [Fact]
    public void Apply_empty_union_sets_snapshot()
    {
        var snapshot = new AttributeCatalogSnapshot();

        snapshot.Apply(new AttributeCatalogPull([], [], []));

        Assert.True(snapshot.HasSnapshot);
        Assert.Empty(snapshot.Attributes);
    }

    [Fact]
    public void Apply_with_failures_before_a_snapshot_stores_last_pull_only()
    {
        var snapshot = new AttributeCatalogSnapshot();
        var pull = new AttributeCatalogPull(
            [new AttributeAppCatalog("identity", [new PublishedAttribute(AttributeTarget.Subject, "subject", "dept")])],
            [],
            ["billing"]);

        snapshot.Apply(pull);

        Assert.False(snapshot.HasSnapshot);
        Assert.Empty(snapshot.Attributes);
        Assert.Same(pull, snapshot.LastPull);
    }

    [Fact]
    public void SelectAttributes_uses_pull_when_complete_stored_union_when_partial_and_throws_when_missing()
    {
        var snapshot = new AttributeCatalogSnapshot();
        var complete = new AttributeCatalogPull(
            [new AttributeAppCatalog("identity", [new PublishedAttribute(AttributeTarget.Subject, "subject", "dept")])],
            [new AttributeAppCatalog("billing", [new PublishedAttribute(AttributeTarget.Context, null, "time")])],
            []);

        var selected = snapshot.SelectAttributes(complete);

        Assert.False(snapshot.HasSnapshot);
        Assert.Null(snapshot.LastPull);
        Assert.Equal(
            [
                new PublishedAttribute(AttributeTarget.Subject, "subject", "dept"),
                new PublishedAttribute(AttributeTarget.Context, null, "time"),
            ],
            selected);

        snapshot.Apply(complete);
        var partial = new AttributeCatalogPull([], [], ["invoices"]);

        Assert.Equal(snapshot.Attributes, snapshot.SelectAttributes(partial));
        Assert.Same(complete, snapshot.LastPull);

        var empty = new AttributeCatalogSnapshot();
        var ex = Assert.Throws<AttributeCatalogUnavailableException>(() => empty.SelectAttributes(partial));
        Assert.IsAssignableFrom<InvalidOperationException>(ex);
        Assert.False(empty.HasSnapshot);
    }
}
