namespace Mirepoix.AccessControl.Providers;

public interface IResourceResolver
{
    Task<Resource> HydrateAsync(Resource partial, CancellationToken cancellationToken);
}
