namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Fills subject roles/attributes from a partial subject (often id-only).
/// </summary>
public interface ISubjectResolver
{
    /// <summary>
    /// Returns a hydrated <see cref="Subject"/> for <paramref name="partial"/>.
    /// </summary>
    /// <param name="partial">Subject as supplied by the caller (at least <see cref="Subject.Id"/>).</param>
    /// <param name="cancellationToken">Cancellation for I/O-bound lookup.</param>
    /// <returns>Complete subject used in the bundle.</returns>
    Task<Subject> HydrateAsync(Subject partial, CancellationToken cancellationToken);
}
