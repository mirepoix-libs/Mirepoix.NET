namespace Mirepoix.AccessControl.Management;

public sealed class InMemoryLabelHelper : ILabelHelper
{
    private readonly IDictionary<string, Subject> _subjects;
    private readonly IDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> _resources;

    public InMemoryLabelHelper(
        IDictionary<string, Subject> subjects,
        IDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> resources)
    {
        _subjects = subjects;
        _resources = resources;
    }

    public void SetSubjectLabel(string subjectId, string key, object? value) =>
        MutableSubjectAttributes(subjectId)[key] = value;

    public void ClearSubjectLabel(string subjectId, string key)
    {
        if (!_subjects.ContainsKey(subjectId))
            return;

        MutableSubjectAttributes(subjectId).Remove(key);
    }

    public void SetResourceLabel(string resourceType, string resourceId, string key, object? value) =>
        MutableResourceAttributes(resourceType, resourceId)[key] = value;

    public void ClearResourceLabel(string resourceType, string resourceId, string key)
    {
        if (!_resources.ContainsKey((resourceType, resourceId)))
            return;

        MutableResourceAttributes(resourceType, resourceId).Remove(key);
    }

    private Dictionary<string, object?> MutableSubjectAttributes(string subjectId)
    {
        if (!_subjects.TryGetValue(subjectId, out var current))
        {
            var attrs = new Dictionary<string, object?>();
            _subjects[subjectId] = new Subject(subjectId, new HashSet<string>(), attrs);
            return attrs;
        }

        if (current.Attributes is Dictionary<string, object?> mutable)
            return mutable;

        mutable = new Dictionary<string, object?>(current.Attributes);
        _subjects[subjectId] = current with { Attributes = mutable };
        return mutable;
    }

    private Dictionary<string, object?> MutableResourceAttributes(string resourceType, string resourceId)
    {
        var key = (resourceType, resourceId);
        if (_resources.TryGetValue(key, out var existing) && existing is Dictionary<string, object?> mutable)
            return mutable;

        mutable = existing is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(existing);
        _resources[key] = mutable;
        return mutable;
    }
}
