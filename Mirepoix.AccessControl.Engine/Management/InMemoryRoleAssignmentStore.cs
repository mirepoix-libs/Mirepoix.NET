namespace Mirepoix.AccessControl.Management;

public sealed class InMemoryRoleAssignmentStore : IRoleAssignmentStore
{
    private readonly ISodConstraintStore _sod;
    private readonly IDictionary<string, Subject> _subjects;
    private readonly Dictionary<string, HashSet<string>> _assignments = new();

    public InMemoryRoleAssignmentStore(
        ISodConstraintStore sodConstraints,
        IDictionary<string, Subject> subjects)
    {
        _sod = sodConstraints;
        _subjects = subjects;
    }

    public AssignmentResult Assign(string subjectId, string roleId)
    {
        if (!_assignments.TryGetValue(subjectId, out var current))
            current = new HashSet<string>();
        else if (current.Contains(roleId))
            return AssignmentResult.Assigned();

        var proposed = new HashSet<string>(current) { roleId };
        foreach (var constraint in _sod.List())
        {
            var overlap = proposed.Intersect(constraint.MutuallyExclusiveRoles).ToList();
            if (overlap.Count >= 2)
                return AssignmentResult.SodConflict(constraint.Id, overlap);
        }

        if (!_assignments.ContainsKey(subjectId))
            _assignments[subjectId] = current;

        current.Add(roleId);
        SyncSubject(subjectId, current);
        return AssignmentResult.Assigned();
    }

    public void Revoke(string subjectId, string roleId)
    {
        if (!_assignments.TryGetValue(subjectId, out var current))
            return;

        current.Remove(roleId);
        SyncSubject(subjectId, current);
    }

    public IReadOnlySet<string> GetRoles(string subjectId) =>
        _assignments.TryGetValue(subjectId, out var roles)
            ? new HashSet<string>(roles)
            : new HashSet<string>();

    private void SyncSubject(string subjectId, IReadOnlySet<string> roles)
    {
        if (!_subjects.TryGetValue(subjectId, out var current))
        {
            _subjects[subjectId] = new Subject(
                subjectId,
                new HashSet<string>(roles),
                new Dictionary<string, object?>());
            return;
        }

        _subjects[subjectId] = current with { Roles = new HashSet<string>(roles) };
    }
}
