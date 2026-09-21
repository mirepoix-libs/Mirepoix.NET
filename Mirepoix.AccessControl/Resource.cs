namespace Mirepoix.AccessControl;

/// <summary>
/// Identifies a resource under evaluation: type + id identity and a flat attribute bag.
/// Type/id are the lookup key for hydrators; attributes feed attribute and ownership atoms.
/// </summary>
/// <param name="Type">Names the resource type (e.g. <c>document</c>). Opaque to the kernel; matching is string equality in atoms/hydrators.</param>
/// <param name="Id">Names the resource instance id within <paramref name="Type"/>.</param>
/// <param name="Attributes">Holds a flat attribute dictionary. Keys are opaque strings; values are
/// comparison operands for policy atoms (<c>object?</c>, including null).</param>
public sealed record Resource(
    string Type,
    string Id,
    IReadOnlyDictionary<string, object?> Attributes);
