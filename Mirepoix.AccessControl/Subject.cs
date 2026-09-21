namespace Mirepoix.AccessControl;

/// <summary>
/// Identifies a principal under evaluation: stable id, set-valued roles, and a flat attribute bag.
/// Roles and attributes are separate: role membership atoms read <see cref="Roles"/>;
/// attribute atoms read <see cref="Attributes"/>. A value may appear in both if a provider
/// maps it that way; the kernel does not auto-mirror between them.
/// </summary>
/// <param name="Id">Names the stable subject identifier (e.g. user id / service principal id).</param>
/// <param name="Roles">Lists role names held by the subject. Empty set means no roles.</param>
/// <param name="Attributes">Holds a flat attribute dictionary. Keys are opaque strings; values are
/// comparison operands for policy atoms (<c>object?</c>, including null).</param>
public sealed record Subject(
    string Id,
    IReadOnlySet<string> Roles,
    IReadOnlyDictionary<string, object?> Attributes);
