namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Returns the caller-supplied <see cref="AccessContext"/> unchanged (no-op enrichment).
/// Useful when time/claims/values are already complete on the request.
/// </summary>
public sealed class PassThroughContextResolver : IContextResolver
{
    /// <summary>
    /// Returns <paramref name="partial"/> as-is.
    /// </summary>
    /// <param name="partial">Context from the request.</param>
    /// <param name="cancellationToken">Unused.</param>
    /// <returns>The same context instance/value.</returns>
    public Task<AccessContext> HydrateAsync(AccessContext partial, CancellationToken cancellationToken) =>
        Task.FromResult(partial);
}
