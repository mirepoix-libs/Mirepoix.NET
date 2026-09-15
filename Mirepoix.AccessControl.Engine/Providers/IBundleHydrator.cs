namespace Mirepoix.AccessControl.Providers;

public interface IBundleHydrator
{
    Task<AuthorizationBundle> HydrateAsync(AuthorizationRequest request, CancellationToken cancellationToken);
}
