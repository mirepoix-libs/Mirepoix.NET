namespace Mirepoix.AccessControl;

/// <summary>
/// Carries facade input: whatever the caller already knows about the check.
/// Shape matches <see cref="AuthorizationBundle"/>, but values may be partial.
/// Hydrators fill roles, attributes, claims, etc. before the kernel runs.
/// The kernel never consumes this type directly; only a hydrated <see cref="AuthorizationBundle"/>.
/// </summary>
/// <param name="Subject">Supplies subject identity and any roles/attributes already known (often id-only).</param>
/// <param name="Resource">Supplies resource type/id and any attributes already known.</param>
/// <param name="Operation">Supplies the operation being authorized; typically fully known at the call site.</param>
/// <param name="Context">Supplies time, claims, and app values already known (callers often set time and claims).</param>
public sealed record AuthorizationRequest(
    Subject Subject,
    Resource Resource,
    Operation Operation,
    AccessContext Context);
