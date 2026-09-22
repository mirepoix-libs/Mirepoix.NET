namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Completes <see cref="AccessContext"/> (claims, values, time) beyond what the caller supplied.
/// </summary>
public interface IContextResolver
{
    /// <summary>
    /// Returns a hydrated <see cref="AccessContext"/> for <paramref name="partial"/>.
    /// </summary>
    /// <param name="partial">Context as supplied by the caller.</param>
    /// <param name="cancellationToken">Cancellation for I/O-bound enrichment.</param>
    /// <returns>Complete context used in the bundle.</returns>
    Task<AccessContext> HydrateAsync(AccessContext partial, CancellationToken cancellationToken);
}
