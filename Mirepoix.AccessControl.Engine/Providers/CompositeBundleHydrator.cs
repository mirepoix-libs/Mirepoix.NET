namespace Mirepoix.AccessControl.Providers;

public sealed class CompositeBundleHydrator : IBundleHydrator
{
    private readonly ISubjectResolver? _subject;
    private readonly IResourceResolver? _resource;
    private readonly IContextResolver? _context;

    public CompositeBundleHydrator(
        ISubjectResolver? subject = null,
        IResourceResolver? resource = null,
        IContextResolver? context = null)
    {
        _subject = subject;
        _resource = resource;
        _context = context;
    }

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
