namespace Mirepoix.AccessControl;

/// <summary>
/// Evaluates an access check for edge and central topologies.
/// Implementations hydrate (or remotely evaluate) then return an <see cref="AccessDecision"/>.
/// <see cref="AuthorizationResult.Deny"/> alone does not distinguish policy deny, default deny, or infrastructure failure.
/// Callers should inspect both <see cref="AccessDecision.Result"/> and <see cref="AccessDecision.Status"/>.
/// </summary>
public interface IAccessChecker
{
    /// <summary>
    /// Evaluates <paramref name="request"/> and returns the full decision (effect, hits, status, version).
    /// Partial subject/resource/context on the request are completed by the implementation's hydrator
    /// (or by a remote service); the kernel never sees the unhydrated request.
    /// </summary>
    /// <param name="request">Caller-known subject, resource, operation, and context (may be partial).</param>
    /// <param name="cancellationToken">Cancels hydration / remote calls; kernel evaluation itself is sync and pure.</param>
    /// <returns>
    /// An <see cref="AccessDecision"/>. Failure paths still return a decision (typically
    /// <see cref="AuthorizationResult.Deny"/> with a non-<see cref="DecisionStatus.Success"/> status),
    /// rather than throwing for expected provider failures; exact mapping is implementation-defined.
    /// </returns>
    Task<AccessDecision> CheckAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
