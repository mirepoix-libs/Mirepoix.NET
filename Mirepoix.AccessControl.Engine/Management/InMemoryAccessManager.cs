using Mirepoix.AccessControl.Policy;
using Mirepoix.AccessControl.Providers;

namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Aggregates in-memory management with matching resolvers and policy source.
/// Shares one subject map and one resource map across assignments, ownership, labels, and resolvers
/// so writes are visible on the next hydrate. Off the check hot path; wire into a checker separately.
/// </summary>
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

    /// <summary>
    /// Creates an empty manager: empty subject/resource maps and a policy set versioned <c>empty</c>
    /// with no policies.
    /// </summary>
    public static InMemoryAccessManager CreateEmpty() =>
        new(
            new Dictionary<string, Subject>(),
            new Dictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>>(),
            new MemoryPolicySource(new PolicySet("empty", Array.Empty<Policy.Policy>())));

    /// <summary>Holds the role definitions catalog.</summary>
    public IRoleCatalog RoleCatalog { get; }

    /// <summary>Holds subject role assignments (SoD-aware).</summary>
    public IRoleAssignmentStore Assignments { get; }

    /// <summary>Holds SoD constraints used by <see cref="Assignments"/>.</summary>
    public ISodConstraintStore SodConstraints { get; }

    /// <summary>Writes resource ownership attributes.</summary>
    public IOwnershipHelper Ownership { get; }

    /// <summary>Writes subject/resource attribute labels.</summary>
    public ILabelHelper Labels { get; }

    /// <summary>Edits the whole policy set over <see cref="PolicySource"/>.</summary>
    public IPolicySetEditor Policies { get; }

    /// <summary>Holds the policy source to pass to <see cref="LocalAccessChecker"/>.</summary>
    public MemoryPolicySource PolicySource { get; }

    /// <summary>Resolves subjects from the shared subject map.</summary>
    public InMemorySubjectResolver SubjectResolver { get; }

    /// <summary>Resolves resources from the shared resource map.</summary>
    public InMemoryResourceResolver ResourceResolver { get; }
}
