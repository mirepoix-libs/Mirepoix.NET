using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Management;

public sealed class InMemoryAccessManager
{
    private InMemoryAccessManager(
        Dictionary<string, Subject> subjects,
        Dictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> resources,
        MemoryPolicySource policySource)
    {
        RoleCatalog = new InMemoryRoleCatalog();
        SodConstraints = new InMemorySodConstraintStore();
        Assignments = new InMemoryRoleAssignmentStore(SodConstraints, subjects);
        Ownership = new InMemoryOwnershipHelper(resources);
        Labels = new InMemoryLabelHelper(subjects, resources);
        Policies = new InMemoryPolicySetEditor(policySource);
        PolicySource = policySource;
        SubjectResolver = new InMemorySubjectResolver(subjects);
        ResourceResolver = new InMemoryResourceResolver(resources);
    }

    public static InMemoryAccessManager CreateEmpty() =>
        new(
            new Dictionary<string, Subject>(),
            new Dictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>>(),
            new MemoryPolicySource(new PolicySet("empty", Array.Empty<Policy.Policy>())));

    public IRoleCatalog RoleCatalog { get; }

    public IRoleAssignmentStore Assignments { get; }

    public ISodConstraintStore SodConstraints { get; }

    public IOwnershipHelper Ownership { get; }

    public ILabelHelper Labels { get; }

    public IPolicySetEditor Policies { get; }

    public MemoryPolicySource PolicySource { get; }

    public InMemorySubjectResolver SubjectResolver { get; }

    public InMemoryResourceResolver ResourceResolver { get; }
}
