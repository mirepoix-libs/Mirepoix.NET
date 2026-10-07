namespace Mirepoix.AccessControl;

/// <summary>
/// Identifies a resource under evaluation: type + key identity and a flat attribute bag.
/// Type/key are the lookup key for hydrators; attributes feed attribute and ownership atoms.
/// </summary>
/// <param name="Type">Names the resource type (e.g. <c>document</c>). Opaque to the kernel.</param>
/// <param name="Key">Names the resource instance within <paramref name="Type"/>.</param>
/// <param name="Attributes">Flat attribute dictionary for policy atoms.</param>
public sealed record Resource(
    string Type,
    ResourceKey Key,
    IReadOnlyDictionary<string, object?> Attributes)
{
    /// <summary>
    /// Legacy single-id view. Prefer <see cref="Key"/>.
    /// Single part named <c>id</c> returns that value; otherwise <see cref="ResourceKey.Canonical"/>.
    /// </summary>
    [Obsolete("Use Key. Prefer ResourceKey.Single for single-part identity.")]
    public string Id =>
        Key.Parts.Count == 1 && Key.TryGet("id", out var id) ? id : Key.Canonical();
}
