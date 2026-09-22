using Mirepoix.AccessControl;
using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

public class LocalCheckFlowTests
{
    [Fact]
    public async Task Check_allows_editor_on_owned_draft_doc()
    {
        var (subjects, _, checker) = WireSeededEditorDraft();

        var decision = await checker.CheckAsync(EditDoc1(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Allow, decision.Result);
        Assert.Equal(DecisionStatus.Success, decision.Status);
        Assert.Equal("v1", decision.PolicySetVersion);
        var hit = Assert.Single(decision.PolicyHits);
        Assert.Equal("editor-draft", hit.PolicyId);
        Assert.Equal(AuthorizationResult.Allow, hit.Effect);
        Assert.Contains("EDITOR", subjects["u1"].Roles);
    }

    [Fact]
    public async Task Check_defaults_when_doc_is_not_draft()
    {
        var (_, _, checker) = WireSeededEditorDraft();
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
    public async Task Policy_swap_changes_version_and_defaults_editor()
    {
        var (_, policySource, checker) = WireSeededEditorDraft();

        var before = await checker.CheckAsync(EditDoc1(), CancellationToken.None);
        Assert.Equal(AuthorizationResult.Allow, before.Result);
        Assert.Equal("v1", before.PolicySetVersion);

        policySource.Replace(new PolicySet("v2", Array.Empty<Policy>()));

        var after = await checker.CheckAsync(EditDoc1(), CancellationToken.None);

        Assert.Equal(AuthorizationResult.Deny, after.Result);
        Assert.Equal(DecisionStatus.Defaulted, after.Status);
        Assert.Equal("v2", after.PolicySetVersion);
        Assert.Empty(after.PolicyHits);
    }

    private static (
        IReadOnlyDictionary<string, Subject> Subjects,
        MemoryPolicySource PolicySource,
        LocalAccessChecker Checker) WireSeededEditorDraft()
    {
        IReadOnlyDictionary<string, Subject> subjects = new Dictionary<string, Subject>
        {
            ["u1"] = new(
                "u1",
                new HashSet<string> { "EDITOR" },
                new Dictionary<string, object?>()),
        };
        IReadOnlyDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> resources =
            new Dictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>>
            {
                [("doc", "1")] = new Dictionary<string, object?>
                {
                    ["ownerId"] = "u1",
                    ["status"] = "draft",
                },
                [("doc", "published")] = new Dictionary<string, object?>
                {
                    ["ownerId"] = "u1",
                    ["status"] = "published",
                },
            };
        var policySource = new MemoryPolicySource(EditorDraftSet("v1"));
        var hydrator = new CompositeBundleHydrator(
            new InMemorySubjectResolver(subjects),
            new InMemoryResourceResolver(resources));
        var checker = new LocalAccessChecker(policySource, hydrator);
        return (subjects, policySource, checker);
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
