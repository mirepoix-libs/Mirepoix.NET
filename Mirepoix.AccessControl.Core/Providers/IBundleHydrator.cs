namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Turns an <see cref="AuthorizationRequest"/> into a hydrated <see cref="AuthorizationBundle"/>
/// for the kernel. This is the only hydration surface the local access checker depends on.
/// Implementations may compose per-concern resolvers or call out externally; failures should throw
/// so the checker can map them to <see cref="DecisionStatus.HydrationFailed"/>.
/// </summary>
public interface IBundleHydrator
{
    /// <summary>
    /// Completes partial subject/resource/context on <paramref name="request"/> into a bundle.
    /// Operation is typically passed through unchanged.
    /// </summary>
    /// <param name="request">Caller-known (possibly partial) check input.</param>
    /// <param name="cancellationToken">Cancellation for I/O-bound hydration.</param>
    /// <returns>Bundle ready for atom evaluation.</returns>
    Task<AuthorizationBundle> HydrateAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
