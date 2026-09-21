namespace Mirepoix.AccessControl;

/// <summary>
/// Carries the fully hydrated evaluation input that the kernel consumes.
/// Produced by an <c>IBundleHydrator</c> (or equivalent) from an <see cref="AuthorizationRequest"/>.
/// Same four fields as the request; semantically complete for atom evaluation (missing attributes
/// are still allowed and are handled per-atom rules in the Engine).
/// </summary>
/// <param name="Subject">Supplies the hydrated subject (id, roles, attributes).</param>
/// <param name="Resource">Supplies the hydrated resource (type, id, attributes).</param>
/// <param name="Operation">Supplies the operation under evaluation (unchanged from the request in the default hydrator).</param>
/// <param name="Context">Supplies the hydrated context (time, claims, app values).</param>
public sealed record AuthorizationBundle(
    Subject Subject,
    Resource Resource,
    Operation Operation,
    AccessContext Context);
