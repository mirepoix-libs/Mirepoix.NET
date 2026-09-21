namespace Mirepoix.AccessControl.Management;

/// <summary>
/// Writes ownership in memory under the hard-coded attribute key <c>ownerId</c>
/// on a shared resource attribute map (same map <see cref="Mirepoix.AccessControl.Providers.InMemoryResourceResolver"/> reads).
/// Creates attribute dictionaries as needed; copies immutable maps into mutable dictionaries on write.
/// </summary>
public sealed class InMemoryOwnershipHelper : IOwnershipHelper
{
    private const string OwnerAttributeKey = "ownerId";

    private readonly IDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> _resources;

    /// <summary>
    /// Creates a helper over a shared resource attribute map.
    /// </summary>
    /// <param name="resources">Mutable map of resource attributes keyed by (type, id).</param>
    public InMemoryOwnershipHelper(
        IDictionary<(string Type, string Id), IReadOnlyDictionary<string, object?>> resources)
    {
        _resources = resources;
    }

    /// <inheritdoc />
    public void SetOwner(string resourceType, string resourceId, string ownerSubjectId) =>
        MutableAttributes(resourceType, resourceId)[OwnerAttributeKey] = ownerSubjectId;

    /// <inheritdoc />
    public void ClearOwner(string resourceType, string resourceId)
    {
        if (!_resources.TryGetValue((resourceType, resourceId), out var attrs))
            return;

        MutableAttributes(resourceType, resourceId, attrs).Remove(OwnerAttributeKey);
    }

    private Dictionary<string, object?> MutableAttributes(
        string resourceType,
        string resourceId,
        IReadOnlyDictionary<string, object?>? existing = null)
    {
        var key = (resourceType, resourceId);
        if (existing is null)
            _resources.TryGetValue(key, out existing);

        if (existing is Dictionary<string, object?> mutable)
        {
            _resources[key] = mutable;
            return mutable;
        }

        mutable = existing is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(existing);
        _resources[key] = mutable;
        return mutable;
    }
}
