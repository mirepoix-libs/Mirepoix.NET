namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Writes attribute labels in memory over shared subject and resource maps used by resolvers.
/// Creates missing subjects/resources on set; clear is a no-op when the entity is absent.
/// Copies immutable attribute dictionaries into mutable ones on first write.
/// </summary>
public sealed class InMemoryLabelHelper : ILabelHelper
{
    private readonly IDictionary<string, Subject> _subjects;
    private readonly IDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> _resources;

    /// <summary>
    /// Creates a helper over shared subject and resource maps.
    /// </summary>
    /// <param name="subjects">Mutable subject map.</param>
    /// <param name="resources">Mutable resource attribute map.</param>
    public InMemoryLabelHelper(
        IDictionary<string, Subject> subjects,
        IDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> resources)
    {
        _subjects = subjects;
        _resources = resources;
    }

    /// <inheritdoc />
    public void SetSubjectLabel(string subjectId, string key, object? value) =>
        MutableSubjectAttributes(subjectId)[key] = value;

    /// <inheritdoc />
    public void ClearSubjectLabel(string subjectId, string key)
    {
        if (!_subjects.ContainsKey(subjectId))
            return;

        MutableSubjectAttributes(subjectId).Remove(key);
    }

    /// <inheritdoc />
    public void SetResourceLabel(string resourceType, string resourceId, string key, object? value) =>
        MutableResourceAttributes(resourceType, resourceId)[key] = value;

    /// <inheritdoc />
    public void ClearResourceLabel(string resourceType, string resourceId, string key)
    {
        if (!_resources.ContainsKey((resourceType, resourceId)))
            return;

        MutableResourceAttributes(resourceType, resourceId).Remove(key);
    }

    /// <inheritdoc />
    public Task SetSubjectLabelAsync(string subjectId, string key, object? value, CancellationToken cancellationToken = default)
    {
        SetSubjectLabel(subjectId, key, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearSubjectLabelAsync(string subjectId, string key, CancellationToken cancellationToken = default)
    {
        ClearSubjectLabel(subjectId, key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetResourceLabelAsync(string resourceType, string resourceId, string key, object? value, CancellationToken cancellationToken = default)
    {
        SetResourceLabel(resourceType, resourceId, key, value);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearResourceLabelAsync(string resourceType, string resourceId, string key, CancellationToken cancellationToken = default)
    {
        ClearResourceLabel(resourceType, resourceId, key);
        return Task.CompletedTask;
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
