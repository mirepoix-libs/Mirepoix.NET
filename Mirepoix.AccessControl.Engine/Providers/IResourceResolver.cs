namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Fills resource attributes from a partial resource (type + id).
/// </summary>
public interface IResourceResolver
{
    /// <summary>
    /// Returns a hydrated <see cref="Resource"/> for <paramref name="partial"/>.
    /// </summary>
    /// <param name="partial">Resource as supplied by the caller (at least type and id).</param>
    /// <param name="cancellationToken">Cancellation for I/O-bound lookup.</param>
    /// <returns>Complete resource used in the bundle.</returns>
    Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken);
}
