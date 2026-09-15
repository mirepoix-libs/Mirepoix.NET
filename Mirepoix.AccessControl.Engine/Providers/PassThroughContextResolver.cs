namespace Mirepoix.AccessControl.Providers;

public sealed class PassThroughContextResolver : IContextResolver
{
    public Task<AccessContext> HydrateAsync(AccessContext partial, CancellationToken cancellationToken) =>
        Task.FromResult(partial);
}
