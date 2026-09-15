using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Management;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

public class LocalCheckFlowTests
{
    [Fact]
    public async Task Check_allows_editor_on_owned_draft_doc()
    {
        var (mgr, checker) = WireSeededEditorDraft();

        var decision = await checker.CheckAsync(EditDoc1(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
        var hit = Assert.Single(decision.PolicyHits);
        Assert.Equal("editor-draft", hit.PolicyId);
        Assert.Equal(AuthorizationResult.Allow, hit.Effect);
        Assert.Contains("EDITOR", mgr.Assignments.GetRoles("u1"));
    }

    [Fact]
    public async Task Check_defaults_when_doc_is_not_draft()
    {
        var (_, checker) = WireSeededEditorDraft();
        var request = new AuthorizationRequest(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", "published", new Dictionary<string, object?>()),
            Operation.Parse("doc:edit"),
            EmptyContext());

        var decision = await checker.CheckAsync(request, CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, decision.Result);
        Assert.Equal(DecisionStatus.Defaulted, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
        Assert.Empty(decision.PolicyHits);
    }

    [Fact]
    public async Task Sod_conflict_leaves_editor_role_and_check_still_allows()
    {
        var (mgr, checker) = WireSeededEditorDraft();

        var conflict = mgr.Assignments.Assign("u1", "APPROVER");

        Assert.Equal(AssignmentOutcome.SodConflict, conflict.Outcome);
        Assert.Equal("sod-edit-approve", conflict.ConstraintId);
        Assert.Equal(new HashSet<string> { "EDITOR", "APPROVER" }, conflict.Roles!.ToHashSet());
        Assert.DoesNotContain("APPROVER", mgr.Assignments.GetRoles("u1"));

        var decision = await checker.CheckAsync(EditDoc1(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
    }

    [Fact]
    public async Task Policy_swap_changes_version_and_defaults_editor()
    {
        var (mgr, checker) = WireSeededEditorDraft();

        var before = await checker.CheckAsync(EditDoc1(), CancellationToken.None);
        Assert.Equal(AuthorizationResult.Allow, before.Result);
        Assert.Equal("v1", before.PolicySetVersion);

        mgr.Policies.Replace(new PolicySet("v2", Array.Empty<Policy>()));

        var after = await checker.CheckAsync(EditDoc1(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, after.Result);
        Assert.Equal(DecisionStatus.Defaulted, after.Status);
        Assert.Equal("v2", after.PolicySetVersion);
        Assert.Empty(after.PolicyHits);
    }

    private static (InMemoryAccessManager Mgr, LocalAccessChecker Checker) WireSeededEditorDraft()
    {
        var mgr = InMemoryAccessManager.CreateEmpty();
        mgr.RoleCatalog.Add(new Role("EDITOR", "edits drafts"));
        mgr.RoleCatalog.Add(new Role("APPROVER", "approves"));
        mgr.SodConstraints.Add(new SodConstraint(
            "sod-edit-approve",
            new HashSet<string> { "EDITOR", "APPROVER" }));
        Assert.Equal(AssignmentOutcome.Assigned, mgr.Assignments.Assign("u1", "EDITOR").Outcome);
        mgr.Ownership.SetOwner("doc", "1", "u1");
        mgr.Labels.SetResourceLabel("doc", "1", "status", "draft");
        mgr.Ownership.SetOwner("doc", "published", "u1");
        mgr.Labels.SetResourceLabel("doc", "published", "status", "published");
        mgr.Policies.Replace(EditorDraftSet("v1"));

        var hydrator = new CompositeBundleHydrator(mgr.SubjectResolver, mgr.ResourceResolver);
        var checker = new LocalAccessChecker(mgr.PolicySource, hydrator);
        return (mgr, checker);
    }

    private static PolicySet EditorDraftSet(string version) =>
        new(
            version,
            new[]
            {
                new Policy(
                    "editor-draft",
                    AuthorizationResult.Allow,
                    "EDITOR may edit owned draft docs",
                    new IAtom[]
                    {
                        new RoleMembershipAtom(new[] { "EDITOR" }),
                        new AttributeValueAtom(
                            AttributeTarget.Resource,
                            "status",
                            ComparisonOperator.Equals,
                            "draft"),
                        new SubjectIdEqualsAttributeAtom(AttributeTarget.Resource, "ownerId"),
                        new OperationMatchAtom(Operation.Parse("doc:edit"))
                    })
            });

    private static AuthorizationRequest EditDoc1() =>
        new(
            new Subject("u1", new HashSet<string>(), new Dictionary<string, object?>()),
            new Resource("doc", "1", new Dictionary<string, object?>()),
            Operation.Parse("doc:edit"),
            EmptyContext());

    private static AccessContext EmptyContext() =>
        new(null, new Dictionary<string, object?>(), new Dictionary<string, object?>());
}
