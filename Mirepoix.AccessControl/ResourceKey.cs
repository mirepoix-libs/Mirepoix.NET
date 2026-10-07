namespace Mirepoix.AccessControl;

/// <summary>One named part of a <see cref="ResourceKey"/>.</summary>
/// <param name="Name">Part name (ordinal).</param>
/// <param name="Value">Non-blank part value.</param>
public readonly record struct ResourceKeyPart(string Name, string Value);

/// <summary>
/// Named resource identity parts. Equality is order-independent; storage order is name-sorted ordinal.
/// </summary>
public readonly struct ResourceKey : IEquatable<ResourceKey>
{
    private readonly ResourceKeyPart[] _parts;

    private ResourceKey(ResourceKeyPart[] parts) => _parts = parts;

    /// <summary>Gets a key with no parts.</summary>
    public static ResourceKey Empty { get; } = new(Array.Empty<ResourceKeyPart>());

    /// <summary>Gets whether this key has no parts.</summary>
    public bool IsEmpty => _parts is null || _parts.Length == 0;

    /// <summary>Gets parts in sorted name order.</summary>
    public IReadOnlyList<ResourceKeyPart> Parts => _parts ?? Array.Empty<ResourceKeyPart>();

    /// <summary>Creates a single-part key named <c>id</c>.</summary>
    public static ResourceKey Single(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return From(("id", id));
    }

    /// <summary>Creates a key from named parts. Duplicate or blank names/values throw.</summary>
    public static ResourceKey From(params (string name, string value)[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Length == 0)
            return Empty;

        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in parts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (!map.TryAdd(name, value))
                throw new ArgumentException($"Duplicate resource key part '{name}'.", nameof(parts));
        }

        return new ResourceKey(map.Select(p => new ResourceKeyPart(p.Key, p.Value)).ToArray());
    }

    /// <summary>Creates a key from a dictionary of parts. Blank names/values are skipped; empty result is <see cref="Empty"/>.</summary>
    public static ResourceKey From(IEnumerable<KeyValuePair<string, string>> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var list = new List<(string, string)>();
        foreach (var pair in parts)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                continue;
            list.Add((pair.Key, pair.Value));
        }

        return list.Count == 0 ? Empty : From(list.ToArray());
    }

    /// <summary>Tries to read the value for <paramref name="name"/>.</summary>
    public bool TryGet(string name, out string value)
    {
        foreach (var part in Parts)
        {
            if (string.Equals(part.Name, name, StringComparison.Ordinal))
            {
                value = part.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    /// <summary>Returns the value for <paramref name="name"/> or throws <see cref="KeyNotFoundException"/>.</summary>
    public string GetRequired(string name)
    {
        if (TryGet(name, out var value))
            return value;
        throw new KeyNotFoundException($"Resource key part '{name}' was not found.");
    }

    /// <summary>Stable log form: <c>name=value</c> segments joined by <c>;</c>.</summary>
    public string Canonical()
    {
        if (IsEmpty)
            return string.Empty;
        return string.Join(';', Parts.Select(p => $"{p.Name}={p.Value}"));
    }

    /// <inheritdoc />
    public bool Equals(ResourceKey other)
    {
        var left = Parts;
        var right = other.Parts;
        if (left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i].Name, right[i].Name, StringComparison.Ordinal)
                || !string.Equals(left[i].Value, right[i].Value, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ResourceKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var part in Parts)
        {
            hash.Add(part.Name, StringComparer.Ordinal);
            hash.Add(part.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(ResourceKey left, ResourceKey right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(ResourceKey left, ResourceKey right) => !left.Equals(right);
}
