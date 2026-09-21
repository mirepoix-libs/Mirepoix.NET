namespace Mirepoix.AccessControl.Providers;

/// <summary>
/// Hydrates the request by running optional subject/resource/context resolvers in parallel.
/// A null resolver means pass-through of that request part. Operation is always taken from the request.
/// Any resolver fault fails the whole <see cref="HydrateAsync"/> (and thus the checker's hydration path).
/// </summary>
public sealed class CompositeBundleHydrator : IBundleHydrator
{
    private readonly ISubjectResolver? _subject;
    private readonly IResourceResolver? _resource;
    private readonly IContextResolver? _context;

    /// <summary>
    /// Creates a composite hydrator. Omitted resolvers leave that part of the request unchanged.
    /// </summary>
    /// <param name="subject">Optional subject hydrator.</param>
    /// <param name="resource">Optional resource hydrator.</param>
    /// <param name="context">Optional context hydrator.</param>
    public CompositeBundleHydrator(
        ISubjectResolver? subject = null,
        IResourceResolver? resource = null,
        IContextResolver? context = null)
    {
        _subject = subject;
        _resource = resource;
        _context = context;
    }

    /// <summary>
    /// Runs present resolvers concurrently via Task.WhenAll, then builds the bundle.
    /// </summary>
    /// <param name="request">Incoming partial request.</param>
    /// <param name="cancellationToken">Shared cancellation for all resolvers.</param>
    /// <returns>Hydrated bundle with <see cref="AuthorizationRequest.Operation"/> unchanged.</returns>
    public async Task<AuthorizationBundle> HydrateAsync(
        AuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        var subjectTask = _subject is null
            ? Task.FromResult(request.Subject)
            : _subject.HydrateAsync(request.Subject, cancellationToken);
        var resourceTask = _resource is null
            ? Task.FromResult(request.Resource)
            : _resource.HydrateAsync(request.Resource, cancellationToken);
        var contextTask = _context is null
            ? Task.FromResult(request.Context)
            : _context.HydrateAsync(request.Context, cancellationToken);

        await Task.WhenAll(subjectTask, resourceTask, contextTask).ConfigureAwait(false);

        return new AuthorizationBundle(
            await subjectTask.ConfigureAwait(false),
            await resourceTask.ConfigureAwait(false),
            request.Operation,
            await contextTask.ConfigureAwait(false));
    }
}
