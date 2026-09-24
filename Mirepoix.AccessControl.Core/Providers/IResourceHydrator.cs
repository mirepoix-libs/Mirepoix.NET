namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Type-agnostic resource hydrate port for bundle hydration, remote PIP, and composite dispatch.
/// </summary>
public interface IResourceHydrator
{
    /// <summary>
    /// Returns a hydrated <see cref="Resource"/> for <paramref name="partial"/>.
    /// </summary>
    /// <param name="partial">Resource as supplied by the caller.</param>
    /// <param name="cancellationToken">Cancellation for I/O-bound lookup.</param>
    /// <returns>Complete resource used in the bundle.</returns>
    Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken);
}
