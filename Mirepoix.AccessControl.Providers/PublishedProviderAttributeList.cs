using Mirepoix.AccessControl.Policy;

namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Holds explicit subject and resource keys from <c>AddAttribute</c>.
/// Duplicate <c>(target, type, key)</c> rows are ignored. Context is rejected.
/// </summary>
public sealed class PublishedProviderAttributeList
{
    private readonly List<PublishedAttribute> _items = new();

    /// <summary>Gets extras in insertion order, including a <c>time</c> key if one was added.</summary>
    public IReadOnlyList<PublishedAttribute> Items => _items;

    /// <summary>
    /// Adds one extra key. Subject type defaults to <c>subject</c> when <paramref name="type"/> is null.
    /// </summary>
    /// <param name="target">Subject or resource.</param>
    /// <param name="key">Non-blank bundle attribute key.</param>
    /// <param name="type">Catalog type. Required for resource. Null on subject becomes <c>subject</c>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="key"/> is blank, <paramref name="target"/> is context,
    /// a provided subject type is blank, or resource type is missing.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="target"/> is not a known value.</exception>
    public void Add(AttributeTarget target, string key, string? type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        switch (target)
        {
            case AttributeTarget.Context:
                throw new ArgumentException(
                    "Provider attribute catalog cannot publish context attributes.",
                    nameof(target));
            case AttributeTarget.Subject:
                if (type is null)
                    type = "subject";
                else if (string.IsNullOrWhiteSpace(type))
                    throw new ArgumentException("Subject attribute type must be non-blank when provided.", nameof(type));
                break;
            case AttributeTarget.Resource:
                if (string.IsNullOrWhiteSpace(type))
                    throw new ArgumentException("Resource attributes require a type.", nameof(type));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown attribute target.");
        }

        if (Contains(target, type, key))
            return;

        _items.Add(new PublishedAttribute(target, type, key));
    }

    private bool Contains(AttributeTarget target, string? type, string key)
    {
        foreach (var existing in _items)
        {
            if (existing.Target == target &&
                string.Equals(existing.Type, type, StringComparison.Ordinal) &&
                string.Equals(existing.Key, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
