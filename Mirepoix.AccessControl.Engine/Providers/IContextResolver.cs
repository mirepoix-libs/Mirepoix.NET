namespace Mirepoix.AccessControl.Providers;

public interface IContextResolver
{
    Task<AccessContext> HydrateAsync(AccessContext partial, CancellationToken cancellationToken);
}
